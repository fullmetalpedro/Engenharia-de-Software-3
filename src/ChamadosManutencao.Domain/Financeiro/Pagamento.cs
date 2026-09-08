using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Financeiro;

/// <summary>
/// Tentativa de pagamento de uma fatura. Requisitos: RF0084, RNF0061.
/// </summary>
public sealed class Pagamento : Entidade
{
    public Pagamento(
        Guid id,
        Guid faturaId,
        Guid formaPagamentoId,
        DateTimeOffset dataHoraPagamento,
        decimal valorPago,
        string identificadorTransacao,
        StatusPagamento status)
        : base(id)
    {
        const string requisito = "RF0084";

        FaturaId = faturaId;
        FormaPagamentoId = formaPagamentoId;
        DataHoraPagamento = dataHoraPagamento;
        ValorPago = Garantir.ValorNaoNegativo(valorPago, "valor pago", requisito);
        IdentificadorTransacao = Garantir.TextoComTamanhoMaximo(
            identificadorTransacao, 100, "identificador da transacao", requisito);
        Status = status;
    }

    private Pagamento()
    {
        IdentificadorTransacao = null!;
    }

    public Guid FaturaId { get; private set; }

    public Guid FormaPagamentoId { get; private set; }

    public DateTimeOffset DataHoraPagamento { get; private set; }

    public decimal ValorPago { get; private set; }

    /// <summary>Identificador devolvido pela operadora (RF0084).</summary>
    public string IdentificadorTransacao { get; private set; }

    public StatusPagamento Status { get; private set; }

    public void Estornar() => Status = StatusPagamento.Estornado;
}
