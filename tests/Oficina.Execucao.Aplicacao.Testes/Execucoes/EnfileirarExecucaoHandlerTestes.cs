using FluentAssertions;
using Moq;
using Oficina.Execucao.Aplicacao.Contratos.Mensagens;
using Oficina.Execucao.Aplicacao.Execucoes;
using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Aplicacao.Testes.Execucoes;

public class EnfileirarExecucaoHandlerTestes
{
    private static readonly Guid OsId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime T0 = RelogioFixo.T0;
    private readonly CancellationToken ct = new CancellationTokenSource().Token;
    private readonly Mock<IRepositorioExecucao> repo = new(MockBehavior.Strict);
    private readonly Mock<IPublicadorMensagens> publicador = new();
    private readonly Mock<ITelemetriaExecucao> telemetria = new();

    private EnfileirarExecucaoHandler Criar(int capacidade = 20) =>
        new(repo.Object, publicador.Object, new RelogioFixo(), telemetria.Object, Fabrica.Opcoes(capacidade));

    private static IniciarExecucao Comando(string unidade = "SP-01", IReadOnlyList<ItemExecucaoMensagem>? servicos = null,
        IReadOnlyList<ItemExecucaoMensagem>? pecas = null, Guid? osId = null) =>
        new(osId ?? OsId, T0, 10, unidade, servicos ?? [new("Troca de oleo", 1)], pecas ?? [new("Filtro", 2)]);

    private void SemExistente() => repo.Setup(r => r.ObterAsync(OsId, ct)).ReturnsAsync((ExecucaoOs?)null);

    [Fact]
    public async Task Executar_ComVaga_GravaAntesDePublicarEnfileirada()
    {
        SemExistente();
        var seq = new List<string>();
        repo.Setup(r => r.InserirNaFilaAsync(It.IsAny<ExecucaoOs>(), 20, ct))
            .Callback(() => seq.Add("gravar")).ReturnsAsync(ResultadoGravacao.Gravada);
        repo.Setup(r => r.ObterPosicaoNaFilaAsync(It.IsAny<ExecucaoOs>(), ct)).ReturnsAsync(3);
        publicador.Setup(p => p.PublicarAsync(It.IsAny<ExecucaoEnfileirada>(), ct))
            .Callback(() => seq.Add("publicar")).Returns(Task.CompletedTask);

        var r = await Criar().ExecutarAsync(Comando(), ct);

        r.Should().Be(new ResultadoEnfileiramento(true, 3, null));
        seq.Should().Equal("gravar", "publicar");
        publicador.Verify(p => p.PublicarAsync(It.Is<ExecucaoEnfileirada>(m =>
            m.PosicaoFila == 3 && m.OrdemServicoId == OsId && m.OcorridoEm == T0), ct), Times.Once);
    }

    [Fact]
    public async Task Executar_CapacidadeEsgotada_GravaRecusadaEPublicaRecusaComMotivoDoContrato()
    {
        SemExistente();
        repo.Setup(r => r.InserirNaFilaAsync(It.IsAny<ExecucaoOs>(), 20, ct)).ReturnsAsync(ResultadoGravacao.CapacidadeEsgotada);
        repo.Setup(r => r.InserirRecusadaAsync(It.Is<ExecucaoOs>(e =>
            e.Status == StatusExecucao.Recusada && e.MotivoRecusa == "Capacidade da unidade SP-01 esgotada (20/20)"), ct))
            .ReturnsAsync(ResultadoGravacao.Gravada);

        var r = await Criar().ExecutarAsync(Comando(), ct);

        r.Should().Be(new ResultadoEnfileiramento(false, null, "Capacidade da unidade SP-01 esgotada (20/20)"));
        publicador.Verify(p => p.PublicarAsync(It.Is<ExecucaoRecusada>(m =>
            m.Motivo == "Capacidade da unidade SP-01 esgotada (20/20)" && m.OcorridoEm == T0), ct), Times.Once);
        telemetria.Verify(t => t.Registrar(It.Is<EventoExecucao>(e => e.Resultado == "Recusada" && e.StatusNovo == "Recusada")), Times.Once);
    }

