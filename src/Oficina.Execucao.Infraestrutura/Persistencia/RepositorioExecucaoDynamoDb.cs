using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using Oficina.Execucao.Aplicacao.Execucoes;
using Oficina.Execucao.Dominio.Execucoes;

namespace Oficina.Execucao.Infraestrutura.Persistencia;

public sealed class RepositorioExecucaoDynamoDb : IRepositorioExecucao
{
    private const int MaxTentativasConflito = 5;
    private const string ConditionalCheckFailed = "ConditionalCheckFailed";
    private const string TransactionConflict = "TransactionConflict";

    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _tabela;

    public RepositorioExecucaoDynamoDb(IAmazonDynamoDB dynamo, IOptions<OpcoesDynamoDb> opcoes)
    {
        _dynamo = dynamo;
        _tabela = opcoes.Value.NomeTabela;
    }

    public async Task<ResultadoGravacao> InserirNaFilaAsync(ExecucaoOs execucao, int capacidadeMaxima, CancellationToken ct)
    {
        // Sem esta guarda, a condicao "attribute_not_exists(ativas)" admitiria a 1a OS com contador ausente.
        if (capacidadeMaxima <= 0) return ResultadoGravacao.CapacidadeEsgotada;

        try
        {
            await TransacionarComRepeticaoAsync(() => new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = _tabela,
                            Item = MapeadorExecucaoDynamoDb.ParaItem(execucao),
                            ConditionExpression = "attribute_not_exists(osId)"
                        }
                    },
                    new TransactWriteItem
                    {
                        Update = new Update
                        {
                            TableName = _tabela,
                            Key = ChaveContador(execucao.Unidade),
                            UpdateExpression = "SET ativas = if_not_exists(ativas, :zero) + :um",
                            ConditionExpression = "attribute_not_exists(ativas) OR ativas < :max",
                            ExpressionAttributeValues = new()
                            {
                                [":zero"] = Numero(0),
                                [":um"] = Numero(1),
                                [":max"] = Numero(capacidadeMaxima)
                            }
                        }
                    }
                ]
            }, ct);
            return ResultadoGravacao.Gravada;
        }
        catch (TransactionCanceledException ex)
        {
            var razoes = ex.CancellationReasons ?? [];
            if (Falhou(razoes, 0)) return ResultadoGravacao.JaExistente;
            if (Falhou(razoes, 1)) return ResultadoGravacao.CapacidadeEsgotada;
            throw;
        }
    }

    public async Task<ResultadoGravacao> InserirRecusadaAsync(ExecucaoOs execucao, CancellationToken ct)
    {
        try
        {
            await _dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = _tabela,
                Item = MapeadorExecucaoDynamoDb.ParaItem(execucao),
                ConditionExpression = "attribute_not_exists(osId)"
            }, ct);
            return ResultadoGravacao.Gravada;
        }
        catch (ConditionalCheckFailedException)
        {
            return ResultadoGravacao.JaExistente;
        }
    }

    public async Task<ExecucaoOs?> ObterAsync(Guid ordemServicoId, CancellationToken ct)
    {
        var r = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _tabela,
            Key = new() { ["osId"] = new AttributeValue(ordemServicoId.ToString("D")) },
            ConsistentRead = true
        }, ct);
        return r.IsItemSet ? MapeadorExecucaoDynamoDb.ParaExecucao(r.Item) : null;
    }

    public async Task AtualizarAsync(ExecucaoOs execucao, CancellationToken ct)
    {
        var anterior = Numero(execucao.Versao - 1);
        var item = MapeadorExecucaoDynamoDb.ParaItem(execucao);

        if (execucao.Status != StatusExecucao.Finalizada)
        {
            try
            {
                await _dynamo.PutItemAsync(new PutItemRequest
                {
                    TableName = _tabela,
                    Item = item,
                    ConditionExpression = "versao = :anterior",
                    ExpressionAttributeValues = new() { [":anterior"] = anterior }
                }, ct);
            }
            catch (ConditionalCheckFailedException ex)
            {
                throw new ConflitoDeConcorrenciaException(
                    $"A execucao {execucao.OrdemServicoId} foi alterada por outra operacao.", ex);
            }
            return;
        }

        try
        {
            await TransacionarComRepeticaoAsync(() => new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = _tabela,
                            Item = item,
                            ConditionExpression = "versao = :anterior",
                            ExpressionAttributeValues = new() { [":anterior"] = anterior }
                        }
                    },
                    new TransactWriteItem
                    {
                        Update = new Update
                        {
                            TableName = _tabela,
                            Key = ChaveContador(execucao.Unidade),
                            UpdateExpression = "SET ativas = ativas - :um",
                            ConditionExpression = "ativas > :zero",
                            ExpressionAttributeValues = new() { [":um"] = Numero(1), [":zero"] = Numero(0) }
                        }
                    }
                ]
            }, ct);
        }
        catch (TransactionCanceledException ex) when (Falhou(ex.CancellationReasons ?? [], 0))
        {
            throw new ConflitoDeConcorrenciaException(
                $"A execucao {execucao.OrdemServicoId} foi alterada por outra operacao.", ex);
        }
    }

    public async Task<IReadOnlyList<ExecucaoOs>> ListarPorStatusAsync(StatusExecucao status, CancellationToken ct)
    {
        var resultado = new List<ExecucaoOs>();
        Dictionary<string, AttributeValue>? inicio = null;
        do
        {
            var r = await _dynamo.QueryAsync(new QueryRequest
            {
                TableName = _tabela,
                IndexName = EsquemaTabelaExecucao.IndiceStatus,
                KeyConditionExpression = "#s = :status",
                ExpressionAttributeNames = new() { ["#s"] = "status" },
                ExpressionAttributeValues = new() { [":status"] = new AttributeValue(status.ToString()) },
                ScanIndexForward = true,
                ExclusiveStartKey = inicio
            }, ct);
            resultado.AddRange(r.Items.Select(MapeadorExecucaoDynamoDb.ParaExecucao));
            inicio = r.LastEvaluatedKey is { Count: > 0 } ? r.LastEvaluatedKey : null;
        } while (inicio is not null);

        return resultado;
    }

    /// <remarks>
    /// O GSI e eventualmente consistente na AWS e o proprio item recem-gravado pode ainda nao aparecer;
    /// por isso conta apenas as OS estritamente anteriores e soma 1 (ver ADR-001).
    /// </remarks>
    public async Task<int> ObterPosicaoNaFilaAsync(ExecucaoOs execucao, CancellationToken ct)
    {
        var total = 0;
        Dictionary<string, AttributeValue>? inicio = null;
        do
        {
            var r = await _dynamo.QueryAsync(new QueryRequest
            {
                TableName = _tabela,
                IndexName = EsquemaTabelaExecucao.IndiceStatus,
                KeyConditionExpression = "#s = :status AND enfileiradaEm < :minha",
                FilterExpression = "unidade = :u",
                ExpressionAttributeNames = new() { ["#s"] = "status" },
                ExpressionAttributeValues = new()
                {
                    [":status"] = new AttributeValue(StatusExecucao.NaFila.ToString()),
                    [":minha"] = new AttributeValue(MapeadorExecucaoDynamoDb.Formatar(execucao.EnfileiradaEm)),
                    [":u"] = new AttributeValue(execucao.Unidade)
                },
                Select = Select.COUNT,
                ExclusiveStartKey = inicio
            }, ct);
            total += r.Count ?? 0;
            inicio = r.LastEvaluatedKey is { Count: > 0 } ? r.LastEvaluatedKey : null;
        } while (inicio is not null);

        return total + 1;
    }

    /// <summary>
    /// Repete a transacao enquanto alguma razao for TransactionConflict (outra transacao tocando o mesmo item,
    /// tipicamente o contador). Cada tentativa monta um request novo, sem reaproveitar ClientRequestToken.
    /// </summary>
    private async Task TransacionarComRepeticaoAsync(Func<TransactWriteItemsRequest> criarRequest, CancellationToken ct)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await _dynamo.TransactWriteItemsAsync(criarRequest(), ct);
                return;
            }
            catch (TransactionCanceledException ex)
                when (tentativa < MaxTentativasConflito
                      && (ex.CancellationReasons ?? []).Any(r => r.Code == TransactionConflict))
            {
                await Task.Delay(Random.Shared.Next(20, 101), ct);
            }
        }
    }

    private static bool Falhou(List<CancellationReason> razoes, int indice) =>
        !razoes.Any(r => r.Code == TransactionConflict)
        && razoes.Count > indice
        && razoes[indice].Code == ConditionalCheckFailed;

    private static Dictionary<string, AttributeValue> ChaveContador(string unidade) =>
        new() { ["osId"] = new AttributeValue(EsquemaTabelaExecucao.ChaveContador(unidade)) };

    private static AttributeValue Numero(int n) => new() { N = n.ToString(CultureInfo.InvariantCulture) };
}
