using System.Net;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.IntegrationTests.Comum;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// RNF0011: toda consulta de usuario responde em no maximo 1 segundo, e a resposta traz o
/// tempo medido no cabecalho X-Tempo-De-Resposta-Ms.
/// </summary>
[Trait("Escopo", "RNF0011")]
public class RNF0011TempoDeRespostaTests : TesteDeIntegracao
{
    private const int LimiteEmMilissegundos = 1000;

    public RNF0011TempoDeRespostaTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    public static TheoryData<string> ConsultasDoAdministrador() =>
    [
        "/api/v1/clientes",
        "/api/v1/tecnicos",
        "/api/v1/categorias-servico",
        "/api/v1/tipos-servico",
        "/api/v1/chamados",
        "/api/v1/faturas"
    ];

    [Theory]
    [MemberData(nameof(ConsultasDoAdministrador))]
    public async Task Consulta_responde_dentro_do_limite_de_um_segundo(string rota)
    {
        var cenario = await MontarCenarioBasicoAsync();
        await AbrirChamadoAsync(cenario);

        var resposta = await Admin.GetAsync(rota);

        await resposta.DeveTerStatusAsync(HttpStatusCode.OK);

        var medido = long.Parse(resposta.Headers.GetValues("X-Tempo-De-Resposta-Ms").Single());

        medido.ShouldBeLessThanOrEqualTo(LimiteEmMilissegundos);
    }

    /// <summary>
    /// RNF0011: a consulta de lista sempre volta paginada, o que impede a resposta crescer
    /// junto com a base.
    /// </summary>
    [Fact]
    public async Task Consulta_de_lista_e_sempre_paginada()
    {
        var cenario = await MontarCenarioBasicoAsync();

        for (var indice = 0; indice < 3; indice++)
        {
            await AbrirChamadoAsync(cenario);
        }

        var pagina = await (await Admin.GetAsync("/api/v1/chamados?page=1&pageSize=2"))
            .LerAsync<ResultadoPaginado<ChamadoResumoDto>>();

        pagina.Itens.Count.ShouldBe(2);
        pagina.Total.ShouldBe(3);
        pagina.TotalDePaginas.ShouldBe(2);

        var segunda = await (await Admin.GetAsync("/api/v1/chamados?page=2&pageSize=2"))
            .LerAsync<ResultadoPaginado<ChamadoResumoDto>>();

        segunda.Itens.Count.ShouldBe(1);
    }

    /// <summary>RNF0011: o tamanho de pagina e limitado, mesmo se o cliente pedir mais.</summary>
    [Fact]
    public async Task Tamanho_de_pagina_e_limitado_ao_maximo()
    {
        var pagina = await (await Admin.GetAsync("/api/v1/clientes?pageSize=5000"))
            .LerAsync<ResultadoPaginado<Application.UC01Clientes.ClienteResumoDto>>();

        pagina.PageSize.ShouldBe(ParametrosDePaginacao.TamanhoMaximoDePagina);
    }
}
