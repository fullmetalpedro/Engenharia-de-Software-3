using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Chamados;

/// <summary>
/// Registro de uma mudanca de status do chamado. Requisitos: RF0050, RNF0012.
/// </summary>
public sealed class HistoricoStatus : Entidade
{
    public HistoricoStatus(
        Guid id,
        Guid chamadoId,
        StatusChamado? statusAnterior,
        StatusChamado statusNovo,
        DateTimeOffset dataHoraAlteracao,
        Guid usuarioResponsavelId,
        string? observacao)
        : base(id)
    {
        ChamadoId = chamadoId;
        StatusAnterior = statusAnterior;
        StatusNovo = statusNovo;
        DataHoraAlteracao = dataHoraAlteracao;
        UsuarioResponsavelId = usuarioResponsavelId;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
    }

    private HistoricoStatus()
    {
    }

    public Guid ChamadoId { get; private set; }

    /// <summary>Nulo apenas no registro de abertura do chamado.</summary>
    public StatusChamado? StatusAnterior { get; private set; }

    public StatusChamado StatusNovo { get; private set; }

    public DateTimeOffset DataHoraAlteracao { get; private set; }

    public Guid UsuarioResponsavelId { get; private set; }

    public string? Observacao { get; private set; }
}
