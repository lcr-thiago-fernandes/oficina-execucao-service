using Microsoft.AspNetCore.Mvc;
using Oficina.Execucao.Aplicacao.Execucoes;
using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Api.Configuracao;

public class MiddlewareDeExcecoes
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MiddlewareDeExcecoes> _log;

    public MiddlewareDeExcecoes(RequestDelegate next, ILogger<MiddlewareDeExcecoes> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (status, problema) = MapearExcecao(ex);
            if (status >= 500)
                _log.LogError(ex, "Erro nao mapeado: {Mensagem}", ex.Message);
            else
                _log.LogWarning(ex, "Falha de dominio: {Titulo}", problema.Title);

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problema, context.RequestAborted);
        }
    }

    private static (int, ProblemDetails) MapearExcecao(Exception ex) => ex switch
    {
        ExecucaoNaoEncontradaException
            => (StatusCodes.Status404NotFound,
                new ProblemDetails { Title = "Nao encontrado", Detail = ex.Message, Status = 404 }),

        ConflitoDeConcorrenciaException
            => (StatusCodes.Status409Conflict,
                new ProblemDetails
                {
                    Title = "Conflito de concorrencia",
                    Detail = "O recurso foi alterado por outra operacao. Tente novamente.",
                    Status = 409
                }),

        TransicaoExecucaoInvalidaException or ExecucaoInvalidaException
            => (StatusCodes.Status422UnprocessableEntity,
                new ProblemDetails { Title = "Operacao invalida", Detail = ex.Message, Status = 422 }),

        _ => (StatusCodes.Status500InternalServerError,
              new ProblemDetails { Title = "Erro interno", Detail = "Erro inesperado ao processar a requisicao.", Status = 500 })
    };
}
