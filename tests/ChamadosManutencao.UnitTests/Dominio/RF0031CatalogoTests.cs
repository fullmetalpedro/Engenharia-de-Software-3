using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RF0031, RF0032 e RF0033: catalogo de categorias e tipos de servico, incluindo as marcacoes
/// que alimentam a RN0031 (categoria de risco) e a RN0032 (exige foto).
/// </summary>
[Trait("Requisito", "RF0031")]
public class RF0031CatalogoTests
{
    [Fact]
    public void Categoria_nasce_ativa_com_as_marcacoes_informadas()
    {
        var categoria = Construtor.Categoria(categoriaDeRisco: true, exigeFoto: true);

        categoria.Ativa.ShouldBeTrue();
        categoria.CategoriaDeRisco.ShouldBeTrue();
        categoria.ExigeFoto.ShouldBeTrue();
        categoria.TiposServico.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", "descricao valida")]
    [InlineData("Eletrica", "")]
    public void Categoria_sem_nome_ou_sem_descricao_e_recusada(string nome, string descricao)
    {
        Should.Throw<ExcecaoDeDominio>(() => new Domain.Catalogo.CategoriaServico(
            Construtor.Id(500),
            nome,
            descricao,
            exigeFoto: false,
            categoriaDeRisco: false));
    }

    [Fact]
    public void Alterar_categoria_atualiza_nome_descricao_e_marcacoes()
    {
        var categoria = Construtor.Categoria();

        categoria.Alterar("Hidraulica", "Servicos hidraulicos", exigeFoto: true, categoriaDeRisco: true);

        categoria.Nome.ShouldBe("Hidraulica");
        categoria.Descricao.ShouldBe("Servicos hidraulicos");
        categoria.ExigeFoto.ShouldBeTrue();
        categoria.CategoriaDeRisco.ShouldBeTrue();
    }

    [Fact]
    public void Categoria_pode_ser_inativada_e_reativada()
    {
        var categoria = Construtor.Categoria();

        categoria.Inativar();
        categoria.Ativa.ShouldBeFalse();

        categoria.Ativar();
        categoria.Ativa.ShouldBeTrue();
    }

    [Fact]
    public void Tipo_de_servico_nasce_ativo_e_vinculado_a_categoria()
    {
        var categoria = Construtor.Categoria();

        var tipo = categoria.AdicionarTipoServico(
            Construtor.Id(501),
            "Troca de disjuntor",
            "Substituicao de disjuntor no quadro");

        tipo.Ativo.ShouldBeTrue();
        tipo.CategoriaServicoId.ShouldBe(categoria.Id);
        categoria.TiposServico.ShouldHaveSingleItem().ShouldBe(tipo);
    }

    [Fact]
    public void Tipo_de_servico_com_nome_repetido_na_categoria_e_recusado()
    {
        var categoria = Construtor.Categoria();
        categoria.AdicionarTipoServico(Construtor.Id(502), "Troca de disjuntor", "Descricao");

        var excecao = Should.Throw<ExcecaoDeDominio>(() => categoria.AdicionarTipoServico(
            Construtor.Id(503),
            "troca de disjuntor",
            "Outra descricao"));

        excecao.Requisito.ShouldBe("RF0032");
        categoria.TiposServico.Count.ShouldBe(1);
    }

    [Fact]
    public void Categoria_inativa_nao_recebe_novo_tipo_de_servico()
    {
        var categoria = Construtor.Categoria();
        categoria.Inativar();

        var excecao = Should.Throw<ExcecaoDeDominio>(() => categoria.AdicionarTipoServico(
            Construtor.Id(504),
            "Troca de disjuntor",
            "Descricao"));

        excecao.Requisito.ShouldBe("RF0032");
    }

    [Fact]
    public void Alterar_tipo_de_servico_atualiza_nome_e_descricao()
    {
        var categoria = Construtor.Categoria();
        var tipo = categoria.AdicionarTipoServico(Construtor.Id(505), "Troca de disjuntor", "Descricao");

        tipo.Alterar("Troca de tomada", "Substituicao de tomada");

        tipo.Nome.ShouldBe("Troca de tomada");
        tipo.Descricao.ShouldBe("Substituicao de tomada");
    }

    [Fact]
    public void Tipo_de_servico_pode_ser_inativado_e_reativado()
    {
        var categoria = Construtor.Categoria();
        var tipo = categoria.AdicionarTipoServico(Construtor.Id(506), "Troca de disjuntor", "Descricao");

        tipo.Inativar();
        tipo.Ativo.ShouldBeFalse();

        tipo.Ativar();
        tipo.Ativo.ShouldBeTrue();
    }

    [Fact]
    public void Alterar_area_de_atendimento_atualiza_faixa_e_taxa()
    {
        var area = Construtor.Area(Construtor.Id(4));

        area.Alterar("Moema", "04001000", "04999999", 60m);

        area.Bairro.ShouldBe("Moema");
        area.CepInicial.ShouldBe("04001000");
        area.TaxaDeslocamento.ShouldBe(60m);
        area.Contem("04500000").ShouldBeTrue();
        area.Contem("05000000").ShouldBeFalse();
    }

    [Fact]
    public void Alterar_area_com_faixa_invertida_e_recusado()
    {
        var area = Construtor.Area(Construtor.Id(4));

        var excecao = Should.Throw<ExcecaoDeDominio>(
            () => area.Alterar("Moema", "05000000", "04000000", 60m));

        excecao.Requisito.ShouldBe("RF0027");
    }

    [Fact]
    public void Area_com_taxa_negativa_e_recusada()
    {
        var excecao = Should.Throw<ExcecaoDeDominio>(
            () => Construtor.Area(Construtor.Id(4), taxa: -1m));

        excecao.Requisito.ShouldBe("RN0071");
    }
}
