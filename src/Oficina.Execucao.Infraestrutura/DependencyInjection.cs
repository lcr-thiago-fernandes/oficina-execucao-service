using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Oficina.Execucao.Aplicacao.Execucoes;
using Oficina.Execucao.Infraestrutura.Mensageria;
using Oficina.Execucao.Infraestrutura.Persistencia;
using Oficina.Execucao.Infraestrutura.Telemetria;

namespace Oficina.Execucao.Infraestrutura;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpcoesDynamoDb>().Bind(configuration.GetSection(OpcoesDynamoDb.Secao));
        services.AddSingleton<IAmazonDynamoDB>(sp => CriarClienteDynamoDb(sp.GetRequiredService<IOptions<OpcoesDynamoDb>>().Value));
        services.AddSingleton<IRepositorioExecucao, RepositorioExecucaoDynamoDb>();
        // Registrado antes do MassTransit: a tabela precisa existir antes de o bus comecar a consumir.
        services.AddHostedService<CriadorTabelaHostedService>();

        services.AddSingleton<IRelogio, RelogioSistema>();
        services.AddSingleton<ITelemetriaExecucao, TelemetriaExecucaoNewRelic>();
        services.AddScoped<IPublicadorMensagens, PublicadorMensagensMassTransit>();
        services.AdicionarMensageria(configuration);
        return services;
    }

    private static IAmazonDynamoDB CriarClienteDynamoDb(OpcoesDynamoDb opcoes)
    {
        if (string.IsNullOrWhiteSpace(opcoes.ServiceUrl))
            return new AmazonDynamoDBClient(new AmazonDynamoDBConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(opcoes.Regiao) });

        // Somente DynamoDB Local / LocalStack: chaves ficticias. Na AWS vale a cadeia padrao (IRSA).
        return new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"),
            new AmazonDynamoDBConfig { ServiceURL = opcoes.ServiceUrl, AuthenticationRegion = opcoes.Regiao });
    }
}
