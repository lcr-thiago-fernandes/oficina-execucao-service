using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Oficina.Execucao.Aplicacao.Execucoes;
using Oficina.Execucao.Dominio.Execucoes;
using Oficina.Execucao.Infraestrutura.Persistencia;

namespace Oficina.Execucao.Infraestrutura.Testes.Persistencia;

/// <summary>Garante o caminho de repeticao de TransactionConflict, que o DynamoDB Local nao produz de forma deterministica.</summary>
public class RepositorioExecucaoConflitoTestes
{
    private static readonly DateTime T0 = new(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);

    private static TransactionCanceledException Cancelada(params string[] codigos) =>
        new("cancelada") { CancellationReasons = codigos.Select(c => new CancellationReason { Code = c }).ToList() };

    private static ExecucaoOs Os() =>
        ExecucaoOs.Enfileirar(Guid.NewGuid(), 1, "SP-01", [new ItemExecucao("A", 1)], [], T0);

    private static RepositorioExecucaoDynamoDb Repo(Mock<IAmazonDynamoDB> mock) =>
        new(mock.Object, Options.Create(new OpcoesDynamoDb { NomeTabela = "t" }));

    private static Mock<IAmazonDynamoDB> Simular(List<TransactWriteItemsRequest> enviados, params Func<Exception?>[] respostas)
    {
        var mock = new Mock<IAmazonDynamoDB>();
        var i = 0;
        mock.Setup(d => d.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Returns((TransactWriteItemsRequest r, CancellationToken _) =>
            {
                enviados.Add(r);
                var ex = respostas[Math.Min(i++, respostas.Length - 1)]();
                return ex is null ? Task.FromResult(new TransactWriteItemsResponse()) : Task.FromException<TransactWriteItemsResponse>(ex);
            });
        return mock;
    }

    [Fact]
    public async Task InserirNaFila_UmConflitoDepoisSucesso_RepeteComRequestNovo()
    {
        var enviados = new List<TransactWriteItemsRequest>();
        var repo = Repo(Simular(enviados, () => Cancelada("None", "TransactionConflict"), () => null));

        (await repo.InserirNaFilaAsync(Os(), 5, default)).Should().Be(ResultadoGravacao.Gravada);

        enviados.Should().HaveCount(2);
        enviados[0].Should().NotBeSameAs(enviados[1]);
        enviados.Should().OnlyContain(r => r.ClientRequestToken == null, "F11: o token nunca e fixado pelo codigo; o SDK gera um novo por request");
    }

    [Fact]
    public async Task InserirNaFila_CincoConflitos_RelancaDepoisDe5Tentativas()
    {
        var enviados = new List<TransactWriteItemsRequest>();
        var repo = Repo(Simular(enviados, () => Cancelada("None", "TransactionConflict")));

        var act = () => repo.InserirNaFilaAsync(Os(), 5, default);

        await act.Should().ThrowAsync<TransactionCanceledException>();
        enviados.Should().HaveCount(5);
    }

    [Fact]
    public async Task InserirNaFila_DuplicataComConflito_RepeteEEntaoReportaJaExistente()
    {
        var enviados = new List<TransactWriteItemsRequest>();
        var repo = Repo(Simular(enviados,
            () => Cancelada("ConditionalCheckFailed", "TransactionConflict"),
            () => Cancelada("ConditionalCheckFailed", "None")));

        (await repo.InserirNaFilaAsync(Os(), 5, default)).Should().Be(ResultadoGravacao.JaExistente);

        enviados.Should().HaveCount(2);
    }

    [Fact]
    public async Task InserirNaFila_CapacidadeComConflito_NaoVirouCapacidadeEsgotadaSemRepetir()
    {
        var enviados = new List<TransactWriteItemsRequest>();
        var repo = Repo(Simular(enviados, () => Cancelada("TransactionConflict", "ConditionalCheckFailed"), () => null));

        (await repo.InserirNaFilaAsync(Os(), 5, default)).Should().Be(ResultadoGravacao.Gravada);

        enviados.Should().HaveCount(2);
    }

    [Fact]
    public async Task Atualizar_FinalizadaComConflito_Repete()
    {
        var os = Os();
        os.IniciarDiagnostico(null, T0.AddMinutes(1));
        os.IniciarReparo(null, T0.AddMinutes(2));
        os.Finalizar(null, T0.AddMinutes(3));
        var enviados = new List<TransactWriteItemsRequest>();
        var repo = Repo(Simular(enviados, () => Cancelada("None", "TransactionConflict"), () => null));

        await repo.AtualizarAsync(os, default);

        enviados.Should().HaveCount(2);
        enviados[0].Should().NotBeSameAs(enviados[1]);
    }
}
