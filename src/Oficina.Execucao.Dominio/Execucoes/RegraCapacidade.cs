namespace Oficina.Execucao.Dominio.Execucoes;

public static class RegraCapacidade
{
    public static bool HaVaga(int ativas, int capacidadeMaxima) => ativas < capacidadeMaxima;

    public static string MotivoCapacidadeEsgotada(string unidade, int capacidadeMaxima) =>
        $"Capacidade da unidade {unidade} esgotada ({capacidadeMaxima}/{capacidadeMaxima})";
}
