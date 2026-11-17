using MassTransit;
using Microsoft.Extensions.Logging;
using Oficina.Execucao.Aplicacao.Contratos.Mensagens;
using Oficina.Execucao.Aplicacao.Execucoes;

namespace Oficina.Execucao.Infraestrutura.Mensageria;

/// <summary>Recebe o comando da saga e delega ao caso de uso (idempotente: entrega repetida republica o desfecho gravado).</summary>
public sealed class IniciarExecucaoConsumer : IConsumer<IniciarExecucao>
{
    private readonly EnfileirarExecucaoHandler _handler;
    private readonly ILogger<IniciarExecucaoConsumer> _logger;

    public IniciarExecucaoConsumer(EnfileirarExecucaoHandler handler, ILogger<IniciarExecucaoConsumer> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IniciarExecucao> context)
    {
        var msg = context.Message;
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ordemServicoId"] = msg.OrdemServicoId,
            ["numeroOs"] = msg.NumeroOs
        });

        await _handler.ExecutarAsync(msg, context.CancellationToken);
    }
}
