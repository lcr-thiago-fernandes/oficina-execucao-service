using FluentAssertions;
using Oficina.Execucao.Infraestrutura.Mensageria;

namespace Oficina.Execucao.Infraestrutura.Testes.Mensageria;

public class NomesMensageriaTestes
{
    [Fact]
    public void FilaBus_ComNomeDoPod_SufixaInstancia()
        => NomesMensageria.FilaBus("prd", "oficina-execucao-7d9f8c6b5-x2k4q")
            .Should().Be("prd-oficina-execucao-bus-oficina-execucao-7d9f8c6b5-x2k4q");

    [Theory]
    [InlineData("MAQUINA-01", "hml-oficina-execucao-bus-maquina-01")]
    [InlineData("host.local", "hml-oficina-execucao-bus-host-local")]
    [InlineData("pc do joão_1", "hml-oficina-execucao-bus-pc-do-jo-o_1")]
    public void FilaBus_SanitizaInstancia(string instancia, string esperado)
        => NomesMensageria.FilaBus("hml", instancia).Should().Be(esperado);

    [Fact]
    public void FilaBus_NomeLongo_TruncaNoLimiteDoSqs()
    {
        var nome = NomesMensageria.FilaBus("prd", new string('a', 200));

        nome.Should().HaveLength(NomesMensageria.TamanhoMaximoFilaSqs).And.HaveLength(80);
        nome.Should().StartWith("prd-oficina-execucao-bus-aaa");
    }

    [Fact]
    public void InstanciaAtual_UsaHostnameOuNomeDaMaquina()
    {
        var anterior = Environment.GetEnvironmentVariable("HOSTNAME");
        try
        {
            Environment.SetEnvironmentVariable("HOSTNAME", "pod-abc");
            NomesMensageria.InstanciaAtual().Should().Be("pod-abc");

            Environment.SetEnvironmentVariable("HOSTNAME", null);
            NomesMensageria.InstanciaAtual().Should().Be(Environment.MachineName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOSTNAME", anterior);
        }
    }
}
