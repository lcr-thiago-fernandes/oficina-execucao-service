using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Oficina.Execucao.Infraestrutura.Persistencia;
using Testcontainers.DynamoDb;

namespace Oficina.Execucao.Infraestrutura.Testes.Persistencia;

public sealed class DynamoDbFixture : IAsyncLifetime
{
    private readonly DynamoDbContainer _container = new DynamoDbBuilder("amazon/dynamodb-local:2.5.2")
        .Build();

    public IAmazonDynamoDB Cliente { get; private set; } = null!;

    public string ServiceUrl => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Cliente = new AmazonDynamoDBClient(
            new BasicAWSCredentials("local", "local"),
            new AmazonDynamoDBConfig { ServiceURL = ServiceUrl, AuthenticationRegion = "us-east-1" });
    }

    public async Task DisposeAsync()
    {
        Cliente?.Dispose();
        await _container.DisposeAsync();
    }

    public async Task<string> CriarTabelaAsync()
    {
        var nome = $"t-{Guid.NewGuid():N}";
        await Cliente.CreateTableAsync(EsquemaTabelaExecucao.CriarRequest(nome));
        return nome;
    }
}

[CollectionDefinition(Nome)]
public sealed class DynamoDbCollection : ICollectionFixture<DynamoDbFixture>
{
    public const string Nome = "DynamoDb";
}
