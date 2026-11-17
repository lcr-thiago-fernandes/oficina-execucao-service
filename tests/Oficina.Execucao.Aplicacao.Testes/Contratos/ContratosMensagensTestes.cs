using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using MassTransit;
using Oficina.Execucao.Aplicacao.Contratos.Mensagens;

namespace Oficina.Execucao.Aplicacao.Testes.Contratos;

public class ContratosMensagensTestes
{
    public static TheoryData<Type, string> Contratos => new()
    {
        { typeof(IniciarExecucao), "IniciarExecucao" },
        { typeof(ExecucaoEnfileirada), "ExecucaoEnfileirada" },
        { typeof(ExecucaoRecusada), "ExecucaoRecusada" },
        { typeof(StatusExecucaoAlterado), "StatusExecucaoAlterado" },
        { typeof(ExecucaoFinalizada), "ExecucaoFinalizada" }
    };

    private static readonly string PastaContratos = Path.Combine("Contratos", "Mensagens");

    [Theory, MemberData(nameof(Contratos))]
    public void Json_IdaEVolta_PreservaPropriedadesEValores(Type tipo, string nome)
    {
        var original = JsonNode.Parse(File.ReadAllText(Path.Combine(PastaContratos, $"{nome}.json")))!;
        var obj = original.Deserialize(tipo, OpcoesJsonContrato.Criar());
        var reserializado = JsonSerializer.SerializeToNode(obj, tipo, OpcoesJsonContrato.Criar());
        JsonNode.DeepEquals(original, reserializado).Should().BeTrue($"{nome}.json diverge do tipo");
    }

    [Theory, MemberData(nameof(Contratos))]
    public void Tipo_TemMessageUrnDoContrato(Type tipo, string nome)
        => tipo.GetCustomAttribute<MessageUrnAttribute>()!.Urn.ToString()
            .Should().Be($"urn:message:oficina:saga:{nome}");

    [Theory, MemberData(nameof(Contratos))]
    public void Tipo_TemEntityNameDoContrato(Type tipo, string nome)
        => tipo.GetCustomAttribute<EntityNameAttribute>()!.EntityName.Should().Be($"oficina-saga-{nome}");

    [Fact]
    public void StatusExecucaoAlterado_SerializaEnumComoString()
    {
        var mensagem = new StatusExecucaoAlterado(
            Guid.NewGuid(), DateTime.UtcNow, StatusExecucaoMensagem.EmReparo, null);

        var json = JsonSerializer.Serialize(mensagem, OpcoesJsonContrato.Criar());

        json.Should().Contain("\"statusExecucao\":\"EmReparo\"");
    }

    [Fact]
    public void PastaDeContratos_TemOs16JsonDoSistema()
        => Directory.GetFiles(PastaContratos, "*.json").Should().HaveCount(16);
}
