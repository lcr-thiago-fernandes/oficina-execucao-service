using MassTransit;

namespace Oficina.Execucao.Aplicacao.Contratos.Mensagens;

[MessageUrn("oficina:saga:ExecucaoEnfileirada")]
[EntityName("oficina-saga-ExecucaoEnfileirada")]
public sealed record ExecucaoEnfileirada(Guid OrdemServicoId, DateTime OcorridoEm, int PosicaoFila);
