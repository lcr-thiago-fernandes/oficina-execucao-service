namespace Oficina.Execucao.Dominio.Execucoes;

public sealed class ExecucaoInvalidaException : ArgumentException
{
    public ExecucaoInvalidaException(string mensagem) : base(mensagem) { }
}

public sealed class TransicaoExecucaoInvalidaException : InvalidOperationException
{
    public StatusExecucao Atual { get; }
    public StatusExecucao Destino { get; }

    public TransicaoExecucaoInvalidaException(StatusExecucao atual, StatusExecucao destino)
        : base($"Transicao invalida de {atual} para {destino}.")
    {
        Atual = atual;
        Destino = destino;
    }
}
