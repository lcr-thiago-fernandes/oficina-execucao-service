namespace Oficina.Execucao.Aplicacao.Execucoes;

/// <summary>Execucao inexistente para a OS informada (HTTP 404).</summary>
public sealed class ExecucaoNaoEncontradaException : Exception
{
    public Guid OrdemServicoId { get; }

    public ExecucaoNaoEncontradaException(Guid ordemServicoId)
        : base($"Execucao da ordem de servico {ordemServicoId} nao encontrada.")
    {
        OrdemServicoId = ordemServicoId;
    }
}

/// <summary>Atualizacao concorrente da mesma execucao (HTTP 409).</summary>
public sealed class ConflitoDeConcorrenciaException : Exception
{
    public ConflitoDeConcorrenciaException(string mensagem) : base(mensagem) { }

    public ConflitoDeConcorrenciaException(string mensagem, Exception interna) : base(mensagem, interna) { }
}
