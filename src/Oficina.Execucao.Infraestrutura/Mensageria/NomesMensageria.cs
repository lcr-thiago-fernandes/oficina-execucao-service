namespace Oficina.Execucao.Infraestrutura.Mensageria;

/// <summary>Nomes de filas e topicos (spec §5). Todos casam com o padrao <c>${amb}-oficina-*</c> do IAM.</summary>
public static class NomesMensageria
{
    public static string FilaComandos(string prefixo) => $"{prefixo}-oficina-execucao-comandos";

    public static string Topico(string prefixo, string nome) => $"{prefixo}-oficina-saga-{nome}";

    public static string FilaBus(string prefixo) => $"{prefixo}-oficina-execucao-bus";
}
