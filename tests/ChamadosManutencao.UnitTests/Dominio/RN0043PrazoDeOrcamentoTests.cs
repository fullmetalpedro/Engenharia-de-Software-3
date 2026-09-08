using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0043: o cliente tem 48 horas para aprovar ou recusar o orcamento. Expirado o prazo,
/// o orcamento vira Expirado e o chamado e cancelado (o cancelamento fica no job).
/// </summary>
[Trait("Requisito", "RN0043")]
public class RN0043PrazoDeOrcamentoTests
{
    private static Orcamento NovoOrcamento() =>
        new(
            Construtor.Id(60),
            Construtor.Id(50),
            Construtor.Agora,
            [new ItemOrcamento(Construtor.Id(61), Construtor.Id(60), "Disjuntor", TipoItem.Peca, 2, 75m)]);

    [Fact]
    public void Prazo_de_aprovacao_e_de_48_horas_apos_o_registro()
    {
        var orcamento = NovoOrcamento();

        orcamento.PrazoAprovacao.ShouldBe(Construtor.Agora.AddHours(48));
        Orcamento.PrazoDeAprovacaoEmHoras.ShouldBe(48);
    }

    [Fact]
    public void Aprovacao_dentro_do_prazo_e_aceita()
    {
        var orcamento = NovoOrcamento();

        orcamento.Aprovar(Construtor.Agora.AddHours(47));

        orcamento.Status.ShouldBe(StatusOrcamento.Aprovado);
    }

    [Fact]
    public void Aprovacao_apos_o_prazo_e_recusada()
    {
        var orcamento = NovoOrcamento();

        var excecao = Should.Throw<ExcecaoDeDominio>(
            () => orcamento.Aprovar(Construtor.Agora.AddHours(48).AddMinutes(1)));

        excecao.Requisito.ShouldBe("RN0043");
        orcamento.Status.ShouldBe(StatusOrcamento.Pendente);
    }

    [Fact]
    public void Expiracao_apos_o_prazo_marca_o_orcamento_como_expirado()
    {
        var orcamento = NovoOrcamento();

        orcamento.EstaVencido(Construtor.Agora.AddHours(49)).ShouldBeTrue();

        orcamento.Expirar(Construtor.Agora.AddHours(49));

        orcamento.Status.ShouldBe(StatusOrcamento.Expirado);
    }

    [Fact]
    public void Expiracao_antes_do_prazo_e_recusada()
    {
        var orcamento = NovoOrcamento();

        var excecao = Should.Throw<ExcecaoDeDominio>(
            () => orcamento.Expirar(Construtor.Agora.AddHours(10)));

        excecao.Requisito.ShouldBe("RN0043");
    }

    [Fact]
    public void Atendimento_com_orcamento_pendente_nao_pode_ser_concluido()
    {
        var atendimento = new Atendimento(
            Construtor.Id(50),
            Construtor.Id(5),
            Construtor.Id(4),
            Construtor.Agora);

        atendimento.RegistrarOrcamento(new Orcamento(
            Construtor.Id(62),
            atendimento.Id,
            Construtor.Agora,
            [new ItemOrcamento(Construtor.Id(63), Construtor.Id(62), "Disjuntor", TipoItem.Peca, 1, 75m)]));

        var excecao = Should.Throw<ExcecaoDeDominio>(() => atendimento.Concluir(
            "Servico executado.",
            Construtor.Id(1),
            chamadoDeGarantia: false,
            Construtor.Agora.AddHours(2),
            Construtor.Gerador()));

        excecao.Requisito.ShouldBe("RN0043");
    }
}
