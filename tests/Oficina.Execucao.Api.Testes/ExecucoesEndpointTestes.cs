using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using MassTransit.Testing;
using Oficina.Execucao.Aplicacao.Contratos.Mensagens;

namespace Oficina.Execucao.Api.Testes;

[Collection(ApiCollection.Nome)]
public class ExecucoesEndpointTestes(DynamoDbApiFixture dynamo)
{
    private const string Base = "/execucao/api/v1/execucoes";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static Task<HttpResponseMessage> Post(HttpClient c, Guid osId, string etapa, object? corpo = null) =>
        c.PostAsJsonAsync($"{Base}/{osId}/{etapa}", corpo ?? new { }, Json);

    private static async Task<string> StatusDe(HttpClient c, Guid osId)
    {
        var resposta = await c.GetAsync($"{Base}/{osId}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("status").GetString()!;
    }

    [Fact]
    public async Task FluxoCompleto_PublicaEventosNaOrdem()
    {
        await using var factory = new ApiFactory(dynamo);
        var osId = await factory.SemearAsync();
        var c = factory.CriarClienteAutenticado("Atendente");

        (await Post(c, osId, "diagnostico", new { observacao = "ok" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(c, osId, "reparo")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(c, osId, "finalizacao")).StatusCode.Should().Be(HttpStatusCode.OK);

        var h = factory.Harness;
        (await h.Published.Any<StatusExecucaoAlterado>(x => x.Context.Message.OrdemServicoId == osId
            && x.Context.Message.StatusExecucao == StatusExecucaoMensagem.EmDiagnostico)).Should().BeTrue();
        (await h.Published.Any<StatusExecucaoAlterado>(x => x.Context.Message.OrdemServicoId == osId
            && x.Context.Message.StatusExecucao == StatusExecucaoMensagem.EmReparo)).Should().BeTrue();
        (await h.Published.Any<ExecucaoFinalizada>(x => x.Context.Message.OrdemServicoId == osId)).Should().BeTrue();
        (await StatusDe(c, osId)).Should().Be("Finalizada");
    }

    [Fact]
    public async Task PostDiagnostico_Repetido_200ERepublica()
    {
        await using var factory = new ApiFactory(dynamo);
        var osId = await factory.SemearAsync();
        var c = factory.CriarClienteAutenticado("Admin");

        (await Post(c, osId, "diagnostico")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(c, osId, "diagnostico")).StatusCode.Should().Be(HttpStatusCode.OK);

        var publicadas = factory.Harness.Published
            .Select<StatusExecucaoAlterado>(x => x.Context.Message.OrdemServicoId == osId).ToList();
        publicadas.Should().HaveCount(2);
        (await StatusDe(c, osId)).Should().Be("EmDiagnostico");
    }

    [Fact]
    public async Task PostReparo_NaFila_422()
    {
        await using var factory = new ApiFactory(dynamo);
        var osId = await factory.SemearAsync();
        var c = factory.CriarClienteAutenticado("Atendente");

        (await Post(c, osId, "reparo")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_OsInexistente_404()
    {
        await using var factory = new ApiFactory(dynamo);
        var c = factory.CriarClienteAutenticado("Atendente");

        (await Post(c, Guid.NewGuid(), "diagnostico")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await c.GetAsync($"{Base}/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_ObservacaoAcimaDe500_400()
    {
        await using var factory = new ApiFactory(dynamo);
        var osId = await factory.SemearAsync();
        var c = factory.CriarClienteAutenticado("Atendente");

        (await Post(c, osId, "diagnostico", new { observacao = new string('x', 501) }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_StatusInvalido_400()
    {
        await using var factory = new ApiFactory(dynamo);
        var c = factory.CriarClienteAutenticado("Atendente");

        (await c.GetAsync($"{Base}?status=Qualquer")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_SemStatus_ListaSoAtivas()
    {
        await using var factory = new ApiFactory(dynamo);
        var ativa = await factory.SemearAsync(numeroOs: 1);
        var finalizada = await factory.SemearAsync(numeroOs: 2);
        var c = factory.CriarClienteAutenticado("Atendente");
        foreach (var etapa in new[] { "diagnostico", "reparo", "finalizacao" })
            (await Post(c, finalizada, etapa)).StatusCode.Should().Be(HttpStatusCode.OK);

        var resposta = await c.GetAsync(Base);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var ids = doc.RootElement.EnumerateArray().Select(e => e.GetProperty("ordemServicoId").GetGuid()).ToList();
        ids.Should().Contain(ativa).And.NotContain(finalizada);

        var porStatus = await c.GetAsync($"{Base}?status=Finalizada");
        using var doc2 = JsonDocument.Parse(await porStatus.Content.ReadAsStringAsync());
        doc2.RootElement.EnumerateArray().Select(e => e.GetProperty("ordemServicoId").GetGuid())
            .Should().Contain(finalizada);
    }
}
