using FluentAssertions;

namespace Oficina.Execucao.Dominio.Testes;

public class EsqueletoTestes
{
    [Fact]
    public void Solution_Compila() => typeof(EsqueletoTestes).Assembly.Should().NotBeNull();
}
