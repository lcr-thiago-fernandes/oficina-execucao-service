using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Oficina.Execucao.Api.Testes;

/// <summary>
/// Assina tokens localmente nos testes. Em producao quem emite e a Lambda oficina-auth;
/// o servico apenas valida (iss=oficina-auth, aud=oficina-api, HS256, claim "perfil").
/// </summary>
public static class GeradorTokenDeTeste
{
    public const string Secret = "chave-de-teste-com-mais-de-32-caracteres-para-hs256";
    public const string Issuer = "oficina-auth";
    public const string Audience = "oficina-api";

    public static string Gerar(string perfil, string audiencia = Audience)
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString()),
            new("perfil", perfil),
            new("nome", "Teste"),
            new("jti", Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: audiencia,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
