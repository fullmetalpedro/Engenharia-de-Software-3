using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Chamados;

/// <summary>
/// Transicao de status recusada pela maquina de estados do chamado (RN0034).
/// </summary>
public sealed class TransicaoDeStatusInvalidaException : ExcecaoDeDominio
{
    public TransicaoDeStatusInvalidaException(StatusChamado atual, StatusChamado novo)
        : base($"Transicao de status invalida: {atual} nao pode ir para {novo}.", "RN0034")
    {
        StatusAtual = atual;
        StatusPretendido = novo;
    }

    public StatusChamado StatusAtual { get; }

    public StatusChamado StatusPretendido { get; }
}
