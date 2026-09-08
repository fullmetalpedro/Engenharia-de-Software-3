using ChamadosManutencao.Domain.Avaliacoes;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0052: o cliente tem 15 dias corridos apos a conclusao para avaliar. Depois disso,
/// a avaliacao nao pode mais ser registrada.
/// </summary>
[Trait("Requisito", "RN0052")]
public class RN0052PrazoDeAvaliacaoTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(14, true)]
    [InlineData(15, true)]
    [InlineData(16, false)]
    [InlineData(30, false)]
    public void Janela_de_avaliacao_dura_quinze_dias(int diasDepois, bool esperado)
    {
        var conclusao = Construtor.Agora;

        Avaliacao.DentroDoPrazo(conclusao, conclusao.AddDays(diasDepois)).ShouldBe(esperado);
    }

    [Fact]
    public void Prazo_da_regra_e_de_quinze_dias()
    {
        Avaliacao.PrazoParaAvaliarEmDias.ShouldBe(15);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Nota_entre_um_e_cinco_e_aceita(int nota)
    {
        var avaliacao = new Avaliacao(
            Construtor.Id(80),
            Construtor.Id(5),
            Construtor.Id(1),
            Construtor.Id(4),
            nota,
            "Atendimento pontual.",
            Construtor.Agora);

        avaliacao.Nota.ShouldBe(nota);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Nota_fora_da_faixa_e_recusada(int nota)
    {
        Should.Throw<ExcecaoDeDominio>(() => new Avaliacao(
            Construtor.Id(81),
            Construtor.Id(5),
            Construtor.Id(1),
            Construtor.Id(4),
            nota,
            null,
            Construtor.Agora));
    }

    [Fact]
    public void Avaliacao_so_pode_ser_respondida_uma_vez()
    {
        var avaliacao = new Avaliacao(
            Construtor.Id(82),
            Construtor.Id(5),
            Construtor.Id(1),
            Construtor.Id(4),
            4,
            "Bom servico.",
            Construtor.Agora);

        avaliacao.Responder(Construtor.Id(83), Construtor.Id(9), "Obrigado pelo retorno.", Construtor.Agora);

        Should.Throw<ExcecaoDeDominio>(() => avaliacao.Responder(
            Construtor.Id(84),
            Construtor.Id(9),
            "Resposta duplicada.",
            Construtor.Agora));
    }
}
