using System.ComponentModel.DataAnnotations;

namespace Oficina.Execucao.Api.Modelos;

/// <summary>Corpo opcional das transicoes de execucao.</summary>
public sealed record ObservacaoRequest([MaxLength(500)] string? Observacao);
