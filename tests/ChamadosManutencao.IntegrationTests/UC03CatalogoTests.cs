using System.Net;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC03Catalogo;
using ChamadosManutencao.IntegrationTests.Comum;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// UC03 Gerenciar Catalogo de Servicos.
/// Requisitos: RF0031, RF0032, RF0033, RF0034.
/// </summary>
[Trait("Escopo", "UC03")]
public class UC03CatalogoTests : TesteDeIntegracao
{
    public UC03CatalogoTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    /// <summary>RF0031: a categoria carrega as marcas usadas por RN0031 e RN0032.</summary>
    [Fact]
    public async Task Cadastro_de_categoria_guarda_exige_foto_e_categoria_de_risco()
    {
        var categoria = await CriarCategoriaAsync("Ar-condicionado", exigeFoto: true, categoriaDeRisco: false);

        categoria.ExigeFoto.ShouldBeTrue();
        categoria.CategoriaDeRisco.ShouldBeFalse();
        categoria.Ativa.ShouldBeTrue();
    }

    /// <summary>RF0032.</summary>
    [Fact]
    public async Task Cadastro_de_tipo_de_servico_fica_vinculado_a_categoria()
    {
        var categoria = await CriarCategoriaAsync();
        var tipo = await CriarTipoServicoAsync(categoria.Id, "Troca de disjuntor");

        tipo.CategoriaServicoId.ShouldBe(categoria.Id);
        tipo.Ativo.ShouldBeTrue();
    }

    [Fact]
    public async Task Tipo_de_servico_com_nome_repetido_na_categoria_e_recusado()
    {
        var categoria = await CriarCategoriaAsync();

        await (await Admin.PostarAsync(
                $"/api/v1/categorias-servico/{categoria.Id}/tipos-servico",
                new TipoServicoCommand("Vazamento", "Reparo de vazamento.")))
            .DeveTerStatusAsync(HttpStatusCode.Created);

        var repetido = await Admin.PostarAsync(
            $"/api/v1/categorias-servico/{categoria.Id}/tipos-servico",
            new TipoServicoCommand("Vazamento", "Outro texto."));

        repetido.StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>RF0033.</summary>
    [Fact]
    public async Task Alteracao_de_categoria_e_de_tipo_de_servico()
    {
        var categoria = await CriarCategoriaAsync();
        var tipo = await CriarTipoServicoAsync(categoria.Id);

        var categoriaAlterada = await (await Admin.ColocarAsync(
                $"/api/v1/categorias-servico/{categoria.Id}",
                new CategoriaCommand("Eletrica predial", "Nova descricao.", true, true)))
            .LerAsync<CategoriaDto>();

        categoriaAlterada.Nome.ShouldBe("Eletrica predial");
        categoriaAlterada.ExigeFoto.ShouldBeTrue();
        categoriaAlterada.CategoriaDeRisco.ShouldBeTrue();

        var tipoAlterado = await (await Admin.ColocarAsync(
                $"/api/v1/tipos-servico/{tipo.Id}",
                new TipoServicoCommand("Troca de tomada", "Descricao nova.")))
            .LerAsync<TipoServicoDto>();

        tipoAlterado.Nome.ShouldBe("Troca de tomada");
    }

    /// <summary>RF0034: qualquer usuario autenticado consulta o catalogo.</summary>
    [Fact]
    public async Task Consulta_de_catalogo_esta_disponivel_para_o_cliente()
    {
        var categoria = await CriarCategoriaAsync();
        await CriarTipoServicoAsync(categoria.Id);
        var cliente = await CadastrarClienteAsync();

        var categorias = await (await cliente.Http.GetAsync("/api/v1/categorias-servico"))
            .LerAsync<ResultadoPaginado<CategoriaDto>>();

        categorias.Total.ShouldBeGreaterThanOrEqualTo(1);

        var tipos = await (await cliente.Http.GetAsync($"/api/v1/tipos-servico?categoriaId={categoria.Id}"))
            .LerAsync<ResultadoPaginado<TipoServicoDto>>();

        tipos.Itens.ShouldAllBe(t => t.CategoriaServicoId == categoria.Id);
    }

    [Fact]
    public async Task Consulta_de_catalogo_sem_token_devolve_401()
    {
        var resposta = await Anonimo.GetAsync("/api/v1/categorias-servico");

        await resposta.DeveTerStatusAsync(HttpStatusCode.Unauthorized);
    }
}
