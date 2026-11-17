using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Oficina.Execucao.Aplicacao;
using Oficina.Execucao.Aplicacao.Contratos.Mensagens;
using Oficina.Execucao.Aplicacao.Execucoes;
using Oficina.Execucao.Infraestrutura.Mensageria;
using Oficina.Execucao.Infraestrutura.Persistencia;
using Oficina.Execucao.Infraestrutura.Testes.Persistencia;

namespace Oficina.Execucao.Infraestrutura.Testes.Mensageria;

[Collection(DynamoDbCollection.Nome)]
public sealed class IniciarExecucaoConsumerTestes : IAsyncLifetime
{
    private static readonly Uri FilaComandos = new("queue:test-oficina-execucao-comandos");

    private readonly DynamoDbFixture _fx;
    private ServiceProvider? _provider;

    public IniciarExecucaoConsumerTestes(DynamoDbFixture fx) => _fx = fx;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_provider is not null) await _provider.DisposeAsync();
    }

    private async Task<(ITestHarness Harness, string Tabela)> IniciarAsync(int capacidade = 20)
    {
        var tabela = await _fx.CriarTabelaAsync();
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Execucao:CapacidadeMaximaPorUnidade"] = capacidade.ToString()
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AdicionarAplicacao(configuracao);
        services.AddSingleton(_fx.Cliente);
        services.AddSingleton<IOptions<OpcoesDynamoDb>>(Options.Create(new OpcoesDynamoDb { NomeTabela = tabela }));
        services.AddSingleton<IRepositorioExecucao, RepositorioExecucaoDynamoDb>();
        services.AddSingleton<IRelogio, RelogioSistema>();
        services.AddSingleton<ITelemetriaExecucao, TelemetriaNula>();
        services.AddScoped<IPublicadorMensagens, PublicadorMensagensMassTransit>();
        services.AddMassTransitTestHarness(x => ConfiguracaoMensageria.ConfigurarRegistro(x,
            new OpcoesMensageria { Transporte = "InMemory", PrefixoAmbiente = "test" }));

        _provider = services.BuildServiceProvider(true);
        var harness = _provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, tabela);
    }

    private static IniciarExecucao Comando(Guid osId, int numeroOs = 1042) =>
        new(osId, DateTime.UtcNow, numeroOs, "SP-01",
            [new ItemExecucaoMensagem("Troca de oleo", 1)],
            [new ItemExecucaoMensagem("Filtro de oleo", 2)]);

    private static async Task EnviarAsync(ITestHarness harness, IniciarExecucao comando) =>
        await (await harness.Bus.GetSendEndpoint(FilaComandos)).Send(comando);

    private static async Task AguardarConsumidasAsync(ITestHarness harness, int quantidade)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (harness.Consumed.Select<IniciarExecucao>().Count() < quantidade && DateTime.UtcNow < limite)
            await Task.Delay(50);
        harness.Consumed.Select<IniciarExecucao>().Should().HaveCount(quantidade);
    }

    private async Task<int?> AtivasAsync(string tabela, string unidade)
    {
        var r = await _fx.Cliente.GetItemAsync(tabela,
            new() { ["osId"] = new AttributeValue(EsquemaTabelaExecucao.ChaveContador(unidade)) });
        return r.IsItemSet ? int.Parse(r.Item["ativas"].N) : null;
    }

    [Fact]
    public async Task Consumir_Novo_PublicaExecucaoEnfileiradaPosicao1()
    {
        var (harness, _) = await IniciarAsync();
        var osA = Guid.NewGuid();

        await EnviarAsync(harness, Comando(osA));

        (await harness.Consumed.Any<IniciarExecucao>()).Should().BeTrue();
        (await harness.Published.Any<ExecucaoEnfileirada>()).Should().BeTrue();
        var publicada = harness.Published.Select<ExecucaoEnfileirada>().Single().Context.Message;
        publicada.OrdemServicoId.Should().Be(osA);
        publicada.PosicaoFila.Should().Be(1);
    }

    [Fact]
    public async Task Consumir_MesmaMensagemDuasVezes_RepublicaEContaUmaVaga()
    {
        var (harness, tabela) = await IniciarAsync();
        var osA = Guid.NewGuid();

        await EnviarAsync(harness, Comando(osA));
        await AguardarConsumidasAsync(harness, 1);
        await EnviarAsync(harness, Comando(osA));
        await AguardarConsumidasAsync(harness, 2);

        var publicadas = harness.Published.Select<ExecucaoEnfileirada>().Select(p => p.Context.Message).ToList();
        publicadas.Should().HaveCount(2);
        publicadas.Should().OnlyContain(m => m.OrdemServicoId == osA && m.PosicaoFila == 1);
        (await AtivasAsync(tabela, "SP-01")).Should().Be(1);
        harness.Published.Select<ExecucaoRecusada>().Should().BeEmpty();
    }

    [Fact]
    public async Task Consumir_CapacidadeZero_PublicaExecucaoRecusada()
    {
        var (harness, tabela) = await IniciarAsync(capacidade: 0);
        var osA = Guid.NewGuid();

        await EnviarAsync(harness, Comando(osA));

        (await harness.Published.Any<ExecucaoRecusada>()).Should().BeTrue();
        var recusa = harness.Published.Select<ExecucaoRecusada>().Single().Context.Message;
        recusa.OrdemServicoId.Should().Be(osA);
        recusa.Motivo.Should().Be("Capacidade da unidade SP-01 esgotada (0/0)");
        harness.Published.Select<ExecucaoEnfileirada>().Should().BeEmpty();
        (await AtivasAsync(tabela, "SP-01")).Should().BeNull();
    }

    [Fact]
    public async Task Consumir_RecusadaDepoisDuplicadaComVaga_RepublicaRecusa()
    {
        // 1a entrega: comando invalido (NumeroOs 0) => recusa gravada.
        // 2a entrega do mesmo comando com vaga sobrando: deve republicar a mesma recusa, nunca enfileirar.
        var (harness, tabela) = await IniciarAsync(capacidade: 5);
        var osA = Guid.NewGuid();
        var comando = Comando(osA, numeroOs: 0);

        await EnviarAsync(harness, comando);
        await AguardarConsumidasAsync(harness, 1);
        await EnviarAsync(harness, comando);
        await AguardarConsumidasAsync(harness, 2);

        var recusas = harness.Published.Select<ExecucaoRecusada>().Select(p => p.Context.Message).ToList();
        recusas.Should().HaveCount(2);
        recusas.Select(r => r.Motivo).Distinct().Should().ContainSingle();
        harness.Published.Select<ExecucaoEnfileirada>().Should().BeEmpty();
        (await AtivasAsync(tabela, "SP-01")).Should().BeNull();
    }

    private sealed class TelemetriaNula : ITelemetriaExecucao
    {
        public void Registrar(EventoExecucao evento) { }
    }
}
