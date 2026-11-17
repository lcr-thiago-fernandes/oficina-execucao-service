using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Execucao.Aplicacao.Execucoes;

namespace Oficina.Execucao.Aplicacao;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarAplicacao(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpcoesExecucao>()
            .Bind(configuration.GetSection(OpcoesExecucao.Secao))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<EnfileirarExecucaoHandler>();
        services.AddScoped<AvancarExecucaoHandler>();
        services.AddScoped<ConsultarExecucoesHandler>();
        return services;
    }
}
