namespace Oficina.Execucao.Api.Configuracao;

public static class PoliticasDeAutorizacao
{
    public const string RequerAdminOuAtendente = "RequerAdminOuAtendente";

    public static IServiceCollection AdicionarPoliticas(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(RequerAdminOuAtendente, p =>
                p.RequireAuthenticatedUser().RequireClaim("perfil", "Admin", "Atendente"));
        });
        return services;
    }
}
