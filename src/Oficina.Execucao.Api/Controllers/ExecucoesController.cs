using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Execucao.Api.Configuracao;
using Oficina.Execucao.Api.Modelos;
using Oficina.Execucao.Aplicacao.Execucoes;
using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Api.Controllers;

[ApiController]
[Route("api/v1/execucoes")]
[Authorize(Policy = PoliticasDeAutorizacao.RequerAdminOuAtendente)]
public class ExecucoesController : ControllerBase
{
    private readonly AvancarExecucaoHandler _avancar;
    private readonly ConsultarExecucoesHandler _consultar;

    public ExecucoesController(AvancarExecucaoHandler avancar, ConsultarExecucoesHandler consultar)
    {
        _avancar = avancar;
        _consultar = consultar;
    }

    /// <summary>Lista execucoes; sem status, somente as ativas (NaFila, EmDiagnostico, EmReparo).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExecucaoResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] string? status, CancellationToken ct)
    {
        StatusExecucao? filtro = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<StatusExecucao>(status, ignoreCase: true, out var parseado) || !Enum.IsDefined(parseado))
                return BadRequest(new ProblemDetails
                {
                    Title = "Status invalido",
                    Detail = $"Valores aceitos: {string.Join(", ", Enum.GetNames<StatusExecucao>())}.",
                    Status = 400
                });
            filtro = parseado;
        }

        var execucoes = await _consultar.ListarAsync(filtro, ct);
        return Ok(execucoes.Select(ExecucaoResponse.De).ToList());
    }

    [HttpGet("{osId:guid}")]
    [ProducesResponseType(typeof(ExecucaoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obter(Guid osId, CancellationToken ct)
    {
        var execucao = await _consultar.ObterAsync(osId, ct) ?? throw new ExecucaoNaoEncontradaException(osId);
        return Ok(ExecucaoResponse.De(execucao));
    }

    [HttpPost("{osId:guid}/diagnostico")]
    [ProducesResponseType(typeof(ExecucaoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> IniciarDiagnostico(Guid osId, [FromBody] ObservacaoRequest? corpo, CancellationToken ct) =>
        Ok(ExecucaoResponse.De(await _avancar.IniciarDiagnosticoAsync(osId, corpo?.Observacao, ct)));

    [HttpPost("{osId:guid}/reparo")]
    [ProducesResponseType(typeof(ExecucaoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> IniciarReparo(Guid osId, [FromBody] ObservacaoRequest? corpo, CancellationToken ct) =>
        Ok(ExecucaoResponse.De(await _avancar.IniciarReparoAsync(osId, corpo?.Observacao, ct)));

    [HttpPost("{osId:guid}/finalizacao")]
    [ProducesResponseType(typeof(ExecucaoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Finalizar(Guid osId, [FromBody] ObservacaoRequest? corpo, CancellationToken ct) =>
        Ok(ExecucaoResponse.De(await _avancar.FinalizarAsync(osId, corpo?.Observacao, ct)));
}
