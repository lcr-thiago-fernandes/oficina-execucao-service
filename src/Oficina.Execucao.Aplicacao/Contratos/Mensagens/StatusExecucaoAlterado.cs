using MassTransit;

namespace Oficina.Execucao.Aplicacao.Contratos.Mensagens;

[MessageUrn("oficina:saga:StatusExecucaoAlterado")]
[EntityName("oficina-saga-StatusExecucaoAlterado")]
public sealed record StatusExecucaoAlterado(Guid OrdemServicoId, DateTime OcorridoEm, StatusExecucaoMensagem StatusExecucao, string? Observacao);
