using Microsoft.Extensions.Options;
using Oficina.Execucao.Aplicacao.Contratos.Mensagens;
using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Aplicacao.Execucoes;

public sealed record ResultadoEnfileiramento(bool Enfileirada, int? PosicaoFila, string? Motivo);

public sealed class EnfileirarExecucaoHandler
{
    private readonly IRepositorioExecucao _repositorio;
    private readonly IPublicadorMensagens _publicador;
    private readonly IRelogio _relogio;
    private readonly ITelemetriaExecucao _telemetria;
    private readonly IOptions<OpcoesExecucao> _opcoes;

    public EnfileirarExecucaoHandler(IRepositorioExecucao repositorio, IPublicadorMensagens publicador,
        IRelogio relogio, ITelemetriaExecucao telemetria, IOptions<OpcoesExecucao> opcoes)
    {
        _repositorio = repositorio;
        _publicador = publicador;
        _relogio = relogio;
        _telemetria = telemetria;
        _opcoes = opcoes;
    }

    public async Task<ResultadoEnfileiramento> ExecutarAsync(IniciarExecucao comando, CancellationToken ct)
    {
        if (comando.OrdemServicoId == Guid.Empty)
            throw new ArgumentException("Identificador da ordem de servico e obrigatorio.", nameof(comando));

        var existente = await _repositorio.ObterAsync(comando.OrdemServicoId, ct);
        if (existente is not null)
            return await RepublicarAsync(existente, ct);

        var capacidade = _opcoes.Value.CapacidadeMaximaPorUnidade;

        ExecucaoOs execucao;
        try
        {
            var servicos = (comando.Servicos ?? []).Select(Mapear).ToList();
            var pecas = (comando.Pecas ?? []).Select(Mapear).ToList();
            execucao = ExecucaoOs.Enfileirar(comando.OrdemServicoId, comando.NumeroOs, comando.Unidade,
                servicos, pecas, _relogio.AgoraUtc);
        }
        catch (ExecucaoInvalidaException ex)
        {
            return await RecusarAsync(comando, $"Comando IniciarExecucao inválido: {ex.Message}", ct);
        }

        if (!RegraCapacidade.HaVaga(0, capacidade))
            return await RecusarAsync(comando, RegraCapacidade.MotivoCapacidadeEsgotada(execucao.Unidade, capacidade), ct);

        switch (await _repositorio.InserirNaFilaAsync(execucao, capacidade, ct))
        {
            case ResultadoGravacao.Gravada:
                var posicao = await _repositorio.ObterPosicaoNaFilaAsync(execucao, ct);
                await _publicador.PublicarAsync(
                    new ExecucaoEnfileirada(execucao.OrdemServicoId, _relogio.AgoraUtc, posicao), ct);
                _telemetria.Registrar(new EventoExecucao(execucao.OrdemServicoId, execucao.NumeroOs,
                    execucao.Unidade, null, StatusExecucao.NaFila.ToString(), "Sucesso", null));
                return new ResultadoEnfileiramento(true, posicao, null);

            case ResultadoGravacao.JaExistente:
                return await RepublicarGravadaAsync(comando.OrdemServicoId, ct);

            default:
                return await RecusarAsync(comando, RegraCapacidade.MotivoCapacidadeEsgotada(execucao.Unidade, capacidade), ct);
        }
    }

    private static ItemExecucao Mapear(ItemExecucaoMensagem? item) =>
        item is null
            ? throw new ExecucaoInvalidaException("Item nulo na lista de servicos ou pecas.")
            : new ItemExecucao(item.Descricao, item.Quantidade);

    private async Task<ResultadoEnfileiramento> RecusarAsync(IniciarExecucao comando, string motivo, CancellationToken ct)
    {
        var recusada = ExecucaoOs.Recusar(comando.OrdemServicoId, comando.NumeroOs, comando.Unidade, motivo, _relogio.AgoraUtc);

        if (await _repositorio.InserirRecusadaAsync(recusada, ct) == ResultadoGravacao.JaExistente)
            return await RepublicarGravadaAsync(comando.OrdemServicoId, ct);

        await _publicador.PublicarAsync(new ExecucaoRecusada(recusada.OrdemServicoId, _relogio.AgoraUtc, motivo), ct);
        _telemetria.Registrar(new EventoExecucao(recusada.OrdemServicoId, recusada.NumeroOs, recusada.Unidade,
            null, StatusExecucao.Recusada.ToString(), "Recusada", null));
        return new ResultadoEnfileiramento(false, null, motivo);
    }

    private async Task<ResultadoEnfileiramento> RepublicarGravadaAsync(Guid ordemServicoId, CancellationToken ct)
    {
        var existente = await _repositorio.ObterAsync(ordemServicoId, ct)
            ?? throw new InvalidOperationException(
                $"Execucao {ordemServicoId} reportada como existente, mas nao encontrada.");
        return await RepublicarAsync(existente, ct);
    }

    private async Task<ResultadoEnfileiramento> RepublicarAsync(ExecucaoOs existente, CancellationToken ct)
    {
        ResultadoEnfileiramento resultado;
        if (existente.Status == StatusExecucao.Recusada)
        {
            var motivo = existente.MotivoRecusa ?? string.Empty;
            await _publicador.PublicarAsync(new ExecucaoRecusada(existente.OrdemServicoId, _relogio.AgoraUtc, motivo), ct);
            resultado = new ResultadoEnfileiramento(false, null, motivo);
        }
        else
        {
            var posicao = existente.Status == StatusExecucao.NaFila
                ? await _repositorio.ObterPosicaoNaFilaAsync(existente, ct)
                : 0;
            await _publicador.PublicarAsync(new ExecucaoEnfileirada(existente.OrdemServicoId, _relogio.AgoraUtc, posicao), ct);
            resultado = new ResultadoEnfileiramento(true, posicao, null);
        }

        _telemetria.Registrar(new EventoExecucao(existente.OrdemServicoId, existente.NumeroOs, existente.Unidade,
            null, existente.Status.ToString(), "Duplicada", null));
        return resultado;
    }
}
