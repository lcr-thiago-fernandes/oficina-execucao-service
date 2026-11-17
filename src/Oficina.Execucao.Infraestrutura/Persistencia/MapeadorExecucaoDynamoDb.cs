using System.Globalization;
using Amazon.DynamoDBv2.Model;
using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Infraestrutura.Persistencia;

public static class MapeadorExecucaoDynamoDb
{
    public static Dictionary<string, AttributeValue> ParaItem(ExecucaoOs e)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["osId"] = new(e.OrdemServicoId.ToString("D")),
            ["numeroOs"] = new() { N = e.NumeroOs.ToString(CultureInfo.InvariantCulture) },
            ["unidade"] = new(e.Unidade),
            ["status"] = new(e.Status.ToString()),
            ["enfileiradaEm"] = new(Formatar(e.EnfileiradaEm)),
            ["servicos"] = ParaLista(e.Servicos),
            ["pecas"] = ParaLista(e.Pecas),
            ["versao"] = new() { N = e.Versao.ToString(CultureInfo.InvariantCulture) }
        };

        if (e.DiagnosticoIniciadoEm is { } d) item["diagnosticoIniciadoEm"] = new(Formatar(d));
        if (e.ReparoIniciadoEm is { } r) item["reparoIniciadoEm"] = new(Formatar(r));
        if (e.FinalizadaEm is { } f) item["finalizadaEm"] = new(Formatar(f));
        if (e.Observacao is not null) item["observacao"] = new(e.Observacao);
        if (e.MotivoRecusa is not null) item["motivoRecusa"] = new(e.MotivoRecusa);
        return item;
    }

    public static ExecucaoOs ParaExecucao(IReadOnlyDictionary<string, AttributeValue> item) =>
        ExecucaoOs.Reconstituir(
            Guid.Parse(item["osId"].S),
            int.Parse(item["numeroOs"].N, CultureInfo.InvariantCulture),
            item["unidade"].S,
            LerLista(item, "servicos"),
            LerLista(item, "pecas"),
            Enum.Parse<StatusExecucao>(item["status"].S),
            Ler(item["enfileiradaEm"].S),
            LerData(item, "diagnosticoIniciadoEm"),
            LerData(item, "reparoIniciadoEm"),
            LerData(item, "finalizadaEm"),
            item.TryGetValue("observacao", out var obs) ? obs.S : null,
            item.TryGetValue("motivoRecusa", out var motivo) ? motivo.S : null,
            int.Parse(item["versao"].N, CultureInfo.InvariantCulture));

    internal static string Formatar(DateTime utc) => utc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTime Ler(string valor) =>
        DateTime.Parse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();

    private static DateTime? LerData(IReadOnlyDictionary<string, AttributeValue> item, string chave) =>
        item.TryGetValue(chave, out var v) && v.S is not null ? Ler(v.S) : null;

    private static AttributeValue ParaLista(IReadOnlyList<ItemExecucao> itens) => new()
    {
        IsLSet = true,
        L = itens.Select(i => new AttributeValue
        {
            IsMSet = true,
            M = new Dictionary<string, AttributeValue>
            {
                ["descricao"] = new(i.Descricao),
                ["quantidade"] = new() { N = i.Quantidade.ToString(CultureInfo.InvariantCulture) }
            }
        }).ToList()
    };

    private static IReadOnlyList<ItemExecucao> LerLista(IReadOnlyDictionary<string, AttributeValue> item, string chave) =>
        item.TryGetValue(chave, out var v) && v.L is not null
            ? v.L.Select(x => new ItemExecucao(x.M["descricao"].S, int.Parse(x.M["quantidade"].N, CultureInfo.InvariantCulture))).ToList()
            : [];
}
