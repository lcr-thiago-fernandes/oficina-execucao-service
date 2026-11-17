using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Oficina.Execucao.Aplicacao.Execucoes;
using Oficina.Execucao.Dominio.Execucoes;
using Oficina.Execucao.Infraestrutura.Persistencia;

namespace Oficina.Execucao.Infraestrutura.Testes.Persistencia;

[Collection(DynamoDbCollection.Nome)]
public class RepositorioExecucaoDynamoDbTestes
{
    private static readonly DateTime T0 = new(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
    private readonly DynamoDbFixture _fx;

    public RepositorioExecucaoDynamoDbTestes(DynamoDbFixture fx) => _fx = fx;

    private async Task<(RepositorioExecucaoDynamoDb Repo, string Tabela)> NovoAsync()
    {
        var tabela = await _fx.CriarTabelaAsync();
        var repo = new RepositorioExecucaoDynamoDb(_fx.Cliente, Options.Create(new OpcoesDynamoDb { NomeTabela = tabela }));
        return (repo, tabela);
    }

    private static ExecucaoOs Os(string unidade = "SP-01", int minutos = 0, int numero = 1) =>
        ExecucaoOs.Enfileirar(Guid.NewGuid(), numero, unidade,
            [new ItemExecucao("Troca de oleo", 1), new ItemExecucao("Alinhamento", 2)],
            [new ItemExecucao("Filtro", 3)], T0.AddMinutes(minutos));

    private async Task<int?> AtivasAsync(string tabela, string unidade)
    {
        var r = await _fx.Cliente.GetItemAsync(tabela, new() { ["osId"] = new AttributeValue(EsquemaTabelaExecucao.ChaveContador(unidade)) });
        return r.IsItemSet ? int.Parse(r.Item["ativas"].N) : null;
    }

    [Fact]
    public async Task InserirNaFila_Nova_GravaEIncrementaContador()
    {
        var (repo, tabela) = await NovoAsync();
        var os = Os();

        (await repo.InserirNaFilaAsync(os, 5, default)).Should().Be(ResultadoGravacao.Gravada);

        (await AtivasAsync(tabela, "SP-01")).Should().Be(1);
        var lida = await repo.ObterAsync(os.OrdemServicoId, default);
        lida.Should().BeEquivalentTo(os);
        lida!.Servicos.Should().HaveCount(2);
        lida.Pecas.Should().ContainSingle();
        lida.Versao.Should().Be(1);
    }

    [Fact]
    public async Task InserirNaFila_MesmaOsDuasVezes_SegundaJaExistenteEContadorUm()
    {
        var (repo, tabela) = await NovoAsync();
        var os = Os();

        await repo.InserirNaFilaAsync(os, 5, default);
        (await repo.InserirNaFilaAsync(os, 5, default)).Should().Be(ResultadoGravacao.JaExistente);

        (await AtivasAsync(tabela, "SP-01")).Should().Be(1);
    }

    [Fact]
    public async Task InserirNaFila_UnidadeCheia_CapacidadeEsgotadaSemGravarExecucao()
    {
        var (repo, tabela) = await NovoAsync();
        await repo.InserirNaFilaAsync(Os(), 2, default);
        await repo.InserirNaFilaAsync(Os(), 2, default);
        var terceira = Os();

        (await repo.InserirNaFilaAsync(terceira, 2, default)).Should().Be(ResultadoGravacao.CapacidadeEsgotada);

        (await repo.ObterAsync(terceira.OrdemServicoId, default)).Should().BeNull();
        (await AtivasAsync(tabela, "SP-01")).Should().Be(2);
    }

    [Fact]
    public async Task InserirNaFila_DuasOsConcorrentesUltimaVaga_ExatamenteUmaGravada()
    {
        var (repo, tabela) = await NovoAsync();
        for (var i = 0; i < 10; i++)
        {
            var unidade = $"U-{i}";
            var resultados = await Task.WhenAll(
                Task.Run(() => repo.InserirNaFilaAsync(Os(unidade), 1, default)),
                Task.Run(() => repo.InserirNaFilaAsync(Os(unidade), 1, default)));

            resultados.Should().BeEquivalentTo([ResultadoGravacao.Gravada, ResultadoGravacao.CapacidadeEsgotada]);
            (await AtivasAsync(tabela, unidade)).Should().Be(1);
        }
    }

    [Fact]
    public async Task InserirNaFila_MesmaOsConcorrente_UmaGravadaUmaJaExistente()
    {
        var (repo, tabela) = await NovoAsync();
        var os = Os();

        var resultados = await Task.WhenAll(
            Task.Run(() => repo.InserirNaFilaAsync(os, 5, default)),
            Task.Run(() => repo.InserirNaFilaAsync(os, 5, default)));

        resultados.Should().BeEquivalentTo([ResultadoGravacao.Gravada, ResultadoGravacao.JaExistente]);
        (await AtivasAsync(tabela, "SP-01")).Should().Be(1);
    }

    [Fact]
    public async Task InserirNaFila_CapacidadeZero_CapacidadeEsgotadaSemEscrever()
    {
        var (repo, tabela) = await NovoAsync();
        var os = Os();

        (await repo.InserirNaFilaAsync(os, 0, default)).Should().Be(ResultadoGravacao.CapacidadeEsgotada);

        (await repo.ObterAsync(os.OrdemServicoId, default)).Should().BeNull();
        (await AtivasAsync(tabela, "SP-01")).Should().BeNull();
    }

    [Fact]
    public async Task ObterPosicaoNaFila_ItemAindaNaoVisivelNoIndice_Retorna1()
    {
        var (repo, _) = await NovoAsync();

        (await repo.ObterPosicaoNaFilaAsync(Os(), default)).Should().Be(1);
    }

    [Fact]
    public async Task UnidadesDiferentes_TemContadoresIndependentes()
    {
        var (repo, tabela) = await NovoAsync();

        (await repo.InserirNaFilaAsync(Os("SP-01"), 1, default)).Should().Be(ResultadoGravacao.Gravada);
        (await repo.InserirNaFilaAsync(Os("RJ-01"), 1, default)).Should().Be(ResultadoGravacao.Gravada);

        (await AtivasAsync(tabela, "SP-01")).Should().Be(1);
        (await AtivasAsync(tabela, "RJ-01")).Should().Be(1);
    }

    [Fact]
    public async Task InserirRecusada_NaoOcupaVagaEDuplicataJaExistente()
    {
        var (repo, tabela) = await NovoAsync();
        var recusada = ExecucaoOs.Recusar(Guid.NewGuid(), 4, "SP-01", "Capacidade da unidade SP-01 esgotada (0/0)", T0);

        (await repo.InserirRecusadaAsync(recusada, default)).Should().Be(ResultadoGravacao.Gravada);
        (await repo.InserirRecusadaAsync(recusada, default)).Should().Be(ResultadoGravacao.JaExistente);

        (await AtivasAsync(tabela, "SP-01")).Should().BeNull();
        (await repo.ObterAsync(recusada.OrdemServicoId, default)).Should().BeEquivalentTo(recusada);
    }

    [Fact]
    public async Task Atualizar_VersaoCorreta_Persiste()
    {
        var (repo, _) = await NovoAsync();
        var os = Os();
        await repo.InserirNaFilaAsync(os, 5, default);

        os.IniciarDiagnostico("comecou", T0.AddMinutes(5));
        await repo.AtualizarAsync(os, default);

        var lida = await repo.ObterAsync(os.OrdemServicoId, default);
        lida!.Status.Should().Be(StatusExecucao.EmDiagnostico);
        lida.Versao.Should().Be(2);
        lida.Observacao.Should().Be("comecou");
        lida.DiagnosticoIniciadoEm.Should().Be(T0.AddMinutes(5));
    }

    [Fact]
    public async Task Atualizar_VersaoDesatualizada_LancaConflito()
    {
        var (repo, _) = await NovoAsync();
        var os = Os();
        await repo.InserirNaFilaAsync(os, 5, default);
        var a = (await repo.ObterAsync(os.OrdemServicoId, default))!;
        var b = (await repo.ObterAsync(os.OrdemServicoId, default))!;
        a.IniciarDiagnostico(null, T0.AddMinutes(1));
        b.IniciarDiagnostico(null, T0.AddMinutes(2));

        await repo.AtualizarAsync(a, default);
        var act = () => repo.AtualizarAsync(b, default);

        await act.Should().ThrowAsync<ConflitoDeConcorrenciaException>();
    }

    [Fact]
    public async Task Atualizar_Finalizada_LiberaVaga()
    {
        var (repo, tabela) = await NovoAsync();
        var a = Os();
        await repo.InserirNaFilaAsync(a, 1, default);
        var b = Os();
        (await repo.InserirNaFilaAsync(b, 1, default)).Should().Be(ResultadoGravacao.CapacidadeEsgotada);

        a.IniciarDiagnostico(null, T0.AddMinutes(1));
        await repo.AtualizarAsync(a, default);
        a.IniciarReparo(null, T0.AddMinutes(2));
        await repo.AtualizarAsync(a, default);
        a.Finalizar(null, T0.AddMinutes(3));
        await repo.AtualizarAsync(a, default);

        (await AtivasAsync(tabela, "SP-01")).Should().Be(0);
        (await repo.InserirNaFilaAsync(b, 1, default)).Should().Be(ResultadoGravacao.Gravada);
        (await AtivasAsync(tabela, "SP-01")).Should().Be(1);
    }

    [Fact]
    public async Task ObterPosicaoNaFila_ContaSoNaFilaDaMesmaUnidade()
    {
        var (repo, _) = await NovoAsync();
        var a = Os("SP-01", 0);
        var b = Os("SP-01", 1);
        var c = Os("SP-01", 2);
        var d = Os("RJ-01", 0);
        foreach (var o in new[] { a, b, c, d }) await repo.InserirNaFilaAsync(o, 10, default);
        a.IniciarDiagnostico(null, T0.AddMinutes(10));
        await repo.AtualizarAsync(a, default);

        (await repo.ObterPosicaoNaFilaAsync(c, default)).Should().Be(2);
        (await repo.ObterPosicaoNaFilaAsync(b, default)).Should().Be(1);
    }

    [Fact]
    public async Task ListarPorStatus_OrdenaPorEnfileiradaEmENaoTrazContadores()
    {
        var (repo, _) = await NovoAsync();
        var tarde = Os("SP-01", 5);
        var cedo = Os("RJ-01", 1);
        await repo.InserirNaFilaAsync(tarde, 10, default);
        await repo.InserirNaFilaAsync(cedo, 10, default);

        var lista = await repo.ListarPorStatusAsync(StatusExecucao.NaFila, default);

        lista.Select(x => x.OrdemServicoId).Should().Equal(cedo.OrdemServicoId, tarde.OrdemServicoId);
    }

    [Fact]
    public async Task CriadorTabela_CriaQuandoAusenteEIgnoraQuandoExiste()
    {
        var nome = $"t-{Guid.NewGuid():N}";
        var opcoes = Options.Create(new OpcoesDynamoDb { NomeTabela = nome, CriarTabelaSeAusente = true });
        IHostedService servico = new CriadorTabelaHostedService(_fx.Cliente, opcoes);

        await servico.StartAsync(default);
        await servico.StartAsync(default);

        var d = await _fx.Cliente.DescribeTableAsync(nome);
        d.Table.TableStatus.Should().Be(TableStatus.ACTIVE);
        d.Table.GlobalSecondaryIndexes.Should().ContainSingle();
    }

    [Fact]
    public async Task CriadorTabela_DesligadoNaoCria()
    {
        var nome = $"t-{Guid.NewGuid():N}";
        var servico = new CriadorTabelaHostedService(_fx.Cliente, Options.Create(new OpcoesDynamoDb { NomeTabela = nome }));

        await servico.StartAsync(default);

        var act = () => _fx.Cliente.DescribeTableAsync(nome);
        await act.Should().ThrowAsync<ResourceNotFoundException>();
    }
}
