using Microsoft.Extensions.Options;
using Oficina.Execucao.Aplicacao.Execucoes;
using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Aplicacao.Testes.Execucoes;

internal sealed class RelogioFixo : IRelogio
{
    public static readonly DateTime T0 = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    public DateTime AgoraUtc { get; set; } = T0;
}

internal static class Fabrica
{
    public static ExecucaoOs EmStatus(Guid osId, StatusExecucao status, string unidade = "SP-01", string? motivo = null,
        DateTime? diagnostico = null, DateTime? reparo = null) =>
        ExecucaoOs.Reconstituir(osId, 10, unidade, [], [], status, RelogioFixo.T0.AddMinutes(-30),
            diagnostico, reparo, null, null, motivo, 1);

    public static IOptions<OpcoesExecucao> Opcoes(int capacidade) =>
        Options.Create(new OpcoesExecucao { CapacidadeMaximaPorUnidade = capacidade });
}
