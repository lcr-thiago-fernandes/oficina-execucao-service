using Oficina.Execucao.Aplicacao.Execucoes;

namespace Oficina.Execucao.Infraestrutura;

public sealed class RelogioSistema : IRelogio
{
    public DateTime AgoraUtc => DateTime.UtcNow;
}
