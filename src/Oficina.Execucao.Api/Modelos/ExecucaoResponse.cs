using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Api.Modelos;

public sealed record ItemResponse(string Descricao, int Quantidade);

public sealed record ExecucaoResponse(
    Guid OrdemServicoId,
    int NumeroOs,
    string Unidade,
    string Status,
    DateTime EnfileiradaEm,
    DateTime? DiagnosticoIniciadoEm,
    DateTime? ReparoIniciadoEm,
    DateTime? FinalizadaEm,
    string? Observacao,
    string? MotivoRecusa,
    int? DuracaoMinutos,
    IReadOnlyList<ItemResponse> Servicos,
    IReadOnlyList<ItemResponse> Pecas)
{
    public static ExecucaoResponse De(ExecucaoOs e) => new(
        e.OrdemServicoId, e.NumeroOs, e.Unidade, e.Status.ToString(), e.EnfileiradaEm,
        e.DiagnosticoIniciadoEm, e.ReparoIniciadoEm, e.FinalizadaEm, e.Observacao, e.MotivoRecusa,
        e.DuracaoMinutos,
        e.Servicos.Select(i => new ItemResponse(i.Descricao, i.Quantidade)).ToList(),
        e.Pecas.Select(i => new ItemResponse(i.Descricao, i.Quantidade)).ToList());
}
