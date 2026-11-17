using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oficina.Execucao.Aplicacao.Contratos.Mensagens;

/// <summary>Opcoes de serializacao JSON do contrato das mensagens da saga.</summary>
public static class OpcoesJsonContrato
{
    public static JsonSerializerOptions Criar()
    {
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opcoes.Converters.Add(new JsonStringEnumConverter());
        return opcoes;
    }
}
