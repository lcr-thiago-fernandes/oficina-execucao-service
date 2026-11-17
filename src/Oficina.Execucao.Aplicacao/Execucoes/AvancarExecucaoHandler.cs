using Oficina.Execucao.Aplicacao.Contratos.Mensagens;
using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Aplicacao.Execucoes;

public sealed class AvancarExecucaoHandler
{
    private readonly IRepositorioExecucao _repositorio;
    private readonly IPublicadorMensagens _publicador;
    private readonly IRelogio _relogio;
    private readonly ITelemetriaExecucao _telemetria;

    public AvancarExecucaoHandler(IRepositorioExecucao repositorio, IPublicadorMensagens publicador,
        IRelogio relogio, ITelemetriaExecucao telemetria)
    {
        _repositorio = repositorio;
        _publicador = publicador;
        _relogio = relogio;
        _telemetria = telemetria;
    }

    public Task<ExecucaoOs> IniciarDiagnosticoAsync(Guid ordemServicoId, string? observacao, CancellationToken ct) =>
        AvancarAsync(ordemServicoId, (e, agora) => e.IniciarDiagnostico(observacao, agora),
            e => new StatusExecucaoAlterado(e.OrdemServicoId, _relogio.AgoraUtc, StatusExecucaoMensagem.EmDiagnostico, e.Observacao), ct);

    public Task<ExecucaoOs> IniciarReparoAsync(Guid ordemServicoId, string? observacao, CancellationToken ct) =>
        AvancarAsync(ordemServicoId, (e, agora) => e.IniciarReparo(observacao, agora),
            e => new StatusExecucaoAlterado(e.OrdemServicoId, _relogio.AgoraUtc, StatusExecucaoMensagem.EmReparo, e.Observacao), ct);

    public Task<ExecucaoOs> FinalizarAsync(Guid ordemServicoId, string? observacao, CancellationToken ct) =>
        AvancarAsync(ordemServicoId, (e, agora) => e.Finalizar(observacao, agora),
            e => new ExecucaoFinalizada(e.OrdemServicoId, _relogio.AgoraUtc, e.Observacao, e.DuracaoMinutos!.Value), ct);

    private async Task<ExecucaoOs> AvancarAsync<T>(Guid ordemServicoId, Func<ExecucaoOs, DateTime, bool> transicao,
        Func<ExecucaoOs, T> criarMensagem, CancellationToken ct) where T : class
    {
        var execucao = await _repositorio.ObterAsync(ordemServicoId, ct)
            ?? throw new ExecucaoNaoEncontradaException(ordemServicoId);

        var statusAnterior = execucao.Status.ToString();
        if (transicao(execucao, _relogio.AgoraUtc))
        {
            await _repositorio.AtualizarAsync(execucao, ct);
            _telemetria.Registrar(new EventoExecucao(execucao.OrdemServicoId, execucao.NumeroOs, execucao.Unidade,
                statusAnterior, execucao.Status.ToString(), "Sucesso", execucao.DuracaoMinutos));
        }

        // Publica sempre: um POST repetido apos falha de publicacao precisa reemitir o evento.
        await _publicador.PublicarAsync(criarMensagem(execucao), ct);
        return execucao;
    }
}
