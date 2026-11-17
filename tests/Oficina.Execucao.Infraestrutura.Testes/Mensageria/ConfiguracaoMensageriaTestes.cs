using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using MassTransit;
using MassTransit.Serialization;
using MassTransit.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Execucao.Aplicacao.Contratos.Mensagens;
using Oficina.Execucao.Aplicacao.Execucoes;
using Oficina.Execucao.Infraestrutura.Mensageria;

namespace Oficina.Execucao.Infraestrutura.Testes.Mensageria;

public sealed class ConfiguracaoMensageriaTestes : IAsyncLifetime
{
    private ServiceProvider? _provider;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_provider is not null) await _provider.DisposeAsync();
    }

    /// <summary>Harness com a mesma configuracao de bus da producao (ConfigurarRegistro), transporte InMemory.</summary>
    private async Task<ITestHarness> IniciarHarnessAsync(string prefixo = "test")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // O handler lanca antes de tocar nas portas quando OrdemServicoId e vazio; as portas so precisam existir.
        services.AddSingleton(Moq.Mock.Of<IRepositorioExecucao>());
        services.AddSingleton(Moq.Mock.Of<IRelogio>());
        services.AddSingleton(Moq.Mock.Of<ITelemetriaExecucao>());
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new OpcoesExecucao()));
        services.AddScoped<EnfileirarExecucaoHandler>();
        services.AddScoped<IPublicadorMensagens, PublicadorMensagensMassTransit>();
        services.AddMassTransitTestHarness(x => ConfiguracaoMensageria.ConfigurarRegistro(x,
            new OpcoesMensageria { Transporte = "InMemory", PrefixoAmbiente = prefixo }));

        _provider = services.BuildServiceProvider(true);
        var harness = _provider.GetRequiredService<ITestHarness>();
        harness.TestTimeout = TimeSpan.FromSeconds(90);
        await harness.Start();
        return harness;
    }

    [Fact]
    public void TransporteDesconhecido_FalhaComMensagemClara()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Mensageria:Transporte"] = "Kafka" })
            .Build();

        var acao = () => new ServiceCollection().AdicionarMensageria(configuracao);

        acao.Should().Throw<InvalidOperationException>()
            .WithMessage("*Kafka*")
            .And.Message.Should().Contain("AmazonSqs").And.Contain("InMemory");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("PRD")]
    [InlineData("hml_1")]
    [InlineData("prd.oficina")]
    [InlineData("prd\n")]
    public void PrefixoAmbienteInvalido_FalhaNaInicializacao(string prefixo)
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Mensageria:PrefixoAmbiente"] = prefixo })
            .Build();

        var acao = () => new ServiceCollection().AdicionarMensageria(configuracao);

        acao.Should().Throw<InvalidOperationException>().WithMessage("*PrefixoAmbiente*");
    }

    [Fact]
    public async Task InMemory_ConsumidorFalhando_NaoPublicaFault()
    {
        var harness = await IniciarHarnessAsync();
        var comandoInvalido = new IniciarExecucao(Guid.Empty, DateTime.UtcNow, 1, "SP-01", [], []);

        await (await harness.Bus.GetSendEndpoint(new Uri("queue:test-oficina-execucao-comandos"))).Send(comandoInvalido);

        (await harness.Consumed.Any<IniciarExecucao>(c => c.Exception is not null)).Should().BeTrue();
        await harness.InactivityTask;
        harness.Published.Select<Fault<IniciarExecucao>>().Should().BeEmpty();
        harness.Published.Select<Fault>().Should().BeEmpty();
    }

    public static TheoryData<Type> TiposDaSaga => new()
    {
        typeof(IniciarExecucao),
        typeof(ExecucaoEnfileirada),
        typeof(ExecucaoRecusada),
        typeof(StatusExecucaoAlterado),
        typeof(ExecucaoFinalizada)
    };

    [Theory, MemberData(nameof(TiposDaSaga))]
    public async Task TopologiaInMemory_EntityNameTemPrefixo(Type tipo)
    {
        var harness = await IniciarHarnessAsync(prefixo: "prd");

        var topologia = typeof(IBusTopology).GetMethod(nameof(IBusTopology.Message))!
            .MakeGenericMethod(tipo)
            .Invoke(harness.Bus.Topology, null)!;
        var entityName = (string)topologia.GetType().GetProperty("EntityName")!.GetValue(topologia)!;

        entityName.Should().Be($"prd-oficina-saga-{tipo.Name}");
    }

    [Fact]
    public async Task SerializadorDoBus_UsaEnumComoString()
    {
        var harness = await IniciarHarnessAsync();

        await harness.Bus.Publish(new StatusExecucaoAlterado(Guid.NewGuid(), DateTime.UtcNow, StatusExecucaoMensagem.EmReparo, null));

        (await harness.Published.Any<StatusExecucaoAlterado>()).Should().BeTrue();
        var envelope = SerializarEnvelope(harness.Published.Select<StatusExecucaoAlterado>().Single().Context, typeof(StatusExecucaoAlterado));
        envelope["message"]!["statusExecucao"]!.GetValue<string>().Should().Be("EmReparo");
    }

    public static TheoryData<Type, string> Produzidas => new()
    {
        { typeof(ExecucaoEnfileirada), "ExecucaoEnfileirada" },
        { typeof(ExecucaoRecusada), "ExecucaoRecusada" },
        { typeof(StatusExecucaoAlterado), "StatusExecucaoAlterado" },
        { typeof(ExecucaoFinalizada), "ExecucaoFinalizada" }
    };

    [Theory, MemberData(nameof(Produzidas))]
    public async Task SerializadorDoBus_CorpoIgualAoJsonDoContrato(Type tipo, string nome)
    {
        var harness = await IniciarHarnessAsync();
        var contrato = JsonNode.Parse(File.ReadAllText(Path.Combine("Contratos", "Mensagens", $"{nome}.json")))!;
        var mensagem = contrato.Deserialize(tipo, OpcoesJsonContrato.Criar())!;

        await harness.Bus.Publish(mensagem, tipo);

        (await harness.Published.Any(p => p.MessageType == tipo)).Should().BeTrue();
        var publicada = harness.Published.Select(p => p.MessageType == tipo).Single();
        var envelope = SerializarEnvelope(publicada.Context, tipo);
        JsonNode.DeepEquals(envelope["message"], contrato).Should().BeTrue(
            $"o corpo publicado de {nome} diverge do contrato: {envelope["message"]!.ToJsonString()}");
    }

    /// <summary>Serializa o contexto publicado com o serializador System.Text.Json do bus (opcoes ajustadas por ConfigureJsonSerializerOptions).</summary>
    private static JsonNode SerializarEnvelope(SendContext contexto, Type tipo)
    {
        var metodo = typeof(SystemTextJsonMessageSerializer).GetMethods()
            .Single(m => m.Name == nameof(SystemTextJsonMessageSerializer.GetMessageBody) && m.IsGenericMethodDefinition)
            .MakeGenericMethod(tipo);
        var corpo = (MessageBody)metodo.Invoke(SystemTextJsonMessageSerializer.Instance, [contexto])!;
        return JsonNode.Parse(corpo.GetBytes())!;
    }
}
