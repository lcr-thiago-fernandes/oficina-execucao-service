using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Oficina.Execucao.Api.Testes;

[Collection(ApiCollection.Nome)]
public class InfraHttpTestes(DynamoDbApiFixture dynamo)
{
    [Fact]
    public async Task Get_Health_SemToken_200()
    {
        await using var factory = new ApiFactory(dynamo);
        var cliente = factory.CreateClient();

        foreach (var rota in new[] { "/execucao/health", "/health" })
        {
            var resposta = await cliente.GetAsync(rota);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK, rota);
            (await resposta.Content.ReadAsStringAsync()).Should().Contain("\"status\":\"ok\"");
        }
    }

    [Fact]
    public async Task Swagger_AtrasDoPathBase_200()
    {
        await using var factory = new ApiFactory(dynamo);
        var resposta = await factory.CreateClient().GetAsync("/execucao/swagger/v1/swagger.json");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("/api/v1/execucoes/{osId}/diagnostico");
    }

    [Fact]
    public async Task Resposta_EcoaXCorrelationId()
    {
        await using var factory = new ApiFactory(dynamo);
        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/execucao/health");
        requisicao.Headers.Add("X-Correlation-Id", "abc-123");

        var resposta = await factory.CreateClient().SendAsync(requisicao);

        resposta.Headers.GetValues("X-Correlation-Id").Should().ContainSingle().Which.Should().Be("abc-123");
    }

    [Fact]
    public void Inicializacao_CapacidadeNegativa_Falha()
    {
        using var factory = new ApiFactory(dynamo, capacidade: -1);

        var acao = () => factory.CreateClient();

        var excecao = acao.Should().Throw<Exception>().Which;
        TemNaCadeia<OptionsValidationException>(excecao).Should().BeTrue("a validacao deve derrubar a inicializacao: {0}", excecao);
    }

    private static bool TemNaCadeia<T>(Exception? excecao)
    {
        for (var atual = excecao; atual is not null; atual = atual.InnerException)
            if (atual is T) return true;
        return false;
    }
}
