using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Oficina.Execucao.Infraestrutura.Persistencia;

/// <summary>Esquema da tabela unica. Deve permanecer identico a terraform/dynamodb.tf.</summary>
public static class EsquemaTabelaExecucao
{
    public const string ChaveParticao = "osId";
    public const string IndiceStatus = "status-enfileiradaEm";

    public static string ChaveContador(string unidade) => $"UNIDADE#{unidade}";

    public static CreateTableRequest CriarRequest(string nomeTabela) => new()
    {
        TableName = nomeTabela,
        BillingMode = BillingMode.PAY_PER_REQUEST,
        AttributeDefinitions =
        [
            new AttributeDefinition(ChaveParticao, ScalarAttributeType.S),
            new AttributeDefinition("status", ScalarAttributeType.S),
            new AttributeDefinition("enfileiradaEm", ScalarAttributeType.S)
        ],
        KeySchema = [new KeySchemaElement(ChaveParticao, KeyType.HASH)],
        GlobalSecondaryIndexes =
        [
            new GlobalSecondaryIndex
            {
                IndexName = IndiceStatus,
                KeySchema =
                [
                    new KeySchemaElement("status", KeyType.HASH),
                    new KeySchemaElement("enfileiradaEm", KeyType.RANGE)
                ],
                Projection = new Projection { ProjectionType = ProjectionType.ALL }
            }
        ]
    };
}
