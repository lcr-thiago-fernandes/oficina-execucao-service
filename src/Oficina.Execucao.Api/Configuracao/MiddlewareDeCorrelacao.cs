using Serilog.Context;

namespace Oficina.Execucao.Api.Configuracao;

/// <summary>
/// Costura uma requisicao ponta a ponta com o X-Correlation-Id propagado pelo API Gateway e pela Lambda de
/// autenticacao. Quando a rota traz <c>osId</c>, o identificador da OS tambem vai para os logs e para o New Relic.
/// </summary>
public class MiddlewareDeCorrelacao
{
    public const string Header = "X-Correlation-Id";

    private readonly RequestDelegate _proximo;

    public MiddlewareDeCorrelacao(RequestDelegate proximo) => _proximo = proximo;

    public async Task InvokeAsync(HttpContext contexto)
    {
        var correlationId = ObterOuGerar(contexto);

        contexto.Items[Header] = correlationId;
        contexto.Response.Headers[Header] = correlationId;

        using (LogContext.PushProperty("correlationId", correlationId))
        {
            if (contexto.GetRouteValue("osId") is { } osId)
            {
                using (LogContext.PushProperty("ordemServicoId", osId))
                {
                    RegistrarNoNewRelic(osId.ToString()!);
                    await _proximo(contexto);
                }
            }
            else
            {
                await _proximo(contexto);
            }
        }
    }

    private static void RegistrarNoNewRelic(string osId)
    {
        try
        {
            NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("ordemServicoId", osId);
        }
        catch
        {
            // Telemetria nunca derruba a requisicao.
        }
    }

    private static string ObterOuGerar(HttpContext contexto)
    {
        // 1) header explicito do cliente; 2) requestId do API Gateway; 3) novo.
        if (contexto.Request.Headers.TryGetValue(Header, out var doCliente) &&
            !string.IsNullOrWhiteSpace(doCliente))
            return doCliente.ToString();

        if (contexto.Request.Headers.TryGetValue("X-Amzn-RequestId", out var doGateway) &&
            !string.IsNullOrWhiteSpace(doGateway))
            return doGateway.ToString();

        return Guid.NewGuid().ToString();
    }
}
