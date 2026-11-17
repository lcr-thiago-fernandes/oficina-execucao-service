using MassTransit;

namespace Oficina.Execucao.Aplicacao.Contratos.Mensagens;

/// <summary>Item (servico ou peca) da ordem de servico a executar.</summary>
public sealed record ItemExecucaoMensagem(string Descricao, int Quantidade);

public enum StatusExecucaoMensagem { EmDiagnostico, EmReparo }
