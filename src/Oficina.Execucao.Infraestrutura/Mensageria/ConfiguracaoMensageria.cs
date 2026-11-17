using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Execucao.Aplicacao.Contratos.Mensagens;

namespace Oficina.Execucao.Infraestrutura.Mensageria;

public static partial class ConfiguracaoMensageria
{
    public const string TransporteAmazonSqs = "AmazonSqs";
    public const string TransporteInMemory = "InMemory";

    public static IServiceCollection AdicionarMensageria(this IServiceCollection services, IConfiguration configuration)
    {
        var opcoes = configuration.GetSection(OpcoesMensageria.Secao).Get<OpcoesMensageria>() ?? new OpcoesMensageria();
        services.AddMassTransit(x => ConfigurarRegistro(x, opcoes));
        return services;
    }

    /// <summary>
    /// Registro compartilhado entre producao (<c>AddMassTransit</c>) e testes (<c>AddMassTransitTestHarness</c>):
    /// consumidor, fila de comandos, prefixo por tipo e PublishFaults=false identicos nos dois transportes;
    /// a fila do bus e fixada no SQS (o InMemory do 8.5.12 nao permite renomea-la).
    /// </summary>
    public static void ConfigurarRegistro(IBusRegistrationConfigurator x, OpcoesMensageria opcoes)
    {
        var prefixo = opcoes.PrefixoAmbiente;
        if (string.IsNullOrEmpty(prefixo) || !PrefixoValido().IsMatch(prefixo))
            throw new InvalidOperationException(
                $"Valor de {OpcoesMensageria.Secao}:PrefixoAmbiente invalido: '{prefixo}'. " +
                "Use apenas letras minusculas, digitos e '-' (ex.: local, hml, prd).");

        x.AddConsumer<IniciarExecucaoConsumer>();

        if (string.Equals(opcoes.Transporte, TransporteInMemory, StringComparison.OrdinalIgnoreCase))
        {
            x.UsingInMemory((ctx, cfg) =>
            {
                // O InMemory do 8.5.12 nao expoe OverrideDefaultBusEndpointQueueName (so SQS/SQL); sem IAM, o nome e irrelevante.
                ConfigurarBus(ctx, cfg, prefixo);
            });
        }
        else if (string.Equals(opcoes.Transporte, TransporteAmazonSqs, StringComparison.OrdinalIgnoreCase))
        {
            x.UsingAmazonSqs((ctx, cfg) =>
            {
                cfg.Host(opcoes.Regiao, h =>
                {
                    if (!string.IsNullOrWhiteSpace(opcoes.ServiceUrl))
                    {
                        // Somente LocalStack: endpoint local e chaves ficticias. Na AWS vale a cadeia padrao (IRSA).
                        h.Config(new AmazonSQSConfig { ServiceURL = opcoes.ServiceUrl });
                        h.Config(new AmazonSimpleNotificationServiceConfig { ServiceURL = opcoes.ServiceUrl });
                        h.AccessKey("test");
                        h.SecretKey("test");
                    }
                });
                // O nome padrao {maquina}_{assembly}_bus_{id} daria AccessDenied no EKS (IAM so permite ${amb}-oficina-*).
                // A fila e auto-delete e cada replica precisa da sua: sufixo com a instancia (nome do pod).
                cfg.OverrideDefaultBusEndpointQueueName(NomesMensageria.FilaBus(prefixo, NomesMensageria.InstanciaAtual()));
                ConfigurarBus(ctx, cfg, prefixo);
            });
        }
        else
        {
            throw new InvalidOperationException(
                $"Valor de {OpcoesMensageria.Secao}:Transporte desconhecido: '{opcoes.Transporte}'. " +
                $"Valores aceitos: {TransporteAmazonSqs}, {TransporteInMemory}.");
        }
    }

    private static void ConfigurarBus(IBusRegistrationContext ctx, IBusFactoryConfigurator cfg, string prefixo)
    {
        cfg.ConfigureJsonSerializerOptions(o =>
        {
            o.Converters.Add(new JsonStringEnumConverter());
            return o;
        });

        // [EntityName] nos contratos faz o 8.5.12 ignorar PrefixEntityNameFormatter: prefixo aplicado por tipo.
        Prefixar<IniciarExecucao>(cfg, prefixo);
        Prefixar<ExecucaoEnfileirada>(cfg, prefixo);
        Prefixar<ExecucaoRecusada>(cfg, prefixo);
        Prefixar<StatusExecucaoAlterado>(cfg, prefixo);
        Prefixar<ExecucaoFinalizada>(cfg, prefixo);

        cfg.ReceiveEndpoint(NomesMensageria.FilaComandos(prefixo), e =>
        {
            // O comando chega por send direto na fila; sem assinatura de topico.
            e.ConfigureConsumeTopology = false;
            // Fault<T> viraria topico nomeado pelo tipo .NET (fora do IAM *-oficina-*); falha definitiva vai para a _error.
            // No 8.5.12 PublishFaults so existe no receive endpoint (nao no configurador do bus).
            e.PublishFaults = false;
            e.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
            e.ConfigureConsumer<IniciarExecucaoConsumer>(ctx);
        });
    }

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex PrefixoValido();

    private static void Prefixar<T>(IBusFactoryConfigurator cfg, string prefixo) where T : class =>
        cfg.Message<T>(m => m.SetEntityName(NomesMensageria.Topico(prefixo, typeof(T).Name)));
}
