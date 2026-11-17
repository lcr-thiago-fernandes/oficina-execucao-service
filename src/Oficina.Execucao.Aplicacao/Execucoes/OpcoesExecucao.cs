using System.ComponentModel.DataAnnotations;

namespace Oficina.Execucao.Aplicacao.Execucoes;

public sealed class OpcoesExecucao
{
    public const string Secao = "Execucao";

    [Range(0, int.MaxValue)]
    public int CapacidadeMaximaPorUnidade { get; set; } = 20;
}
