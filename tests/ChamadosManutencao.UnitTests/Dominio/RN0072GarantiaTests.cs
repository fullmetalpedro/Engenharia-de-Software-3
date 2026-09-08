using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0072: a garantia e acionavel em ate 90 dias corridos apos a conclusao, com a fatura
/// quitada e para o mesmo tipo de servico executado.
/// </summary>
[Trait("Requisito", "RN0072")]
public class RN0072GarantiaTests
{
    private static readonly Guid TipoServico = Construtor.Id(6);

    private static Garantia NovaGarantia() =>
        new(Construtor.Id(110), Construtor.Id(50), Construtor.Agora);

    [Fact]
    public void Prazo_padrao_da_garantia_e_de_noventa_dias()
    {
        var garantia = NovaGarantia();

        garantia.PrazoDias.ShouldBe(90);
        garantia.DataFim.ShouldBe(Construtor.Agora.AddDays(90));
        Garantia.PrazoPadraoEmDias.ShouldBe(90);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(89, true)]
    [InlineData(90, true)]
    [InlineData(91, false)]
    public void Vigencia_respeita_os_noventa_dias(int diasDepois, bool esperado)
    {
        var garantia = NovaGarantia();

        garantia.EstaVigente(Construtor.Agora.AddDays(diasDepois)).ShouldBe(esperado);
    }

    [Fact]
    public void Acionamento_dentro_das_tres_condicoes_e_permitido()
    {
        var garantia = NovaGarantia();

        garantia.PodeSerAcionada(
            Construtor.Agora.AddDays(30),
            faturaQuitada: true,
            TipoServico,
            TipoServico).ShouldBeTrue();
    }

    [Fact]
    public void Acionamento_fora_do_prazo_e_recusado()
    {
        var garantia = NovaGarantia();

        garantia.PodeSerAcionada(
            Construtor.Agora.AddDays(91),
            faturaQuitada: true,
            TipoServico,
            TipoServico).ShouldBeFalse();

        garantia.MotivoDeRecusa(
            Construtor.Agora.AddDays(91),
            true,
            TipoServico,
            TipoServico).ShouldNotBeNull().ShouldContain("venceu");
    }

    [Fact]
    public void Acionamento_com_fatura_em_aberto_e_recusado()
    {
        var garantia = NovaGarantia();

        garantia.PodeSerAcionada(
            Construtor.Agora.AddDays(10),
            faturaQuitada: false,
            TipoServico,
            TipoServico).ShouldBeFalse();

        garantia.MotivoDeRecusa(
            Construtor.Agora.AddDays(10),
            false,
            TipoServico,
            TipoServico).ShouldNotBeNull().ShouldContain("quitada");
    }

    [Fact]
    public void Acionamento_para_outro_tipo_de_servico_e_recusado()
    {
        var garantia = NovaGarantia();

        garantia.PodeSerAcionada(
            Construtor.Agora.AddDays(10),
            faturaQuitada: true,
            TipoServico,
            Construtor.Id(7)).ShouldBeFalse();
    }

    [Fact]
    public void Prazo_nao_positivo_e_recusado()
    {
        var excecao = Should.Throw<ExcecaoDeDominio>(
            () => new Garantia(Construtor.Id(111), Construtor.Id(50), Construtor.Agora, prazoDias: 0));

        excecao.Requisito.ShouldBe("RN0072");
    }

    [Fact]
    public void Chamado_de_garantia_nasce_vinculado_ao_original_e_com_urgencia_alta()
    {
        var gerador = Construtor.Gerador();
        var original = Construtor.Chamado(gerador: gerador);
        var garantia = NovaGarantia();

        var chamadoDeGarantia = Domain.Chamados.Chamado.AbrirParaGarantia(
            Construtor.Id(112),
            numero: 2,
            original.ClienteId,
            imovelId: Construtor.Id(100),
            tipoServicoId: TipoServico,
            "O mesmo defeito voltou a ocorrer.",
            Construtor.Agora.AddDays(20),
            original.Id,
            garantia.Id,
            gerador);

        chamadoDeGarantia.ChamadoDeGarantia.ShouldBeTrue();
        chamadoDeGarantia.ChamadoOriginalId.ShouldBe(original.Id);
        chamadoDeGarantia.GarantiaId.ShouldBe(garantia.Id);
        chamadoDeGarantia.Urgencia.ShouldBe(Domain.Enums.Urgencia.Alta);
    }
}
