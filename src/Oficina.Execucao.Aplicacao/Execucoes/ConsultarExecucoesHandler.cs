using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Aplicacao.Execucoes;

public sealed class ConsultarExecucoesHandler
{
    private static readonly StatusExecucao[] StatusAtivos =
        [StatusExecucao.NaFila, StatusExecucao.EmDiagnostico, StatusExecucao.EmReparo];

    private readonly IRepositorioExecucao _repositorio;

    public ConsultarExecucoesHandler(IRepositorioExecucao repositorio) => _repositorio = repositorio;

    public Task<ExecucaoOs?> ObterAsync(Guid ordemServicoId, CancellationToken ct) =>
        _repositorio.ObterAsync(ordemServicoId, ct);

    public async Task<IReadOnlyList<ExecucaoOs>> ListarAsync(StatusExecucao? status, CancellationToken ct)
    {
        if (status is { } unico)
            return await _repositorio.ListarPorStatusAsync(unico, ct);

        var todas = new List<ExecucaoOs>();
        foreach (var s in StatusAtivos)
            todas.AddRange(await _repositorio.ListarPorStatusAsync(s, ct));
        return todas.OrderBy(e => e.EnfileiradaEm).ToList();
    }
}
