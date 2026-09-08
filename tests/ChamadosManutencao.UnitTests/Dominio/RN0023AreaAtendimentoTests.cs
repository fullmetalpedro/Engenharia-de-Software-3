using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0023: o tecnico so recebe chamado de imovel dentro de sua area de atendimento.
/// </summary>
[Trait("Requisito", "RN0023")]
public class RN0023AreaAtendimentoTests
{
    [Theory]
    [InlineData("04000000", true)]
    [InlineData("04567000", true)]
    [InlineData("05000000", true)]
    [InlineData("03999999", false)]
    [InlineData("05000001", false)]
    public void Faixa_de_cep_da_area_e_inclusiva_nos_extremos(string cep, bool esperado)
    {
        var tecnico = Construtor.Tecnico();

        tecnico.AtendeCep(cep).ShouldBe(esperado);
    }

    [Fact]
    public void Atribuicao_de_tecnico_fora_da_area_e_recusada()
    {
        var gerador = Construtor.Gerador();
        var categoria = Construtor.Categoria(id: Construtor.Id(20));
        var tecnico = Construtor.Tecnico(categoria);
        var chamado = Construtor.Chamado(gerador: gerador);

        var excecao = Should.Throw<ExcecaoDeDominio>(() => chamado.AtribuirTecnico(
            tecnico,
            categoria.Id,
            "09000000",
            Construtor.Id(9),
            Construtor.Agora,
            gerador));

        excecao.Requisito.ShouldBe("RN0023");
        chamado.TecnicoId.ShouldBeNull();
    }

    [Fact]
    public void Area_que_atende_o_cep_devolve_a_taxa_de_deslocamento()
    {
        var tecnico = Construtor.Tecnico();

        var area = tecnico.AreaQueAtende("04567000");

        area.ShouldNotBeNull();
        area.TaxaDeslocamento.ShouldBe(45m);
    }

    [Fact]
    public void Area_com_cep_inicial_maior_que_o_final_e_recusada()
    {
        Should.Throw<ExcecaoDeDominio>(() => Construtor.Area(
            Construtor.Id(4),
            cepInicial: "05000000",
            cepFinal: "04000000"));
    }
}
