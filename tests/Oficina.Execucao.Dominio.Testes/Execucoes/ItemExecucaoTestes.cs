using FluentAssertions;
using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Dominio.Testes.Execucoes;

public class ItemExecucaoTestes
{
    [Theory]
    [InlineData("", 1)]
    [InlineData("Filtro", 0)]
    public void ItemExecucao_Invalido_Lanca(string descricao, int quantidade)
    {
        var acao = () => new ItemExecucao(descricao, quantidade);
        acao.Should().Throw<ExecucaoInvalidaException>();
    }

    [Fact]
    public void ItemExecucao_Valido_Cria()
    {
        var item = new ItemExecucao("Filtro", 2);
        item.Descricao.Should().Be("Filtro");
        item.Quantidade.Should().Be(2);
    }
}
