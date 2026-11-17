using System.Text.RegularExpressions;

namespace Oficina.Execucao.Infraestrutura.Mensageria;

/// <summary>Nomes de filas e topicos (spec §5). Todos casam com o padrao <c>${amb}-oficina-*</c> do IAM.</summary>
public static partial class NomesMensageria
{
    public const int TamanhoMaximoFilaSqs = 80;

    public static string FilaComandos(string prefixo) => $"{prefixo}-oficina-execucao-comandos";

    public static string Topico(string prefixo, string nome) => $"{prefixo}-oficina-saga-{nome}";

    /// <summary>
    /// Fila do bus (temporaria, auto-delete): uma por replica, sufixada pela instancia (nome do pod),
    /// em minusculas, caracteres fora de [a-z0-9-_] trocados por '-' e truncada no limite do SQS.
    /// </summary>
    public static string FilaBus(string prefixo, string instancia)
    {
        var nome = $"{prefixo}-oficina-execucao-bus-{CaractereInvalido().Replace(instancia.ToLowerInvariant(), "-")}";
        return nome.Length > TamanhoMaximoFilaSqs ? nome[..TamanhoMaximoFilaSqs] : nome;
    }

    /// <summary>HOSTNAME (nome do pod no Kubernetes); sem ele, o nome da maquina.</summary>
    public static string InstanciaAtual() =>
        Environment.GetEnvironmentVariable("HOSTNAME") is { } hostname && !string.IsNullOrWhiteSpace(hostname)
            ? hostname
            : Environment.MachineName;

    [GeneratedRegex("[^a-z0-9_-]")]
    private static partial Regex CaractereInvalido();
}
