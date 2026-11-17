namespace Oficina.Execucao.Infraestrutura.Mensageria;

public sealed class OpcoesMensageria
{
    public const string Secao = "Mensageria";

    /// <summary>"AmazonSqs" (SQS/SNS) ou "InMemory".</summary>
    public string Transporte { get; set; } = "InMemory";

    /// <summary>Prefixo do ambiente em filas e topicos: local | hml | prd.</summary>
    public string PrefixoAmbiente { get; set; } = "local";

    public string Regiao { get; set; } = "us-east-1";

    /// <summary>Endpoint alternativo (LocalStack). Vazio = AWS real com a cadeia padrao de credenciais (IRSA).</summary>
    public string? ServiceUrl { get; set; }
}
