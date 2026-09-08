using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Financeiro;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0071: a fatura e a soma das pecas e da mao de obra do orcamento aprovado, mais a taxa de
/// deslocamento da area de atendimento. Chamado de garantia nao gera fatura.
/// </summary>
[Trait("Requisito", "RN0071")]
public class RN0071ComposicaoDaFaturaTests
{
    private static Orcamento OrcamentoAprovado()
    {
        var orcamentoId = Construtor.Id(90);

        var orcamento = new Orcamento(
            orcamentoId,
            Construtor.Id(50),
            Construtor.Agora,
            [
                new ItemOrcamento(Construtor.Id(91), orcamentoId, "Compressor", TipoItem.Peca, 1, 800m),
                new ItemOrcamento(Construtor.Id(92), orcamentoId, "Gas refrigerante", TipoItem.Peca, 2, 60m),
                new ItemOrcamento(Construtor.Id(93), orcamentoId, "Mao de obra", TipoItem.MaoDeObra, 3, 100m)
            ]);

        orcamento.Aprovar(Construtor.Agora.AddHours(2));

        return orcamento;
    }

    [Fact]
    public void Orcamento_separa_pecas_de_mao_de_obra()
    {
        var orcamento = OrcamentoAprovado();

        orcamento.CalcularValorPecas().ShouldBe(920m);
        orcamento.CalcularValorMaoObra().ShouldBe(300m);
        orcamento.ValorTotal().ShouldBe(1220m);
    }

    [Fact]
    public void Fatura_soma_pecas_mao_de_obra_e_taxa_de_deslocamento()
    {
        var orcamento = OrcamentoAprovado();

        var fatura = new Fatura(
            Construtor.Id(94),
            numero: 1,
            Construtor.Id(50),
            Construtor.Id(1),
            Construtor.Id(5),
            Construtor.Agora,
            orcamento.CalcularValorPecas(),
            orcamento.CalcularValorMaoObra(),
            taxaDeslocamento: 45m);

        fatura.ValorTotal.ShouldBe(1265m);
        fatura.CalcularValorTotal().ShouldBe(1265m);
        fatura.Status.ShouldBe(StatusFatura.Emitida);
    }

    [Fact]
    public void Fatura_vence_quinze_dias_apos_a_emissao()
    {
        var fatura = new Fatura(
            Construtor.Id(95),
            2,
            Construtor.Id(50),
            Construtor.Id(1),
            Construtor.Id(5),
            Construtor.Agora,
            100m,
            50m,
            45m);

        fatura.DataVencimento.ShouldBe(Construtor.Agora.AddDays(15));
    }

    [Fact]
    public void Valor_negativo_na_composicao_e_recusado()
    {
        var excecao = Should.Throw<ExcecaoDeDominio>(() => new Fatura(
            Construtor.Id(96),
            3,
            Construtor.Id(50),
            Construtor.Id(1),
            Construtor.Id(5),
            Construtor.Agora,
            valorPecas: -1m,
            valorMaoObra: 0m,
            taxaDeslocamento: 0m));

        excecao.Requisito.ShouldBe("RN0071");
    }

    [Fact]
    public void Pagamento_aprovado_quita_a_fatura()
    {
        var fatura = new Fatura(
            Construtor.Id(97),
            4,
            Construtor.Id(50),
            Construtor.Id(1),
            Construtor.Id(5),
            Construtor.Agora,
            100m,
            50m,
            45m);

        fatura.RegistrarPagamento(new Pagamento(
            Construtor.Id(98),
            fatura.Id,
            Construtor.Id(99),
            Construtor.Agora.AddDays(1),
            fatura.ValorTotal,
            "TRX-0001",
            StatusPagamento.Aprovado));

        fatura.EstaQuitada().ShouldBeTrue();
    }

    [Fact]
    public void Pagamento_recusado_nao_quita_a_fatura()
    {
        var fatura = new Fatura(
            Construtor.Id(100),
            5,
            Construtor.Id(50),
            Construtor.Id(1),
            Construtor.Id(5),
            Construtor.Agora,
            100m,
            50m,
            45m);

        fatura.RegistrarPagamento(new Pagamento(
            Construtor.Id(101),
            fatura.Id,
            Construtor.Id(99),
            Construtor.Agora.AddDays(1),
            fatura.ValorTotal,
            "TRX-0002",
            StatusPagamento.Recusado));

        fatura.EstaQuitada().ShouldBeFalse();
        fatura.Status.ShouldBe(StatusFatura.Emitida);
    }

    [Fact]
    public void Pagamento_com_valor_diferente_do_total_e_recusado()
    {
        var fatura = new Fatura(
            Construtor.Id(102),
            6,
            Construtor.Id(50),
            Construtor.Id(1),
            Construtor.Id(5),
            Construtor.Agora,
            100m,
            50m,
            45m);

        Should.Throw<ExcecaoDeDominio>(() => fatura.RegistrarPagamento(new Pagamento(
            Construtor.Id(103),
            fatura.Id,
            Construtor.Id(99),
            Construtor.Agora,
            10m,
            "TRX-0003",
            StatusPagamento.Aprovado)));
    }

    [Fact]
    public void Fatura_emitida_vencida_muda_de_status_e_publica_evento()
    {
        var fatura = new Fatura(
            Construtor.Id(104),
            7,
            Construtor.Id(50),
            Construtor.Id(1),
            Construtor.Id(5),
            Construtor.Agora,
            100m,
            50m,
            45m);

        fatura.RegistrarVencimento(Construtor.Agora.AddDays(16)).ShouldBeTrue();

        fatura.Status.ShouldBe(StatusFatura.Vencida);
        fatura.Eventos.Count.ShouldBe(1);
    }
}
