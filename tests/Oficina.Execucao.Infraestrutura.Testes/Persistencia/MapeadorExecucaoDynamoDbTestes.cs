using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Oficina.Execucao.Dominio.Execucoes;
using Oficina.Execucao.Infraestrutura.Persistencia;

namespace Oficina.Execucao.Infraestrutura.Testes.Persistencia;

public class MapeadorExecucaoDynamoDbTestes
{
    private static readonly DateTime T0 = new(2026, 11, 16, 10, 0, 0, 123, DateTimeKind.Utc);

    [Fact]
    public void ParaItem_Enfileirada_OmiteAtributosNulosEUsaFormatoOrdenavel()
    {
        var id = Guid.NewGuid();
        var e = ExecucaoOs.Enfileirar(id, 7, "SP-01", [new ItemExecucao("Troca de oleo", 1)], [new ItemExecucao("Filtro", 2)], T0);

        var item = MapeadorExecucaoDynamoDb.ParaItem(e);

        item["osId"].S.Should().Be(id.ToString("D"));
        item["numeroOs"].N.Should().Be("7");
        item["status"].S.Should().Be("NaFila");
        item["enfileiradaEm"].S.Should().Be(T0.ToString("O"));
        item["versao"].N.Should().Be("1");
        item["servicos"].L.Single().M["descricao"].S.Should().Be("Troca de oleo");
        item["pecas"].L.Single().M["quantidade"].N.Should().Be("2");
        item.Keys.Should().NotContain(["diagnosticoIniciadoEm", "reparoIniciadoEm", "finalizadaEm", "observacao", "motivoRecusa"]);
    }

    [Fact]
    public void RoundTrip_Finalizada_PreservaTodosOsCampos()
    {
        var e = ExecucaoOs.Enfileirar(Guid.NewGuid(), 9, "RJ-01", [new ItemExecucao("A", 1)], [], T0);
        e.IniciarDiagnostico("obs1", T0.AddMinutes(1));
        e.IniciarReparo(null, T0.AddMinutes(2));
        e.Finalizar("fim", T0.AddMinutes(3));

        var volta = MapeadorExecucaoDynamoDb.ParaExecucao(MapeadorExecucaoDynamoDb.ParaItem(e));

        volta.Should().BeEquivalentTo(e);
        volta.EnfileiradaEm.Kind.Should().Be(DateTimeKind.Utc);
        volta.FinalizadaEm!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void RoundTrip_Recusada_PreservaMotivo()
    {
        var e = ExecucaoOs.Recusar(Guid.NewGuid(), 3, "SP-01", "Capacidade da unidade SP-01 esgotada (0/0)", T0);

        var volta = MapeadorExecucaoDynamoDb.ParaExecucao(MapeadorExecucaoDynamoDb.ParaItem(e));

        volta.Should().BeEquivalentTo(e);
        volta.Status.Should().Be(StatusExecucao.Recusada);
    }

    [Fact]
    public void EsquemaTabela_TemChaveEGsiIdenticosAoTerraform()
    {
        var r = EsquemaTabelaExecucao.CriarRequest("tabela");

        r.BillingMode.Should().Be(BillingMode.PAY_PER_REQUEST);
        r.KeySchema.Should().ContainSingle().Which.AttributeName.Should().Be("osId");
        r.AttributeDefinitions.Select(a => a.AttributeName).Should().BeEquivalentTo("osId", "status", "enfileiradaEm");
        var gsi = r.GlobalSecondaryIndexes.Should().ContainSingle().Subject;
        gsi.IndexName.Should().Be("status-enfileiradaEm");
        gsi.Projection.ProjectionType.Should().Be(ProjectionType.ALL);
        EsquemaTabelaExecucao.ChaveContador("SP-01").Should().Be("UNIDADE#SP-01");
    }
}
