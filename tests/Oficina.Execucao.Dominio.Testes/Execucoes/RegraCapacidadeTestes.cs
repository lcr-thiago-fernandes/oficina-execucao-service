using FluentAssertions;
using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Dominio.Testes.Execucoes;

public class RegraCapacidadeTestes
{
    [Theory]
    [InlineData(0, 20, true)]
    [InlineData(19, 20, true)]
    [InlineData(20, 20, false)]
    [InlineData(0, 0, false)]
    public void HaVaga(int ativas, int capacidade, bool esperado) =>
        RegraCapacidade.HaVaga(ativas, capacidade).Should().Be(esperado);

    [Fact]
    public void MotivoCapacidadeEsgotada_FormatoDoContrato() =>
        RegraCapacidade.MotivoCapacidadeEsgotada("SP-01", 20).Should().Be("Capacidade da unidade SP-01 esgotada (20/20)");
}
