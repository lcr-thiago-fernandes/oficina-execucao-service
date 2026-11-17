using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;

namespace Oficina.Execucao.Api.Testes;

[Collection(ApiCollection.Nome)]
public class AutorizacaoTestes(DynamoDbApiFixture dynamo)
{
    private const string Rota = "/execucao/api/v1/execucoes";

    [Fact]
    public async Task Get_SemToken_401()
    {
        await using var factory = new ApiFactory(dynamo);
        (await factory.CreateClient().GetAsync(Rota)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_PerfilCliente_403()
    {
        await using var factory = new ApiFactory(dynamo);
        (await factory.CriarClienteAutenticado("Cliente").GetAsync(Rota)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Atendente")]
    [InlineData("Admin")]
    public async Task Get_PerfilMecanico_200(string perfil)
    {
        await using var factory = new ApiFactory(dynamo);
        var resposta = await factory.CriarClienteAutenticado(perfil).GetAsync(Rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        json.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Get_TokenComAssinaturaErrada_401()
    {
        await using var factory = new ApiFactory(dynamo);
        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            GeradorTokenDeTeste.Gerar("Admin", segredo: "outro-segredo-com-mais-de-32-caracteres-hs256"));

        (await cliente.GetAsync(Rota)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_TokenExpirado_401()
    {
        await using var factory = new ApiFactory(dynamo);
        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GeradorTokenDeTeste.Gerar("Admin", expirado: true));

        (await cliente.GetAsync(Rota)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_TokenComAudienciaErrada_401()
    {
        await using var factory = new ApiFactory(dynamo);
        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GeradorTokenDeTeste.Gerar("Admin", audiencia: "outra-api"));

        (await cliente.GetAsync(Rota)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
