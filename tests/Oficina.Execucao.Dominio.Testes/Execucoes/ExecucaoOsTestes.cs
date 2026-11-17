using FluentAssertions;
using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Dominio.Testes.Execucoes;

public class ExecucaoOsTestes
{
    private static readonly DateTime T0 = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OsId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Local = DateTime.SpecifyKind(T0, DateTimeKind.Local);

    private static ExecucaoOs NovaNaFila() =>
        ExecucaoOs.Enfileirar(OsId, 1042, "SP-01", [new("Troca de oleo", 1)], [new("Filtro de oleo", 2)], T0);

    [Fact]
    public void Enfileirar_DadosValidos_FicaNaFilaVersao1Ativa()
    {
        var e = NovaNaFila();
        e.Status.Should().Be(StatusExecucao.NaFila);
        e.Versao.Should().Be(1);
        e.Ativa.Should().BeTrue();
        e.EnfileiradaEm.Should().Be(T0);
        e.Servicos.Should().ContainSingle();
        e.Pecas.Should().ContainSingle();
        e.DuracaoMinutos.Should().BeNull();
    }

    [Fact]
    public void Enfileirar_ListasNulas_ViramVazias()
    {
        var e = ExecucaoOs.Enfileirar(OsId, 1, "SP-01", null, null, T0);
        e.Servicos.Should().BeEmpty();
        e.Pecas.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Enfileirar_NumeroOsInvalido_Lanca(int n)
    {
        var acao = () => ExecucaoOs.Enfileirar(OsId, n, "SP-01", null, null, T0);
        acao.Should().Throw<ExecucaoInvalidaException>();
    }

    [Fact]
    public void Enfileirar_OsIdVazio_Lanca()
    {
        var acao = () => ExecucaoOs.Enfileirar(Guid.Empty, 1, "SP-01", null, null, T0);
        acao.Should().Throw<ExecucaoInvalidaException>();
    }

    [Fact]
    public void Enfileirar_UnidadeEmBranco_Lanca()
    {
        var acao = () => ExecucaoOs.Enfileirar(OsId, 1, "   ", null, null, T0);
        acao.Should().Throw<ExecucaoInvalidaException>();
    }

    [Fact]
    public void Enfileirar_HorarioNaoUtc_Lanca()
    {
        var acao = () => ExecucaoOs.Enfileirar(OsId, 1, "SP-01", null, null, Local);
        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Enfileirar_UnidadeComEspacos_NormalizaComTrim() =>
        ExecucaoOs.Enfileirar(OsId, 1, " SP-01 ", null, null, T0).Unidade.Should().Be("SP-01");

    [Fact]
    public void CicloCompleto_NaFilaAteFinalizada_IncrementaVersaoECalculaDuracao()
    {
        var e = NovaNaFila();
        e.IniciarDiagnostico("Veiculo no elevador 2", T0.AddMinutes(10)).Should().BeTrue();
        e.IniciarReparo(null, T0.AddMinutes(30)).Should().BeTrue();
        e.Finalizar("Servico concluido sem pendencias", T0.AddMinutes(105)).Should().BeTrue();
        e.Status.Should().Be(StatusExecucao.Finalizada);
        e.Versao.Should().Be(4);
        e.Ativa.Should().BeFalse();
        e.DuracaoMinutos.Should().Be(95);
        e.Observacao.Should().Be("Servico concluido sem pendencias");
        e.DiagnosticoIniciadoEm.Should().Be(T0.AddMinutes(10));
        e.ReparoIniciadoEm.Should().Be(T0.AddMinutes(30));
        e.FinalizadaEm.Should().Be(T0.AddMinutes(105));
    }

    [Fact]
    public void IniciarReparo_SemObservacao_MantemObservacaoAnterior()
    {
        var e = NovaNaFila();
        e.IniciarDiagnostico("diag", T0.AddMinutes(1));
        e.IniciarReparo(null, T0.AddMinutes(2));
        e.Observacao.Should().Be("diag");
    }

    [Fact]
    public void IniciarDiagnostico_JaEmDiagnostico_RetornaFalseSemAlterarVersao()
    {
        var e = NovaNaFila();
        e.IniciarDiagnostico("a", T0.AddMinutes(1));
        e.IniciarDiagnostico("b", T0.AddMinutes(2)).Should().BeFalse();
        e.Versao.Should().Be(2);
        e.Observacao.Should().Be("a");
        e.DiagnosticoIniciadoEm.Should().Be(T0.AddMinutes(1));
    }

    [Fact]
    public void IniciarReparo_JaEmReparo_RetornaFalse()
    {
        var e = NovaNaFila();
        e.IniciarDiagnostico(null, T0.AddMinutes(1));
        e.IniciarReparo(null, T0.AddMinutes(2));
        e.IniciarReparo(null, T0.AddMinutes(3)).Should().BeFalse();
        e.Versao.Should().Be(3);
    }

    [Fact]
    public void Finalizar_JaFinalizada_RetornaFalse()
    {
        var e = NovaNaFila();
        e.IniciarDiagnostico(null, T0.AddMinutes(1));
        e.IniciarReparo(null, T0.AddMinutes(2));
        e.Finalizar(null, T0.AddMinutes(3));
        e.Finalizar(null, T0.AddMinutes(4)).Should().BeFalse();
        e.Versao.Should().Be(4);
        e.FinalizadaEm.Should().Be(T0.AddMinutes(3));
    }

    [Fact]
    public void IniciarReparo_NaFila_LancaTransicaoInvalida()
    {
        var acao = () => NovaNaFila().IniciarReparo(null, T0.AddMinutes(1));
        acao.Should().Throw<TransicaoExecucaoInvalidaException>();
    }

    [Fact]
    public void Finalizar_EmDiagnostico_LancaTransicaoInvalida()
    {
        var e = NovaNaFila();
        e.IniciarDiagnostico(null, T0.AddMinutes(1));
        var acao = () => e.Finalizar(null, T0.AddMinutes(2));
        acao.Should().Throw<TransicaoExecucaoInvalidaException>();
    }

    [Fact]
    public void IniciarDiagnostico_Recusada_LancaTransicaoInvalida()
    {
        var e = ExecucaoOs.Recusar(OsId, 1, "SP-01", "lotada", T0);
        var acao = () => e.IniciarDiagnostico(null, T0.AddMinutes(1));
        acao.Should().Throw<TransicaoExecucaoInvalidaException>();
    }

    [Fact]
    public void Recusar_GuardaMotivoENaoEhAtiva()
    {
        var e = ExecucaoOs.Recusar(OsId, 1, "SP-01", "lotada", T0);
        e.Status.Should().Be(StatusExecucao.Recusada);
        e.MotivoRecusa.Should().Be("lotada");
        e.Ativa.Should().BeFalse();
        e.Versao.Should().Be(1);
    }

    [Fact]
    public void Recusar_UnidadeVaziaENumeroOsZero_Aceita()
    {
        var e = ExecucaoOs.Recusar(OsId, 0, null!, "comando invalido", T0);
        e.Unidade.Should().BeEmpty();
        e.NumeroOs.Should().Be(0);
        e.Status.Should().Be(StatusExecucao.Recusada);
    }

    [Fact]
    public void Recusar_MotivoVazio_Lanca()
    {
        var acao = () => ExecucaoOs.Recusar(OsId, 1, "SP-01", " ", T0);
        acao.Should().Throw<ExecucaoInvalidaException>();
    }

    [Fact]
    public void Recusar_OsIdVazio_Lanca()
    {
        var acao = () => ExecucaoOs.Recusar(Guid.Empty, 1, "SP-01", "x", T0);
        acao.Should().Throw<ExecucaoInvalidaException>();
    }

    [Fact]
    public void Recusar_HorarioNaoUtc_Lanca()
    {
        var acao = () => ExecucaoOs.Recusar(OsId, 1, "SP-01", "x", Local);
        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Transicao_HorarioNaoUtc_Lanca()
    {
        var e = NovaNaFila();
        var acao = () => e.IniciarDiagnostico(null, Local);
        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reconstituir_RestauraTodosOsCampos()
    {
        var e = ExecucaoOs.Reconstituir(OsId, 7, "SP-01", [new("S", 1)], [new("P", 1)], StatusExecucao.EmReparo,
            T0, T0.AddMinutes(1), T0.AddMinutes(2), null, "obs", null, 3);
        e.Status.Should().Be(StatusExecucao.EmReparo);
        e.Versao.Should().Be(3);
        e.ReparoIniciadoEm.Should().Be(T0.AddMinutes(2));
        e.Observacao.Should().Be("obs");
        e.Ativa.Should().BeTrue();
    }
}
