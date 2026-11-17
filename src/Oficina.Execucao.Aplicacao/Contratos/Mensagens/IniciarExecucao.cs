using MassTransit;

namespace Oficina.Execucao.Aplicacao.Contratos.Mensagens;

[MessageUrn("oficina:saga:IniciarExecucao")]
[EntityName("oficina-saga-IniciarExecucao")]
public sealed record IniciarExecucao(Guid OrdemServicoId, DateTime OcorridoEm, int NumeroOs, string Unidade, IReadOnlyList<ItemExecucaoMensagem> Servicos, IReadOnlyList<ItemExecucaoMensagem> Pecas);
