using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using MassTransit.Testing;
using Oficina.Execucao.Api.Testes;
using Oficina.Execucao.Aplicacao.Contratos.Mensagens;
using Oficina.Execucao.Bdd.Testes.Suporte;
using Reqnroll;

namespace Oficina.Execucao.Bdd.Testes.Steps;

[Binding]
public sealed class FluxoExecucaoSteps(AmbienteBdd ambiente)
{
    private const string Base = "/execucao/api/v1/execucoes";
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(15);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private HttpClient? _cliente;
    private HttpClient Cliente => _cliente ??= ambiente.Factory.CriarClienteAutenticado("Atendente");
    private Guid OsAtual => ambiente.IdDa(ambiente.UltimaOs ?? throw new InvalidOperationException("Nenhuma OS enviada."));

    [Given("que a unidade {string} aceita no máximo {int} OS ativa")]
    public void DadoCapacidade(string unidade, int capacidade) => ambiente.Iniciar(capacidade);

    [When("o OS Service enviar IniciarExecucao para a OS {int} da unidade {string}")]
    public Task QuandoEnviar(int numeroOs, string unidade) => EnviarAsync(numeroOs, unidade, 1);

    [When("o OS Service enviar IniciarExecucao para a OS {int} da unidade {string} duas vezes")]
    public Task QuandoEnviarDuasVezes(int numeroOs, string unidade) => EnviarAsync(numeroOs, unidade, 2);

    [When("o mecânico registrar o diagnóstico com a observação {string}")]
    public Task QuandoDiagnostico(string observacao) => PostAsync("diagnostico", observacao);

    [When("o mecânico registrar o reparo")]
    public Task QuandoReparo() => PostAsync("reparo", null);

    [When("o mecânico registrar a finalização com a observação {string}")]
    public Task QuandoFinalizacao(string observacao) => PostAsync("finalizacao", observacao);

    [Then("o evento ExecucaoEnfileirada é publicado com posição {int}")]
    public async Task EntaoEnfileiradaComPosicao(int posicao)
    {
        await AguardarConsumoAsync(OsAtual, 1);
        (await Harness.Published.Any<ExecucaoEnfileirada>(x =>
            x.Context.Message.OrdemServicoId == OsAtual && x.Context.Message.PosicaoFila == posicao)).Should().BeTrue();
    }

    [Then("o evento ExecucaoEnfileirada é publicado para a OS {int}")]
    public async Task EntaoEnfileiradaParaOs(int numeroOs)
    {
        var id = ambiente.IdDa(numeroOs);
        await AguardarConsumoAsync(id, 1);
        (await Harness.Published.Any<ExecucaoEnfileirada>(x => x.Context.Message.OrdemServicoId == id)).Should().BeTrue();
    }

    [Then("o evento ExecucaoEnfileirada é publicado {int} vezes para a OS {int}")]
    public async Task EntaoEnfileiradaVezes(int vezes, int numeroOs)
    {
        var id = ambiente.IdDa(numeroOs);
        await AguardarConsumoAsync(id, vezes);
        await EsperarAsync(() => PublicadasEnfileirada(id) >= vezes);
        PublicadasEnfileirada(id).Should().Be(vezes);
    }

    [Then("o evento ExecucaoRecusada é publicado com o motivo {string}")]
    public async Task EntaoRecusadaComMotivo(string motivo)
    {
        await AguardarConsumoAsync(OsAtual, 1);
        (await Harness.Published.Any<ExecucaoRecusada>(x =>
            x.Context.Message.OrdemServicoId == OsAtual && x.Context.Message.Motivo == motivo)).Should().BeTrue();
    }

    [Then("o evento ExecucaoRecusada é publicado para a OS {int}")]
    public async Task EntaoRecusadaParaOs(int numeroOs)
    {
        var id = ambiente.IdDa(numeroOs);
        await AguardarConsumoAsync(id, 1);
        (await Harness.Published.Any<ExecucaoRecusada>(x => x.Context.Message.OrdemServicoId == id)).Should().BeTrue();
    }

    [Then("nenhum evento ExecucaoEnfileirada é publicado")]
    public async Task EntaoNenhumaEnfileirada()
    {
        // So afirma ausencia depois do consumo e da recusa (o consumidor ja terminou de decidir).
        await AguardarConsumoAsync(OsAtual, 1);
        (await Harness.Published.Any<ExecucaoRecusada>(x => x.Context.Message.OrdemServicoId == OsAtual)).Should().BeTrue();
        PublicadasEnfileirada(OsAtual).Should().Be(0);
    }

    [Then("o evento StatusExecucaoAlterado é publicado com status {string}")]
    public async Task EntaoStatusAlterado(string status)
    {
        var esperado = Enum.Parse<StatusExecucaoMensagem>(status);
        (await Harness.Published.Any<StatusExecucaoAlterado>(x =>
            x.Context.Message.OrdemServicoId == OsAtual && x.Context.Message.StatusExecucao == esperado)).Should().BeTrue();
    }

    [Then("o evento ExecucaoFinalizada é publicado")]
    public async Task EntaoFinalizada() =>
        (await Harness.Published.Any<ExecucaoFinalizada>(x => x.Context.Message.OrdemServicoId == OsAtual)).Should().BeTrue();

    [Then("a execução da OS {int} consta como {string}")]
    public async Task EntaoConstaComo(int numeroOs, string status)
    {
        var resposta = await Cliente.GetAsync($"{Base}/{ambiente.IdDa(numeroOs)}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("status").GetString().Should().Be(status);
    }

    private ITestHarness Harness => ambiente.Factory.Harness;

    private int PublicadasEnfileirada(Guid id) =>
        Harness.Published.Select<ExecucaoEnfileirada>(x => x.Context.Message.OrdemServicoId == id).Count();

    private async Task EnviarAsync(int numeroOs, string unidade, int vezes)
    {
        var id = ambiente.IdDa(numeroOs);
        ambiente.UltimaOs = numeroOs;
        _ = ambiente.Factory.CreateClient(); // garante host e bus do harness iniciados

        var endpoint = await Harness.Bus.GetSendEndpoint(
            new Uri($"queue:{ApiFactory.PrefixoMensageria}-oficina-execucao-comandos"));
        var comando = new IniciarExecucao(id, DateTime.UtcNow, numeroOs, unidade,
            [new ItemExecucaoMensagem("Troca de oleo", 1)], [new ItemExecucaoMensagem("Filtro", 2)]);
        for (var i = 0; i < vezes; i++) await endpoint.Send(comando);
    }

    private async Task PostAsync(string etapa, string? observacao)
    {
        var resposta = await Cliente.PostAsJsonAsync($"{Base}/{OsAtual}/{etapa}", new { observacao }, Json);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task AguardarConsumoAsync(Guid id, int vezes)
    {
        await EsperarAsync(() =>
            Harness.Consumed.Select<IniciarExecucao>(x => x.Context.Message.OrdemServicoId == id).Count() >= vezes);
        Harness.Consumed.Select<IniciarExecucao>(x => x.Context.Message.OrdemServicoId == id).Count()
            .Should().BeGreaterThanOrEqualTo(vezes);
    }

    private static async Task EsperarAsync(Func<bool> condicao)
    {
        var fim = DateTime.UtcNow + Limite;
        while (!condicao() && DateTime.UtcNow < fim) await Task.Delay(50);
    }
}
