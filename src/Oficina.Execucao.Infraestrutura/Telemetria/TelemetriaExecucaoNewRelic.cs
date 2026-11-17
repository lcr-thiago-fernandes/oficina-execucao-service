using Microsoft.Extensions.Logging;
using Oficina.Execucao.Aplicacao.Execucoes;

namespace Oficina.Execucao.Infraestrutura.Telemetria;

/// <summary>Evento customizado <c>ExecucaoEvento</c> no New Relic. Sem agente carregado a API e no-op; telemetria nunca derruba o fluxo.</summary>
public sealed class TelemetriaExecucaoNewRelic : ITelemetriaExecucao
{
    public const string NomeEvento = "ExecucaoEvento";

    private readonly ILogger<TelemetriaExecucaoNewRelic> _logger;

    public TelemetriaExecucaoNewRelic(ILogger<TelemetriaExecucaoNewRelic> logger) => _logger = logger;

    public void Registrar(EventoExecucao evento)
    {
        try
        {
            NewRelic.Api.Agent.NewRelic.RecordCustomEvent(NomeEvento, MontarAtributos(evento));
            NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction
                .AddCustomAttribute("ordemServicoId", evento.OrdemServicoId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao registrar {Evento} no New Relic para a OS {OrdemServicoId}",
                NomeEvento, evento.OrdemServicoId);
        }
    }

    public static Dictionary<string, object> MontarAtributos(EventoExecucao evento)
    {
        var atributos = new Dictionary<string, object>
        {
            ["ordemServicoId"] = evento.OrdemServicoId.ToString(),
            ["numeroOs"] = evento.NumeroOs,
            ["unidade"] = evento.Unidade,
            ["statusAnterior"] = evento.StatusAnterior ?? "(inicial)",
            ["statusNovo"] = evento.StatusNovo,
            ["resultado"] = evento.Resultado
        };
        if (evento.DuracaoMinutos is { } duracao)
            atributos["duracaoMinutos"] = duracao;
        return atributos;
    }
}
