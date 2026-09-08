using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Atendimentos;

/// <summary>
/// Item de peca ou de mao de obra do orcamento. Requisitos: RF0055, RN0071.
/// </summary>
public sealed class ItemOrcamento : Entidade
{
    public ItemOrcamento(
        Guid id,
        Guid orcamentoId,
        string descricao,
        TipoItem tipo,
        int quantidade,
        decimal valorUnitario)
        : base(id)
    {
        const string requisito = "RF0055";

        OrcamentoId = orcamentoId;
        Descricao = Garantir.TextoComTamanhoMaximo(descricao, 300, "descricao do item", requisito);
        Tipo = tipo;

        if (quantidade <= 0)
        {
            throw new ExcecaoDeDominio("A quantidade do item deve ser maior que zero.", requisito);
        }

        Quantidade = quantidade;
        ValorUnitario = Garantir.ValorNaoNegativo(valorUnitario, "valor unitario", requisito);
    }

    private ItemOrcamento()
    {
        Descricao = null!;
    }

    public Guid OrcamentoId { get; private set; }

    public string Descricao { get; private set; }

    public TipoItem Tipo { get; private set; }

    public int Quantidade { get; private set; }

    public decimal ValorUnitario { get; private set; }

    public decimal CalcularSubtotal() => Quantidade * ValorUnitario;
}
