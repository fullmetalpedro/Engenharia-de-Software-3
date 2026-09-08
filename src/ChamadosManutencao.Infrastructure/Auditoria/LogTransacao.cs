namespace ChamadosManutencao.Infrastructure.Auditoria;

/// <summary>
/// Registro de auditoria exigido pela RNF0012: para toda operacao de escrita ficam gravados
/// data, hora, usuario responsavel, entidade, chave, operacao e o estado anterior e novo.
/// Nao e entidade de dominio, e por isso vive na infraestrutura.
/// </summary>
public sealed class LogTransacao
{
    public Guid Id { get; set; }

    public DateTimeOffset DataHora { get; set; }

    public Guid? UsuarioId { get; set; }

    public string Entidade { get; set; } = null!;

    public string ChavePrimaria { get; set; } = null!;

    /// <summary>Insercao, Alteracao ou Exclusao.</summary>
    public string Operacao { get; set; } = null!;

    public string? EstadoAnterior { get; set; }

    public string? EstadoNovo { get; set; }
}
