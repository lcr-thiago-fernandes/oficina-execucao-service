using FluentAssertions;
using Moq;
using Oficina.Execucao.Aplicacao.Execucoes;
using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Aplicacao.Testes.Execucoes;

public class ConsultarExecucoesHandlerTestes
{
    private readonly CancellationToken ct = new CancellationTokenSource().Token;
    private readonly Mock<IRepositorioExecucao> repo = new();

    private static ExecucaoOs Os(StatusExecucao status, int minutosAtras) =>
        ExecucaoOs.Reconstituir(Guid.NewGuid(), 1, "SP-01", [], [], status, RelogioFixo.T0.AddMinutes(-minutosAtras),
            null, null, null, null, null, 1);

    [Fact]
    public async Task Listar_SemStatus_RetornaAtivasOrdenadasPorEnfileiradaEm()
    {
        var a = Os(StatusExecucao.NaFila, 10);
        var b = Os(StatusExecucao.EmDiagnostico, 30);
        var c = Os(StatusExecucao.EmReparo, 20);
        repo.Setup(r => r.ListarPorStatusAsync(StatusExecucao.NaFila, ct)).ReturnsAsync([a]);
        repo.Setup(r => r.ListarPorStatusAsync(StatusExecucao.EmDiagnostico, ct)).ReturnsAsync([b]);
        repo.Setup(r => r.ListarPorStatusAsync(StatusExecucao.EmReparo, ct)).ReturnsAsync([c]);

        var lista = await new ConsultarExecucoesHandler(repo.Object).ListarAsync(null, ct);

        lista.Should().Equal(b, c, a);
        repo.Verify(r => r.ListarPorStatusAsync(StatusExecucao.Finalizada, It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.ListarPorStatusAsync(StatusExecucao.Recusada, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Listar_ComStatus_DelegaAoRepositorio()
    {
        var f = Os(StatusExecucao.Finalizada, 5);
        repo.Setup(r => r.ListarPorStatusAsync(StatusExecucao.Finalizada, ct)).ReturnsAsync([f]);

        var lista = await new ConsultarExecucoesHandler(repo.Object).ListarAsync(StatusExecucao.Finalizada, ct);

        lista.Should().Equal(f);
    }

    [Fact]
    public async Task Obter_DelegaAoRepositorio()
    {
        var e = Os(StatusExecucao.NaFila, 1);
        repo.Setup(r => r.ObterAsync(e.OrdemServicoId, ct)).ReturnsAsync(e);

        (await new ConsultarExecucoesHandler(repo.Object).ObterAsync(e.OrdemServicoId, ct)).Should().BeSameAs(e);
    }
}
