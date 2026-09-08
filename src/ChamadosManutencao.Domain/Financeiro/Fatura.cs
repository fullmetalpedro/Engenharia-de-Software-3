using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Eventos;

namespace ChamadosManutencao.Domain.Financeiro;

/// <summary>
/// Fatura do atendimento concluido. Raiz do agregado que guarda os pagamentos.
/// Requisitos: RF0082, RF0083, RF0084, RN0071.
/// </summary>
public sealed class Fatura : RaizDeAgregado
{
    /// <summary>Prazo padrao de vencimento, em dias corridos apos a emissao.</summary>
    public const int PrazoDeVencimentoEmDias = 15;

    private readonly List<Pagamento> _pagamentos = [];

    public Fatura(
        Guid id,
        long numero,
        Guid atendimentoId,
        Guid clienteId,
        Guid chamadoId,
        DateTimeOffset dataEmissao,
        decimal valorPecas,
        decimal valorMaoObra,
        decimal taxaDeslocamento,
        int prazoDeVencimentoEmDias = PrazoDeVencimentoEmDias)
        : base(id)
    {
        const string requisito = "RN0071";

        Numero = numero;
        AtendimentoId = atendimentoId;
        ClienteId = clienteId;
        ChamadoId = chamadoId;
        DataEmissao = dataEmissao;
        DataVencimento = dataEmissao.AddDays(prazoDeVencimentoEmDias);
        ValorPecas = Garantir.ValorNaoNegativo(valorPecas, "valor de pecas", requisito);
        ValorMaoObra = Garantir.ValorNaoNegativo(valorMaoObra, "valor de mao de obra", requisito);
        TaxaDeslocamento = Garantir.ValorNaoNegativo(taxaDeslocamento, "taxa de deslocamento", requisito);
        Status = StatusFatura.Emitida;
        ValorTotal = CalcularValorTotal();
    }

    private Fatura()
    {
    }

    /// <summary>Numero sequencial unico vindo da sequence seq_numero_fatura.</summary>
    public long Numero { get; private set; }

    public Guid AtendimentoId { get; private set; }

    public Guid ClienteId { get; private set; }

    public Guid ChamadoId { get; private set; }

    public DateTimeOffset DataEmissao { get; private set; }

    public DateTimeOffset DataVencimento { get; private set; }

    public decimal ValorPecas { get; private set; }

    public decimal ValorMaoObra { get; private set; }

    public decimal TaxaDeslocamento { get; private set; }

    public decimal ValorTotal { get; private set; }

    public StatusFatura Status { get; private set; }

    public IReadOnlyCollection<Pagamento> Pagamentos => _pagamentos.AsReadOnly();

    /// <summary>RN0071: pecas + mao de obra do orcamento aprovado + taxa de deslocamento da area.</summary>
    public decimal CalcularValorTotal() => ValorPecas + ValorMaoObra + TaxaDeslocamento;

    public bool EstaQuitada() => Status == StatusFatura.Paga;

    /// <summary>Job de vencimento: fatura emitida cujo vencimento passou vira Vencida.</summary>
    public bool RegistrarVencimento(DateTimeOffset agora)
    {
        if (Status != StatusFatura.Emitida || agora <= DataVencimento)
        {
            return false;
        }

        Status = StatusFatura.Vencida;
        RegistrarEvento(new FaturaVencida(Id, Numero, ClienteId, agora));
        return true;
    }

    /// <summary>RF0084: registra a tentativa de pagamento devolvida pelo gateway.</summary>
    public void RegistrarPagamento(Pagamento pagamento)
    {
        Garantir.NaoNulo(pagamento, "pagamento", "RF0084");

        if (Status is StatusFatura.Paga or StatusFatura.Cancelada)
        {
            throw new ExcecaoDeDominio(
                $"Nao e possivel pagar uma fatura com status {Status}.",
                "RF0084");
        }

        if (pagamento.FaturaId != Id)
        {
            throw new ExcecaoDeDominio("O pagamento pertence a outra fatura.", "RF0084");
        }

        if (pagamento.ValorPago != ValorTotal)
        {
            throw new ExcecaoDeDominio(
                "O valor pago deve corresponder ao valor total da fatura.",
                "RF0084");
        }

        _pagamentos.Add(pagamento);

        if (pagamento.Status == StatusPagamento.Aprovado)
        {
            Status = StatusFatura.Paga;
        }
    }

    public void Cancelar() => Status = StatusFatura.Cancelada;
}
