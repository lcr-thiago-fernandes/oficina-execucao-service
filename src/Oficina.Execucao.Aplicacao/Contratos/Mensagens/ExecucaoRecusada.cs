using MassTransit;

namespace Oficina.Execucao.Aplicacao.Contratos.Mensagens;

[MessageUrn("oficina:saga:ExecucaoRecusada")]
[EntityName("oficina-saga-ExecucaoRecusada")]
public sealed record ExecucaoRecusada(Guid OrdemServicoId, DateTime OcorridoEm, string Motivo);
