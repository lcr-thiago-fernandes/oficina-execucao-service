using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Oficina.Execucao.Aplicacao.Execucoes;
using Oficina.Execucao.Infraestrutura.Telemetria;

namespace Oficina.Execucao.Infraestrutura.Testes.Telemetria;

public class TelemetriaExecucaoNewRelicTestes
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("EmReparo", 95)]
    public void Registrar_SemAgenteCarregado_NaoLancaENaoLogaAviso(string? statusAnterior, int? duracaoMinutos)
    {
        var logger = new Mock<ILogger<TelemetriaExecucaoNewRelic>>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var telemetria = new TelemetriaExecucaoNewRelic(logger.Object);

        var acao = () => telemetria.Registrar(new EventoExecucao(Guid.NewGuid(), 1042, "SP-01",
            statusAnterior, "Finalizada", "Sucesso", duracaoMinutos));

        acao.Should().NotThrow();
        logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    [Fact]
    public void MontarAtributos_StatusAnteriorNulo_UsaInicialEOmiteDuracao()
    {
        var osId = Guid.NewGuid();

        var atributos = TelemetriaExecucaoNewRelic.MontarAtributos(
            new EventoExecucao(osId, 1042, "SP-01", null, "NaFila", "Sucesso", null));

        atributos.Should().BeEquivalentTo(new Dictionary<string, object>
        {
            ["ordemServicoId"] = osId.ToString(),
            ["numeroOs"] = 1042,
            ["unidade"] = "SP-01",
            ["statusAnterior"] = "(inicial)",
            ["statusNovo"] = "NaFila",
            ["resultado"] = "Sucesso"
        });
    }

    [Fact]
    public void MontarAtributos_ComDuracao_IncluiDuracaoMinutos()
    {
        var atributos = TelemetriaExecucaoNewRelic.MontarAtributos(
            new EventoExecucao(Guid.NewGuid(), 7, "RJ-02", "EmReparo", "Finalizada", "Sucesso", 95));

        atributos["statusAnterior"].Should().Be("EmReparo");
        atributos["duracaoMinutos"].Should().Be(95);
    }
}
