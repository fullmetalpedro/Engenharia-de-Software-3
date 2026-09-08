using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Eventos;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0051: ao concluir o atendimento, o sistema solicita a avaliacao ao cliente. O dominio
/// publica o evento AtendimentoConcluido; o notificador o consome (RNF0041).
/// </summary>
[Trait("Requisito", "RN0051")]
public class RN0051SolicitacaoDeAvaliacaoTests
{
    private static Atendimento NovoAtendimento() =>
        new(Construtor.Id(70), Construtor.Id(5), Construtor.Id(4), Construtor.Agora);

    [Fact]
    public void Conclusao_publica_o_evento_de_atendimento_concluido()
    {
        var atendimento = NovoAtendimento();

        atendimento.Concluir(
            "Compressor substituido e equipamento testado.",
            Construtor.Id(1),
            chamadoDeGarantia: false,
            Construtor.Agora.AddHours(3),
            Construtor.Gerador());

        var evento = atendimento.Eventos.OfType<AtendimentoConcluido>().ShouldHaveSingleItem();
        evento.ChamadoId.ShouldBe(Construtor.Id(5));
        evento.ClienteId.ShouldBe(Construtor.Id(1));
        evento.ChamadoDeGarantia.ShouldBeFalse();
        evento.OcorridoEm.ShouldBe(Construtor.Agora.AddHours(3));
    }

    [Fact]
    public void Conclusao_registra_relato_e_data_e_gera_garantia()
    {
        var atendimento = NovoAtendimento();

        var garantia = atendimento.Concluir(
            "Servico executado.",
            Construtor.Id(1),
            chamadoDeGarantia: false,
            Construtor.Agora.AddHours(3),
            Construtor.Gerador());

        atendimento.EstaConcluido.ShouldBeTrue();
        atendimento.RelatoTecnico.ShouldBe("Servico executado.");
        atendimento.DataHoraConclusao.ShouldBe(Construtor.Agora.AddHours(3));
        garantia.AtendimentoId.ShouldBe(atendimento.Id);
        atendimento.GarantiaId.ShouldBe(garantia.Id);
    }

    [Fact]
    public void Evento_de_chamado_de_garantia_sinaliza_que_nao_havera_fatura()
    {
        var atendimento = NovoAtendimento();

        atendimento.Concluir(
            "Retrabalho em garantia.",
            Construtor.Id(1),
            chamadoDeGarantia: true,
            Construtor.Agora.AddHours(2),
            Construtor.Gerador());

        atendimento.Eventos.OfType<AtendimentoConcluido>().Single()
            .ChamadoDeGarantia.ShouldBeTrue();
    }
}
