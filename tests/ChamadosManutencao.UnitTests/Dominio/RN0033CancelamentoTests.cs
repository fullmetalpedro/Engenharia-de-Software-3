using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0033: o cliente so cancela o chamado com status ABERTO ou AGENDADO. Depois do inicio do
/// atendimento, o cancelamento passa pelo administrador (decisao D06: EM ANALISE tambem e
/// cancelavel, mas apenas pelo administrador).
/// </summary>
[Trait("Requisito", "RN0033")]
public class RN0033CancelamentoTests
{
    [Theory]
    [InlineData(StatusChamado.Aberto, true)]
    [InlineData(StatusChamado.Agendado, true)]
    [InlineData(StatusChamado.EmAnalise, false)]
    [InlineData(StatusChamado.EmAtendimento, false)]
    [InlineData(StatusChamado.Concluido, false)]
    public void Pode_ser_cancelado_pelo_cliente_apenas_em_aberto_ou_agendado(
        StatusChamado status,
        bool esperado)
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.ChamadoNoStatus(status, gerador);

        chamado.PodeSerCancelado().ShouldBe(esperado);
    }

    [Fact]
    public void Cliente_cancela_chamado_aberto()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.ChamadoNoStatus(StatusChamado.Aberto, gerador);

        chamado.Cancelar(chamado.ClienteId, Construtor.Agora, gerador);

        chamado.Status.ShouldBe(StatusChamado.Cancelado);
    }

    [Fact]
    public void Cliente_nao_cancela_chamado_em_atendimento()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.ChamadoNoStatus(StatusChamado.EmAtendimento, gerador);

        var excecao = Should.Throw<ExcecaoDeDominio>(
            () => chamado.Cancelar(chamado.ClienteId, Construtor.Agora, gerador));

        excecao.Requisito.ShouldBe("RN0033");
        chamado.Status.ShouldBe(StatusChamado.EmAtendimento);
    }

    [Fact]
    public void Administrador_cancela_chamado_em_atendimento()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.ChamadoNoStatus(StatusChamado.EmAtendimento, gerador);

        chamado.CancelarPeloAdministrador(
            Construtor.Id(9),
            "Cliente solicitou o cancelamento por telefone.",
            Construtor.Agora,
            gerador);

        chamado.Status.ShouldBe(StatusChamado.Cancelado);
    }

    [Fact]
    public void Administrador_cancela_chamado_em_analise()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.ChamadoNoStatus(StatusChamado.EmAnalise, gerador);

        chamado.CancelarPeloAdministrador(
            Construtor.Id(9),
            "Duplicidade de chamado.",
            Construtor.Agora,
            gerador);

        chamado.Status.ShouldBe(StatusChamado.Cancelado);
    }
}
