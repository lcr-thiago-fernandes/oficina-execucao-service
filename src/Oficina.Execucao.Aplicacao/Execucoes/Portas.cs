using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Aplicacao.Execucoes;

public enum ResultadoGravacao { Gravada, JaExistente, CapacidadeEsgotada }

public interface IRepositorioExecucao
{
    /// <summary>Grava a execucao ocupando uma vaga da unidade. Gravada | JaExistente | CapacidadeEsgotada.</summary>
    Task<ResultadoGravacao> InserirNaFilaAsync(ExecucaoOs execucao, int capacidadeMaxima, CancellationToken ct);

    /// <summary>Grava a execucao recusada sem ocupar vaga. Gravada | JaExistente.</summary>
    Task<ResultadoGravacao> InserirRecusadaAsync(ExecucaoOs execucao, CancellationToken ct);

    Task<ExecucaoOs?> ObterAsync(Guid ordemServicoId, CancellationToken ct);

    /// <summary>Condicao versao == execucao.Versao - 1; Finalizada libera a vaga; conflito lanca <see cref="ConflitoDeConcorrenciaException"/>.</summary>
    Task AtualizarAsync(ExecucaoOs execucao, CancellationToken ct);

    /// <summary>Ordem de EnfileiradaEm crescente.</summary>
    Task<IReadOnlyList<ExecucaoOs>> ListarPorStatusAsync(StatusExecucao status, CancellationToken ct);

    /// <summary>Posicao 1-based entre as NaFila da mesma unidade.</summary>
    Task<int> ObterPosicaoNaFilaAsync(ExecucaoOs execucao, CancellationToken ct);
}

public interface IPublicadorMensagens
{
    Task PublicarAsync<T>(T mensagem, CancellationToken ct) where T : class;
}

public interface IRelogio
{
    DateTime AgoraUtc { get; }
}

/// <param name="Resultado">"Sucesso" | "Recusada" | "Duplicada"</param>
public sealed record EventoExecucao(Guid OrdemServicoId, int NumeroOs, string Unidade, string? StatusAnterior, string StatusNovo, string Resultado, int? DuracaoMinutos);

public interface ITelemetriaExecucao
{
    void Registrar(EventoExecucao evento);
}
