using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0035: reabertura em ate 7 dias corridos apos a conclusao, voltando para EM ANALISE e
/// mantendo o vinculo com o chamado original.
/// </summary>
[Trait("Requisito", "RN0035")]
public class RN0035ReaberturaTests
{
    [Fact]
    public void Reabertura_dentro_do_prazo_volta_para_em_analise()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.ChamadoNoStatus(StatusChamado.Concluido, gerador);
        var conclusao = Construtor.Agora;

        chamado.SolicitarReabertura(
            chamado.ClienteId,
            conclusao,
            conclusao.AddDays(6),
            gerador);

        chamado.Status.ShouldBe(StatusChamado.EmAnalise);
    }

    [Fact]
    public void Reabertura_no_limite_de_sete_dias_e_aceita()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.ChamadoNoStatus(StatusChamado.Concluido, gerador);
        var conclusao = Construtor.Agora;

        chamado.SolicitarReabertura(
            chamado.ClienteId,
            conclusao,
            conclusao.AddDays(7),
            gerador);

        chamado.Status.ShouldBe(StatusChamado.EmAnalise);
    }

    [Fact]
    public void Reabertura_apos_o_prazo_e_recusada()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.ChamadoNoStatus(StatusChamado.Concluido, gerador);
        var conclusao = Construtor.Agora;

        var excecao = Should.Throw<ExcecaoDeDominio>(() => chamado.SolicitarReabertura(
            chamado.ClienteId,
            conclusao,
            conclusao.AddDays(7).AddSeconds(1),
            gerador));

        excecao.Requisito.ShouldBe("RN0035");
        chamado.Status.ShouldBe(StatusChamado.Concluido);
    }

    [Fact]
    public void Reabertura_de_chamado_nao_concluido_e_recusada()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.ChamadoNoStatus(StatusChamado.Agendado, gerador);

        var excecao = Should.Throw<ExcecaoDeDominio>(() => chamado.SolicitarReabertura(
            chamado.ClienteId,
            Construtor.Agora,
            Construtor.Agora,
            gerador));

        excecao.Requisito.ShouldBe("RN0035");
    }

    [Fact]
    public void Reabertura_grava_o_historico_com_a_observacao_da_regra()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.ChamadoNoStatus(StatusChamado.Concluido, gerador);

        chamado.SolicitarReabertura(
            chamado.ClienteId,
            Construtor.Agora,
            Construtor.Agora.AddDays(1),
            gerador);

        var ultimo = chamado.HistoricoStatus.Last();
        ultimo.StatusAnterior.ShouldBe(StatusChamado.Concluido);
        ultimo.StatusNovo.ShouldBe(StatusChamado.EmAnalise);
        ultimo.Observacao.ShouldNotBeNull();
    }
}
