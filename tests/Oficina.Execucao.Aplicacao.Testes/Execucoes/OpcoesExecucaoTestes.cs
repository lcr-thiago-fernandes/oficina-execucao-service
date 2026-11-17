using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Oficina.Execucao.Aplicacao.Execucoes;

namespace Oficina.Execucao.Aplicacao.Testes.Execucoes;

public class OpcoesExecucaoTestes
{
    private static ServiceProvider Provedor(string? capacidade)
    {
        var valores = new Dictionary<string, string?>();
        if (capacidade is not null) valores["Execucao:CapacidadeMaximaPorUnidade"] = capacidade;
        var config = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        return new ServiceCollection().AdicionarAplicacao(config).BuildServiceProvider();
    }

    [Fact]
    public void Opcoes_CapacidadeNegativa_FalhaNaValidacao()
    {
        using var sp = Provedor("-1");

        var acao = () => sp.GetRequiredService<IOptions<OpcoesExecucao>>().Value;

        acao.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Opcoes_SemConfiguracao_UsaPadrao20()
    {
        using var sp = Provedor(null);
        sp.GetRequiredService<IOptions<OpcoesExecucao>>().Value.CapacidadeMaximaPorUnidade.Should().Be(20);
    }

    [Fact]
    public void Opcoes_CapacidadeZero_EValida()
    {
        using var sp = Provedor("0");
        sp.GetRequiredService<IOptions<OpcoesExecucao>>().Value.CapacidadeMaximaPorUnidade.Should().Be(0);
    }
}
