using Amazon.DynamoDBv2;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Oficina.Execucao.Aplicacao;
using Oficina.Execucao.Aplicacao.Execucoes;
using Oficina.Execucao.Infraestrutura.Mensageria;
using Oficina.Execucao.Infraestrutura.Persistencia;
using Oficina.Execucao.Infraestrutura.Telemetria;

namespace Oficina.Execucao.Infraestrutura.Testes;

public class DependencyInjectionTestes
{
    private static ServiceProvider Construir(Dictionary<string, string?> valores)
    {
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AdicionarAplicacao(configuracao);
        services.AdicionarInfraestrutura(configuracao);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public async Task AdicionarInfraestrutura_InMemory_ResolvePortasEHostedServices()
    {
        await using var provider = Construir(new() { ["DynamoDb:ServiceUrl"] = "http://localhost:8000" });
        await using var escopo = provider.CreateAsyncScope();

        escopo.ServiceProvider.GetRequiredService<IRepositorioExecucao>().Should().BeOfType<RepositorioExecucaoDynamoDb>();
        escopo.ServiceProvider.GetRequiredService<IPublicadorMensagens>().Should().BeOfType<PublicadorMensagensMassTransit>();
        provider.GetRequiredService<ITelemetriaExecucao>().Should().BeOfType<TelemetriaExecucaoNewRelic>();
        provider.GetRequiredService<IRelogio>().Should().BeOfType<RelogioSistema>();
        provider.GetRequiredService<IAmazonDynamoDB>().Config.ServiceURL.Should().StartWith("http://localhost:8000");
        provider.GetServices<IHostedService>().Should().Contain(h => h is CriadorTabelaHostedService);
        provider.GetRequiredService<IBus>().Should().NotBeNull();
    }

    [Fact]
    public async Task AdicionarInfraestrutura_CriadorDeTabelaAntesDoBus()
    {
        // O hosted service da tabela precisa iniciar antes do bus (que ja consome comandos).
        await using var provider = Construir(new());

        var hosted = provider.GetServices<IHostedService>().ToList();

        hosted.First().Should().BeOfType<CriadorTabelaHostedService>();
    }

    [Fact]
    public void AdicionarInfraestrutura_PublicadorEScoped()
    {
        var configuracao = new ConfigurationBuilder().Build();
        var services = new ServiceCollection().AdicionarInfraestrutura(configuracao);

        services.Single(d => d.ServiceType == typeof(IPublicadorMensagens)).Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void RelogioSistema_RetornaUtcAtual()
    {
        var agora = new RelogioSistema().AgoraUtc;

        agora.Kind.Should().Be(DateTimeKind.Utc);
        agora.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
