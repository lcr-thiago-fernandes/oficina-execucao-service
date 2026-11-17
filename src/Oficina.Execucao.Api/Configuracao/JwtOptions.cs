namespace Oficina.Execucao.Api.Configuracao;

/// <summary>
/// Parametros de VALIDACAO do token. O servico nao emite JWT: quem emite e a Lambda oficina-auth.
/// Os defaults refletem o contrato compartilhado (iss=oficina-auth, aud=oficina-api, HS256).
/// </summary>
public sealed class JwtOptions
{
    public const string Secao = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "oficina-auth";
    public string Audience { get; set; } = "oficina-api";
}
