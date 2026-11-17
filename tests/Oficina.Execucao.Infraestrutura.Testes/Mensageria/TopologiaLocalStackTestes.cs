using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Execucao.Aplicacao;
using Oficina.Execucao.Aplicacao.Contratos.Mensagens;
using Testcontainers.LocalStack;

namespace Oficina.Execucao.Infraestrutura.Testes.Mensageria;

/// <summary>Sobe o bus real (Transporte=AmazonSqs) contra o LocalStack e confere os nomes finais no SNS/SQS.</summary>
public sealed class LocalStackFixture : IAsyncLifetime
{
    public const string Prefixo = "test";

    private readonly LocalStackContainer _container = new LocalStackBuilder("localstack/localstack:3.8.1")
        .WithEnvironment("SERVICES", "sqs,sns")
        .Build();

    private ServiceProvider? _provider;

    public IBusControl Bus { get; private set; } = null!;
    public IAmazonSimpleNotificationService Sns { get; private set; } = null!;
    public IAmazonSQS Sqs { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var serviceUrl = _container.GetConnectionString();

        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mensageria:Transporte"] = "AmazonSqs",
                ["Mensageria:PrefixoAmbiente"] = Prefixo,
                ["Mensageria:ServiceUrl"] = serviceUrl,
                // DynamoDB nao e exercitado aqui (o comando invalido falha antes de tocar no repositorio).
                ["DynamoDb:ServiceUrl"] = serviceUrl
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AdicionarAplicacao(configuracao);
        services.AdicionarInfraestrutura(configuracao);
        _provider = services.BuildServiceProvider(true);

        Bus = _provider.GetRequiredService<IBusControl>();
        await Bus.StartAsync(new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);

        var credenciais = new BasicAWSCredentials("test", "test");
        Sns = new AmazonSimpleNotificationServiceClient(credenciais,
            new AmazonSimpleNotificationServiceConfig { ServiceURL = serviceUrl, AuthenticationRegion = "us-east-1" });
        Sqs = new AmazonSQSClient(credenciais,
            new AmazonSQSConfig { ServiceURL = serviceUrl, AuthenticationRegion = "us-east-1" });
    }

    public async Task DisposeAsync()
    {
        if (Bus is not null) await Bus.StopAsync();
        if (_provider is not null) await _provider.DisposeAsync();
        Sns?.Dispose();
        Sqs?.Dispose();
        await _container.DisposeAsync();
    }

    public async Task<List<string>> ListarTopicosAsync()
    {
        var arns = new List<string>();
        string? proximo = null;
        do
        {
            var r = await Sns.ListTopicsAsync(new ListTopicsRequest { NextToken = proximo });
            arns.AddRange((r.Topics ?? []).Select(t => t.TopicArn));
            proximo = r.NextToken;
        } while (!string.IsNullOrEmpty(proximo));
        return arns;
    }

    public async Task<List<string>> ListarFilasAsync() =>
        (await Sqs.ListQueuesAsync(new ListQueuesRequest())).QueueUrls ?? [];
}

public sealed class TopologiaLocalStackTestes : IClassFixture<LocalStackFixture>
{
    private readonly LocalStackFixture _fx;

    public TopologiaLocalStackTestes(LocalStackFixture fx) => _fx = fx;

    [Fact]
    public async Task AmazonSqs_PublicarEvento_CriaTopicoComNomeDoContrato()
    {
        await _fx.Bus.Publish(new ExecucaoEnfileirada(Guid.NewGuid(), DateTime.UtcNow, 1));
        await _fx.Bus.Publish(new StatusExecucaoAlterado(Guid.NewGuid(), DateTime.UtcNow, StatusExecucaoMensagem.EmDiagnostico, null));

        var topicos = await _fx.ListarTopicosAsync();

        topicos.Should().Contain(t => t.EndsWith(":test-oficina-saga-ExecucaoEnfileirada"));
        topicos.Should().Contain(t => t.EndsWith(":test-oficina-saga-StatusExecucaoAlterado"));
    }

    [Fact]
    public async Task AmazonSqs_CriaFilaDeComandosComNomeDoContrato()
    {
        var filas = await _fx.ListarFilasAsync();

        filas.Should().Contain(u => u.EndsWith("/test-oficina-execucao-comandos"));
    }

    [Fact]
    public async Task AmazonSqs_TodasAsFilasTemPrefixoDoAmbiente()
    {
        // A fila do bus e criada sob demanda (resposta de request, handler conectado ao bus...);
        // conectar um handler forca a criacao para conferir o nome real no SQS.
        _fx.Bus.Address.AbsolutePath.Should().Be("/test-oficina-execucao-bus");
        var handle = _fx.Bus.ConnectHandler<ExecucaoFinalizada>(_ => Task.CompletedTask);
        try
        {
            await AguardarFilaAsync("test-oficina-execucao-bus", TimeSpan.FromSeconds(30));
        }
        finally
        {
            handle.Disconnect();
        }

        var filas = await _fx.ListarFilasAsync();

        filas.Should().NotBeEmpty();
        filas.Should().OnlyContain(u => u.Contains("/test-oficina-"));
        filas.Select(u => u.Substring(u.LastIndexOf('/') + 1)).Should().OnlyContain(n => n.StartsWith("test-oficina-"));
        filas.Should().Contain(u => u.EndsWith("/test-oficina-execucao-bus"));
    }

    [Fact]
    public async Task AmazonSqs_ConsumidorFalhando_NaoCriaTopicoFault()
    {
        var comandoInvalido = new IniciarExecucao(Guid.Empty, DateTime.UtcNow, 1, "SP-01", [], []);
        await (await _fx.Bus.GetSendEndpoint(new Uri("queue:test-oficina-execucao-comandos"))).Send(comandoInvalido);

        var urlErro = await AguardarFilaAsync("test-oficina-execucao-comandos_error", TimeSpan.FromSeconds(90));
        var recebida = await _fx.Sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = urlErro, WaitTimeSeconds = 10, MaxNumberOfMessages = 1
        });
        (recebida.Messages ?? []).Should().ContainSingle("a falha definitiva deve terminar na fila _error");

        var topicos = await _fx.ListarTopicosAsync();
        topicos.Should().OnlyContain(t => t.Substring(t.LastIndexOf(':') + 1).StartsWith("test-oficina-saga-"));
        topicos.Should().NotContain(t => t.Contains("Fault"));
    }

    private async Task<string> AguardarFilaAsync(string nome, TimeSpan limite)
    {
        var fim = DateTime.UtcNow + limite;
        while (DateTime.UtcNow < fim)
        {
            var url = (await _fx.ListarFilasAsync()).FirstOrDefault(u => u.EndsWith("/" + nome));
            if (url is not null) return url;
            await Task.Delay(500);
        }

        throw new TimeoutException($"A fila {nome} nao foi criada em {limite}.");
    }
}
