using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Oficina.Execucao.Api.Configuracao;

public static class ConfiguracaoJwt
{
    public static IServiceCollection AdicionarJwtBearer(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.Secao).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Secao 'Jwt' ausente.");

        if (string.IsNullOrWhiteSpace(jwt.Secret) || jwt.Secret.Length < 32)
            throw new InvalidOperationException("Jwt.Secret precisa ter pelo menos 32 caracteres.");

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // TLS termina no gateway/NLB

                // Le as claims exatamente como a Lambda as emitiu ("perfil" intacto).
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = chave,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        return services;
    }
}
