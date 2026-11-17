using MassTransit;

namespace Oficina.Execucao.Aplicacao.Contratos.Mensagens;

[MessageUrn("oficina:saga:ExecucaoFinalizada")]
[EntityName("oficina-saga-ExecucaoFinalizada")]
public sealed record ExecucaoFinalizada(Guid OrdemServicoId, DateTime OcorridoEm, string? Observacao, int DuracaoMinutos);
