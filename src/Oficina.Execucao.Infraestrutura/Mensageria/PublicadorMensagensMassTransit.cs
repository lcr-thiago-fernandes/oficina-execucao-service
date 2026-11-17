using MassTransit;
using Oficina.Execucao.Aplicacao.Execucoes;

namespace Oficina.Execucao.Infraestrutura.Mensageria;

/// <summary>Registrado como Scoped: dentro do consumidor o IPublishEndpoint e o ConsumeContext (preserva correlacao/trace).</summary>
public sealed class PublicadorMensagensMassTransit : IPublicadorMensagens
{
    private readonly IPublishEndpoint _publishEndpoint;

    public PublicadorMensagensMassTransit(IPublishEndpoint publishEndpoint) => _publishEndpoint = publishEndpoint;

    public Task PublicarAsync<T>(T mensagem, CancellationToken ct) where T : class =>
        _publishEndpoint.Publish(mensagem, ct);
}
