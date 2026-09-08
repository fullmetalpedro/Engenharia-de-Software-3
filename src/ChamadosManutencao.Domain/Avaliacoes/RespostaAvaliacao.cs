using ChamadosManutencao.Domain.Common;

namespace ChamadosManutencao.Domain.Avaliacoes;

/// <summary>
/// Resposta publica do administrador a uma avaliacao. Requisito: RF0063.
/// </summary>
public sealed class RespostaAvaliacao : Entidade
{
    public RespostaAvaliacao(
        Guid id,
        Guid avaliacaoId,
        string texto,
        DateTimeOffset dataHoraResposta,
        Guid administradorId)
        : base(id)
    {
        AvaliacaoId = avaliacaoId;
        Texto = Garantir.TextoComTamanhoMaximo(texto, 2000, "texto da resposta", "RF0063");
        DataHoraResposta = dataHoraResposta;
        AdministradorId = administradorId;
    }

    private RespostaAvaliacao()
    {
        Texto = null!;
    }

    public Guid AvaliacaoId { get; private set; }

    public string Texto { get; private set; }

    public DateTimeOffset DataHoraResposta { get; private set; }

    public Guid AdministradorId { get; private set; }
}
