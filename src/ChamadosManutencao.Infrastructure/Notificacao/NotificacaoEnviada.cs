namespace ChamadosManutencao.Infrastructure.Notificacao;

/// <summary>
/// Notificacao disparada pelo sistema (RNF0041). Persistir o envio permite auditar e trocar o
/// canal (log, SMTP, push) sem tocar no dominio.
/// </summary>
public sealed class NotificacaoEnviada
{
    public Guid Id { get; set; }

    public DateTimeOffset DataHora { get; set; }

    public Guid DestinatarioId { get; set; }

    public string Canal { get; set; } = null!;

    public string Assunto { get; set; } = null!;

    public string Corpo { get; set; } = null!;

    /// <summary>Tipo do objeto referenciado (Chamado, Fatura).</summary>
    public string ReferenciaTipo { get; set; } = null!;

    public Guid ReferenciaId { get; set; }
}
