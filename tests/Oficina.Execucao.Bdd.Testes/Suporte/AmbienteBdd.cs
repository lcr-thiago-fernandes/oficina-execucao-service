using Oficina.Execucao.Api.Testes;

namespace Oficina.Execucao.Bdd.Testes.Suporte;

/// <summary>
/// Contexto do cenario: uma <see cref="ApiFactory"/> por cenario sobre o container DynamoDB unico da execucao
/// (<see cref="Hooks"/>). ApiFactory, DynamoDbApiFixture e GeradorTokenDeTeste sao compartilhados com
/// Oficina.Execucao.Api.Testes por link de arquivo (sem duplicar a montagem da API).
/// </summary>
public sealed class AmbienteBdd : IAsyncDisposable
{
    public static DynamoDbApiFixture? Dynamo { get; set; }

    private readonly Dictionary<int, Guid> _ordens = [];
    private ApiFactory? _factory;

    public ApiFactory Factory => _factory ?? throw new InvalidOperationException("Defina a capacidade da unidade no passo Dado.");

    public int? UltimaOs { get; set; }

    public void Iniciar(int capacidade) =>
        _factory = new ApiFactory(Dynamo ?? throw new InvalidOperationException("Container nao iniciado."), capacidade);

    /// <summary>O OrdemServicoId de cada numero de OS e um Guid gerado e guardado no cenario.</summary>
    public Guid IdDa(int numeroOs)
    {
        if (!_ordens.TryGetValue(numeroOs, out var id)) _ordens[numeroOs] = id = Guid.NewGuid();
        return id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
    }
}
