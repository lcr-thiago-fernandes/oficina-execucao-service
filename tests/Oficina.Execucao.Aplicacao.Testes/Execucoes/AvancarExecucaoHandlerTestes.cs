using FluentAssertions;
using Moq;
using Oficina.Execucao.Aplicacao.Contratos.Mensagens;
using Oficina.Execucao.Aplicacao.Execucoes;
using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Aplicacao.Testes.Execucoes;

public class AvancarExecucaoHandlerTestes
{
    private static readonly Guid OsId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime T0 = RelogioFixo.T0;
    private readonly CancellationToken ct = new CancellationTokenSource().Token;
    private readonly Mock<IRepositorioExecucao> repo = new();
    private readonly Mock<IPublicadorMensagens> publicador = new();
    private readonly Mock<ITelemetriaExecucao> telemetria = new();

    private AvancarExecucaoHandler Criar() => new(repo.Object, publicador.Object, new RelogioFixo(), telemetria.Object);

    private void Existente(ExecucaoOs e) => repo.Setup(r => r.ObterAsync(OsId, ct)).ReturnsAsync(e);

    [Fact]
    public async Task IniciarDiagnostico_NaFila_AtualizaEPublicaStatusEmDiagnostico()
    {
        Existente(Fabrica.EmStatus(OsId, StatusExecucao.NaFila));

        var r = await Criar().IniciarDiagnosticoAsync(OsId, "obs", ct);

        r.Status.Should().Be(StatusExecucao.EmDiagnostico);
        repo.Verify(x => x.AtualizarAsync(It.Is<ExecucaoOs>(e => e.Versao == 2), ct), Times.Once);
        publicador.Verify(p => p.PublicarAsync(It.Is<StatusExecucaoAlterado>(m =>
            m.StatusExecucao == StatusExecucaoMensagem.EmDiagnostico && m.Observacao == "obs" && m.OcorridoEm == T0), ct), Times.Once);
        telemetria.Verify(t => t.Registrar(new EventoExecucao(OsId, 10, "SP-01", "NaFila", "EmDiagnostico", "Sucesso", null)), Times.Once);
    }

    [Fact]
    public async Task IniciarDiagnostico_JaEmDiagnostico_NaoAtualizaMasRepublica()
    {
        Existente(Fabrica.EmStatus(OsId, StatusExecucao.EmDiagnostico, diagnostico: T0.AddMinutes(-5)));

        await Criar().IniciarDiagnosticoAsync(OsId, null, ct);

        repo.Verify(x => x.AtualizarAsync(It.IsAny<ExecucaoOs>(), It.IsAny<CancellationToken>()), Times.Never);
        publicador.Verify(p => p.PublicarAsync(It.IsAny<StatusExecucaoAlterado>(), ct), Times.Once);
        telemetria.Verify(t => t.Registrar(It.IsAny<EventoExecucao>()), Times.Never);
    }

    [Fact]
    public async Task IniciarReparo_EmDiagnostico_AtualizaEPublicaStatusEmReparo()
    {
        Existente(Fabrica.EmStatus(OsId, StatusExecucao.EmDiagnostico, diagnostico: T0.AddMinutes(-5)));

        var r = await Criar().IniciarReparoAsync(OsId, null, ct);

        r.Status.Should().Be(StatusExecucao.EmReparo);
        publicador.Verify(p => p.PublicarAsync(It.Is<StatusExecucaoAlterado>(m => m.StatusExecucao == StatusExecucaoMensagem.EmReparo), ct), Times.Once);
    }

    [Fact]
    public async Task Finalizar_EmReparo_PublicaExecucaoFinalizadaComDuracao()
    {
        Existente(Fabrica.EmStatus(OsId, StatusExecucao.EmReparo, diagnostico: T0.AddMinutes(-45), reparo: T0.AddMinutes(-20)));

        var r = await Criar().FinalizarAsync(OsId, "pronto", ct);

        r.Status.Should().Be(StatusExecucao.Finalizada);
        publicador.Verify(p => p.PublicarAsync(It.Is<ExecucaoFinalizada>(m =>
            m.DuracaoMinutos == 45 && m.Observacao == "pronto" && m.OrdemServicoId == OsId), ct), Times.Once);
        telemetria.Verify(t => t.Registrar(new EventoExecucao(OsId, 10, "SP-01", "EmReparo", "Finalizada", "Sucesso", 45)), Times.Once);
    }

    [Fact]
    public async Task Avancar_OsInexistente_LancaExecucaoNaoEncontrada()
    {
        repo.Setup(r => r.ObterAsync(OsId, ct)).ReturnsAsync((ExecucaoOs?)null);

        var acao = () => Criar().FinalizarAsync(OsId, null, ct);

        await acao.Should().ThrowAsync<ExecucaoNaoEncontradaException>();
        publicador.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Avancar_TransicaoInvalida_PropagaENaoPublica()
    {
        Existente(Fabrica.EmStatus(OsId, StatusExecucao.NaFila));

        var acao = () => Criar().FinalizarAsync(OsId, null, ct);

        await acao.Should().ThrowAsync<TransicaoExecucaoInvalidaException>();
        publicador.VerifyNoOtherCalls();
        repo.Verify(x => x.AtualizarAsync(It.IsAny<ExecucaoOs>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Avancar_ConflitoNaAtualizacao_PropagaENaoPublica()
    {
        Existente(Fabrica.EmStatus(OsId, StatusExecucao.NaFila));
        repo.Setup(r => r.AtualizarAsync(It.IsAny<ExecucaoOs>(), ct)).ThrowsAsync(new ConflitoDeConcorrenciaException("x"));

        var acao = () => Criar().IniciarDiagnosticoAsync(OsId, null, ct);

        await acao.Should().ThrowAsync<ConflitoDeConcorrenciaException>();
        publicador.VerifyNoOtherCalls();
    }
}
