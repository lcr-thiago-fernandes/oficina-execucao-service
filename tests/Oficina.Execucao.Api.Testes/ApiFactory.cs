using System.Net.Http.Headers;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Execucao.Aplicacao.Contratos.Mensagens;
using Oficina.Execucao.Infraestrutura.Mensageria;
using Testcontainers.DynamoDb;

namespace Oficina.Execucao.Api.Testes;

/// <summary>Um unico DynamoDB Local para todos os testes da API; cada factory usa a sua tabela.</summary>
public sealed class DynamoDbApiFixture : IAsyncLifetime
{
    private readonly DynamoDbContainer _container = new DynamoDbBuilder("amazon/dynamodb-local:2.5.2").Build();

    public string ServiceUrl => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<DynamoDbApiFixture>
{
    public const string Nome = "Api";
}

/// <summary>
/// Sobe a API real com tabela propria no DynamoDB Local e o harness do MassTransit no lugar do bus
/// (mesmo registro de producao via <see cref="ConfiguracaoMensageria.ConfigurarRegistro"/>).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string PrefixoMensageria = "test";

    private readonly string _serviceUrl;
    private readonly string _capacidade;

    public ApiFactory(DynamoDbApiFixture dynamo, int capacidade = 20)
    {
        _serviceUrl = dynamo.ServiceUrl;
        _capacidade = capacidade.ToString();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Secret", GeradorTokenDeTeste.Secret);
        builder.UseSetting("Jwt:Issuer", GeradorTokenDeTeste.Issuer);
        builder.UseSetting("Jwt:Audience", GeradorTokenDeTeste.Audience);
        builder.UseSetting("DynamoDb:ServiceUrl", _serviceUrl);
        builder.UseSetting("DynamoDb:NomeTabela", $"t-{Guid.NewGuid():N}");
        builder.UseSetting("DynamoDb:CriarTabelaSeAusente", "true");
        builder.UseSetting("Mensageria:Transporte", "InMemory");
        builder.UseSetting("Mensageria:PrefixoAmbiente", PrefixoMensageria);
        builder.UseSetting("Execucao:CapacidadeMaximaPorUnidade", _capacidade);

        // O harness substitui o bus registrado pelo Program; consumidor e endpoint seguem identicos aos de producao.
        builder.ConfigureTestServices(s => s.AddMassTransitTestHarness(x =>
            ConfiguracaoMensageria.ConfigurarRegistro(x,
                new OpcoesMensageria { Transporte = "InMemory", PrefixoAmbiente = PrefixoMensageria })));
    }

    public ITestHarness Harness => Services.GetTestHarness();

    public HttpClient CriarClienteAutenticado(string perfil)
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GeradorTokenDeTeste.Gerar(perfil));
        return cliente;
    }

    /// <summary>Envia IniciarExecucao pela fila de comandos e aguarda consumo e publicacao (sem isso o teste e instavel).</summary>
    public async Task<Guid> SemearAsync(string unidade = "U1", int numeroOs = 1)
    {
        var osId = Guid.NewGuid();
        _ = CreateClient(); // garante o host (e o bus do harness) iniciado

        var harness = Harness;
        var endpoint = await harness.Bus.GetSendEndpoint(new Uri($"queue:{PrefixoMensageria}-oficina-execucao-comandos"));
        await endpoint.Send(new IniciarExecucao(osId, DateTime.UtcNow, numeroOs, unidade,
            [new ItemExecucaoMensagem("Troca de oleo", 1)], [new ItemExecucaoMensagem("Filtro", 2)]));

        (await harness.Consumed.Any<IniciarExecucao>(x => x.Context.Message.OrdemServicoId == osId)).Should().BeTrue();
        (await harness.Published.Any<ExecucaoEnfileirada>(x => x.Context.Message.OrdemServicoId == osId)).Should().BeTrue();
        return osId;
    }
}
