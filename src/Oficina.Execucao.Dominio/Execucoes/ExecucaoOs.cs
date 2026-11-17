namespace Oficina.Execucao.Dominio.Execucoes;

public sealed class ExecucaoOs
{
    public Guid OrdemServicoId { get; private set; }
    public int NumeroOs { get; private set; }
    public string Unidade { get; private set; } = string.Empty;
    public IReadOnlyList<ItemExecucao> Servicos { get; private set; } = [];
    public IReadOnlyList<ItemExecucao> Pecas { get; private set; } = [];
    public StatusExecucao Status { get; private set; }
    public DateTime EnfileiradaEm { get; private set; }
    public DateTime? DiagnosticoIniciadoEm { get; private set; }
    public DateTime? ReparoIniciadoEm { get; private set; }
    public DateTime? FinalizadaEm { get; private set; }
    public string? Observacao { get; private set; }
    public string? MotivoRecusa { get; private set; }
    public int Versao { get; private set; }

    public bool Ativa => Status is StatusExecucao.NaFila or StatusExecucao.EmDiagnostico or StatusExecucao.EmReparo;

    public int? DuracaoMinutos =>
        FinalizadaEm is { } fim && DiagnosticoIniciadoEm is { } inicio
            ? (int)Math.Floor((fim - inicio).TotalMinutes)
            : null;

    private ExecucaoOs() { }

    public static ExecucaoOs Enfileirar(Guid ordemServicoId, int numeroOs, string unidade,
        IEnumerable<ItemExecucao>? servicos, IEnumerable<ItemExecucao>? pecas, DateTime agoraUtc)
    {
        ExigirUtc(agoraUtc);
        if (ordemServicoId == Guid.Empty)
            throw new ExecucaoInvalidaException("Identificador da ordem de servico e obrigatorio.");
        if (numeroOs <= 0)
            throw new ExecucaoInvalidaException("Numero da OS deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(unidade))
            throw new ExecucaoInvalidaException("Unidade e obrigatoria.");

        return new ExecucaoOs
        {
            OrdemServicoId = ordemServicoId,
            NumeroOs = numeroOs,
            Unidade = unidade.Trim(),
            Servicos = servicos?.ToList() ?? [],
            Pecas = pecas?.ToList() ?? [],
            Status = StatusExecucao.NaFila,
            EnfileiradaEm = agoraUtc,
            Versao = 1
        };
    }

    public static ExecucaoOs Recusar(Guid ordemServicoId, int numeroOs, string unidade, string motivo, DateTime agoraUtc)
    {
        ExigirUtc(agoraUtc);
        if (ordemServicoId == Guid.Empty)
            throw new ExecucaoInvalidaException("Identificador da ordem de servico e obrigatorio.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ExecucaoInvalidaException("Motivo da recusa e obrigatorio.");

        return new ExecucaoOs
        {
            OrdemServicoId = ordemServicoId,
            NumeroOs = numeroOs,
            Unidade = unidade?.Trim() ?? string.Empty,
            Status = StatusExecucao.Recusada,
            EnfileiradaEm = agoraUtc,
            MotivoRecusa = motivo,
            Versao = 1
        };
    }

    public static ExecucaoOs Reconstituir(Guid ordemServicoId, int numeroOs, string unidade,
        IReadOnlyList<ItemExecucao> servicos, IReadOnlyList<ItemExecucao> pecas, StatusExecucao status,
        DateTime enfileiradaEm, DateTime? diagnosticoIniciadoEm, DateTime? reparoIniciadoEm, DateTime? finalizadaEm,
        string? observacao, string? motivoRecusa, int versao) =>
        new()
        {
            OrdemServicoId = ordemServicoId,
            NumeroOs = numeroOs,
            Unidade = unidade,
            Servicos = servicos,
            Pecas = pecas,
            Status = status,
            EnfileiradaEm = enfileiradaEm,
            DiagnosticoIniciadoEm = diagnosticoIniciadoEm,
            ReparoIniciadoEm = reparoIniciadoEm,
            FinalizadaEm = finalizadaEm,
            Observacao = observacao,
            MotivoRecusa = motivoRecusa,
            Versao = versao
        };

    public bool IniciarDiagnostico(string? observacao, DateTime agoraUtc) =>
        Transitar(StatusExecucao.NaFila, StatusExecucao.EmDiagnostico, observacao, agoraUtc,
            () => DiagnosticoIniciadoEm = agoraUtc);

    public bool IniciarReparo(string? observacao, DateTime agoraUtc) =>
        Transitar(StatusExecucao.EmDiagnostico, StatusExecucao.EmReparo, observacao, agoraUtc,
            () => ReparoIniciadoEm = agoraUtc);

    public bool Finalizar(string? observacao, DateTime agoraUtc) =>
        Transitar(StatusExecucao.EmReparo, StatusExecucao.Finalizada, observacao, agoraUtc,
            () => FinalizadaEm = agoraUtc);

    private bool Transitar(StatusExecucao origem, StatusExecucao destino, string? observacao, DateTime agoraUtc,
        Action registrarTimestamp)
    {
        ExigirUtc(agoraUtc);
        if (Status == destino) return false;
        if (Status != origem) throw new TransicaoExecucaoInvalidaException(Status, destino);

        Status = destino;
        registrarTimestamp();
        if (observacao is not null) Observacao = observacao;
        Versao++;
        return true;
    }

    private static void ExigirUtc(DateTime valor)
    {
        if (valor.Kind != DateTimeKind.Utc)
            throw new ArgumentException("O horario deve ser UTC.", nameof(valor));
    }
}
