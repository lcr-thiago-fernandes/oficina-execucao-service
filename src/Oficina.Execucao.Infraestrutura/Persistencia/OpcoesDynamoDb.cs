namespace Oficina.Execucao.Infraestrutura.Persistencia;

public sealed class OpcoesDynamoDb
{
    public const string Secao = "DynamoDb";

    public string NomeTabela { get; set; } = "oficina-execucao-local";
    public string Regiao { get; set; } = "us-east-1";

    /// <summary>Endpoint alternativo (DynamoDB Local / LocalStack). Vazio = AWS real com a cadeia padrao de credenciais (IRSA).</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>Cria a tabela na inicializacao se ausente (apenas desenvolvimento local; em hml/prd a tabela vem do Terraform).</summary>
    public bool CriarTabelaSeAusente { get; set; }
}
