namespace Oficina.Execucao.Dominio.Execucoes;

public sealed record ItemExecucao
{
    public string Descricao { get; }
    public int Quantidade { get; }

    public ItemExecucao(string descricao, int quantidade)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ExecucaoInvalidaException("Descricao do item e obrigatoria.");
        if (quantidade <= 0)
            throw new ExecucaoInvalidaException("Quantidade do item deve ser maior que zero.");
        Descricao = descricao;
        Quantidade = quantidade;
    }
}