    [Fact]
    public async Task Executar_CapacidadeZero_RecusaSemTentarOcuparVaga()
    {
        SemExistente();
        repo.Setup(r => r.InserirRecusadaAsync(It.IsAny<ExecucaoOs>(), ct)).ReturnsAsync(ResultadoGravacao.Gravada);

        var r = await Criar(0).ExecutarAsync(Comando(), ct);

        r.Motivo.Should().Be("Capacidade da unidade SP-01 esgotada (0/0)");
        repo.Verify(x => x.InserirNaFilaAsync(It.IsAny<ExecucaoOs>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        publicador.Verify(p => p.PublicarAsync(It.Is<ExecucaoRecusada>(m => m.Motivo.EndsWith("(0/0)")), ct), Times.Once);
    }

    [Fact]
    public async Task Executar_Duplicado_JaNaFila_RepublicaEnfileiradaSemInserir()
    {
        repo.Setup(r => r.ObterAsync(OsId, ct)).ReturnsAsync(Fabrica.EmStatus(OsId, StatusExecucao.NaFila));
        repo.Setup(r => r.ObterPosicaoNaFilaAsync(It.IsAny<ExecucaoOs>(), ct)).ReturnsAsync(2);

        var r = await Criar().ExecutarAsync(Comando(), ct);

        r.Should().Be(new ResultadoEnfileiramento(true, 2, null));
        repo.Verify(x => x.InserirNaFilaAsync(It.IsAny<ExecucaoOs>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(x => x.InserirRecusadaAsync(It.IsAny<ExecucaoOs>(), It.IsAny<CancellationToken>()), Times.Never);
        publicador.Verify(p => p.PublicarAsync(It.Is<ExecucaoEnfileirada>(m => m.PosicaoFila == 2), ct), Times.Once);
        telemetria.Verify(t => t.Registrar(It.Is<EventoExecucao>(e => e.Resultado == "Duplicada")), Times.Once);
    }

    [Fact]
    public async Task Executar_Duplicado_JaRecusada_RepublicaRecusaMesmoComVagaLivre()
    {
        repo.Setup(r => r.ObterAsync(OsId, ct)).ReturnsAsync(Fabrica.EmStatus(OsId, StatusExecucao.Recusada, motivo: "motivo gravado"));

        var r = await Criar(20).ExecutarAsync(Comando(), ct);

        r.Should().Be(new ResultadoEnfileiramento(false, null, "motivo gravado"));
        repo.Verify(x => x.InserirNaFilaAsync(It.IsAny<ExecucaoOs>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        publicador.Verify(p => p.PublicarAsync(It.Is<ExecucaoRecusada>(m => m.Motivo == "motivo gravado"), ct), Times.Once);
        publicador.Verify(p => p.PublicarAsync(It.IsAny<ExecucaoEnfileirada>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Executar_Duplicado_JaEmReparo_RepublicaEnfileiradaPosicaoZero()
    {
        repo.Setup(r => r.ObterAsync(OsId, ct)).ReturnsAsync(Fabrica.EmStatus(OsId, StatusExecucao.EmReparo));

        var r = await Criar().ExecutarAsync(Comando(), ct);

        r.Should().Be(new ResultadoEnfileiramento(true, 0, null));
        publicador.Verify(p => p.PublicarAsync(It.Is<ExecucaoEnfileirada>(m => m.PosicaoFila == 0), ct), Times.Once);
        repo.Verify(x => x.ObterPosicaoNaFilaAsync(It.IsAny<ExecucaoOs>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Executar_CorridaInsercaoRetornaJaExistente_ReleERepublica()
    {
        repo.SetupSequence(r => r.ObterAsync(OsId, ct))
            .ReturnsAsync((ExecucaoOs?)null)
            .ReturnsAsync(Fabrica.EmStatus(OsId, StatusExecucao.NaFila));
        repo.Setup(r => r.InserirNaFilaAsync(It.IsAny<ExecucaoOs>(), 20, ct)).ReturnsAsync(ResultadoGravacao.JaExistente);
        repo.Setup(r => r.ObterPosicaoNaFilaAsync(It.IsAny<ExecucaoOs>(), ct)).ReturnsAsync(1);

        var r = await Criar().ExecutarAsync(Comando(), ct);

        r.Should().Be(new ResultadoEnfileiramento(true, 1, null));
        repo.Verify(x => x.ObterAsync(OsId, ct), Times.Exactly(2));
        publicador.Verify(p => p.PublicarAsync(It.IsAny<ExecucaoEnfileirada>(), ct), Times.Once);
    }

    [Fact]
    public async Task Executar_CorridaNaRecusaRetornaJaExistente_ReleERepublica()
    {
        repo.SetupSequence(r => r.ObterAsync(OsId, ct))
            .ReturnsAsync((ExecucaoOs?)null)
            .ReturnsAsync(Fabrica.EmStatus(OsId, StatusExecucao.Recusada, motivo: "gravado antes"));
        repo.Setup(r => r.InserirRecusadaAsync(It.IsAny<ExecucaoOs>(), ct)).ReturnsAsync(ResultadoGravacao.JaExistente);

        var r = await Criar(0).ExecutarAsync(Comando(), ct);

        r.Motivo.Should().Be("gravado antes");
        publicador.Verify(p => p.PublicarAsync(It.Is<ExecucaoRecusada>(m => m.Motivo == "gravado antes"), ct), Times.Once);
    }

    [Fact]
    public async Task Executar_JaExistenteSemRegistroNaReleitura_Lanca()
    {
        repo.Setup(r => r.ObterAsync(OsId, ct)).ReturnsAsync((ExecucaoOs?)null);
        repo.Setup(r => r.InserirNaFilaAsync(It.IsAny<ExecucaoOs>(), 20, ct)).ReturnsAsync(ResultadoGravacao.JaExistente);

        var acao = () => Criar().ExecutarAsync(Comando(), ct);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Executar_UnidadeVazia_RecusaComMotivoDeComandoInvalido()
    {
        SemExistente();
        repo.Setup(r => r.InserirRecusadaAsync(It.IsAny<ExecucaoOs>(), ct)).ReturnsAsync(ResultadoGravacao.Gravada);

        var r = await Criar().ExecutarAsync(Comando(unidade: " "), ct);

        r.Enfileirada.Should().BeFalse();
        r.Motivo.Should().StartWith("Comando IniciarExecucao inválido:");
        publicador.Verify(p => p.PublicarAsync(It.Is<ExecucaoRecusada>(m => m.Motivo.StartsWith("Comando IniciarExecucao inválido:")), ct), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Executar_ItemNulo_RecusaNaoLanca(bool nosServicos)
    {
        SemExistente();
        repo.Setup(r => r.InserirRecusadaAsync(It.IsAny<ExecucaoOs>(), ct)).ReturnsAsync(ResultadoGravacao.Gravada);
        IReadOnlyList<ItemExecucaoMensagem> comNulo = [null!];

        var comando = nosServicos ? Comando(servicos: comNulo) : Comando(pecas: comNulo);
        var r = await Criar().ExecutarAsync(comando, ct);

        r.Enfileirada.Should().BeFalse();
        publicador.Verify(p => p.PublicarAsync(It.Is<ExecucaoRecusada>(m => m.Motivo.StartsWith("Comando IniciarExecucao inválido:")), ct), Times.Once);
        repo.Verify(x => x.InserirNaFilaAsync(It.IsAny<ExecucaoOs>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Executar_ItemComQuantidadeZero_RecusaNaoLanca()
    {
        SemExistente();
        repo.Setup(r => r.InserirRecusadaAsync(It.IsAny<ExecucaoOs>(), ct)).ReturnsAsync(ResultadoGravacao.Gravada);

        var r = await Criar().ExecutarAsync(Comando(servicos: [new("Troca", 0)]), ct);

        r.Enfileirada.Should().BeFalse();
        publicador.Verify(p => p.PublicarAsync(It.Is<ExecucaoRecusada>(m => m.Motivo.StartsWith("Comando IniciarExecucao inválido:")), ct), Times.Once);
        repo.Verify(x => x.InserirNaFilaAsync(It.IsAny<ExecucaoOs>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Executar_ListasNulas_EnfileiraComListasVazias()
    {
        SemExistente();
        ExecucaoOs? gravada = null;
        repo.Setup(r => r.InserirNaFilaAsync(It.IsAny<ExecucaoOs>(), 20, ct))
            .Callback<ExecucaoOs, int, CancellationToken>((e, _, _) => gravada = e).ReturnsAsync(ResultadoGravacao.Gravada);
        repo.Setup(r => r.ObterPosicaoNaFilaAsync(It.IsAny<ExecucaoOs>(), ct)).ReturnsAsync(1);

        var comando = new IniciarExecucao(OsId, T0, 10, "SP-01", null!, null!);
        var r = await Criar().ExecutarAsync(comando, ct);

        r.Enfileirada.Should().BeTrue();
        gravada!.Servicos.Should().BeEmpty();
        gravada.Pecas.Should().BeEmpty();
    }

    [Fact]
    public async Task Executar_OrdemServicoIdVazio_LancaArgumentException()
    {
        var acao = () => Criar().ExecutarAsync(Comando(osId: Guid.Empty), ct);

        await acao.Should().ThrowAsync<ArgumentException>();
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Executar_Sucesso_RegistraTelemetriaSucessoNaFila()
    {
        SemExistente();
        repo.Setup(r => r.InserirNaFilaAsync(It.IsAny<ExecucaoOs>(), 20, ct)).ReturnsAsync(ResultadoGravacao.Gravada);
        repo.Setup(r => r.ObterPosicaoNaFilaAsync(It.IsAny<ExecucaoOs>(), ct)).ReturnsAsync(1);

        await Criar().ExecutarAsync(Comando(), ct);

        telemetria.Verify(t => t.Registrar(new EventoExecucao(OsId, 10, "SP-01", null, "NaFila", "Sucesso", null)), Times.Once);
    }
}
