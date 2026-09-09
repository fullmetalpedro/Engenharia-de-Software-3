using System.Net;
using ChamadosManutencao.Application.UC09Analise;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.IntegrationTests.Comum;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// UC09 Analisar Historico de Chamados.
/// Requisitos: RF0071, RF0072, RF0073, RF0074, RN0061, RN0062, RN0063, RNF0051.
/// </summary>
[Trait("Escopo", "UC09")]
public class UC09AnaliseTests : TesteDeIntegracao
{
    public UC09AnaliseTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    /// <summary>RF0071, RN0061, RNF0051: uma coluna por mes e uma serie por categoria.</summary>
    [Fact]
    public async Task Analise_agrupa_por_mes_e_devolve_uma_serie_por_categoria()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirChamadoAsync(cenario);
        await AbrirChamadoAsync(cenario);

        // Um dos chamados vai para o mes anterior, para que o eixo X tenha duas colunas.
        await EnvelhecerAberturaAsync(chamado.Id, DateTimeOffset.UtcNow.AddMonths(-1));

        var analise = await ConsultarAsync(DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow);

        analise.EixoX.Count.ShouldBe(2);
        analise.EixoX.ShouldAllBe(rotulo => rotulo.Length == 7 && rotulo[4] == '-');
        analise.Series.Count.ShouldBe(1);
        analise.Series.Single().Legenda.ShouldBe(cenario.Categoria.Nome);
        analise.Series.Single().Valores.Sum().ShouldBe(2);
        analise.TotalChamados.ShouldBe(2);
    }

    /// <summary>RF0073: selecao de categorias limita as series exibidas.</summary>
    [Fact]
    public async Task Filtro_por_categoria_limita_as_series()
    {
        var cenario = await MontarCenarioBasicoAsync();
        await AbrirChamadoAsync(cenario);

        var outraCategoria = await CriarCategoriaAsync("Hidraulica");
        var outroTipo = await CriarTipoServicoAsync(outraCategoria.Id);
        var outroTecnico = await CriarTecnicoAsync(outraCategoria.Id);
        var segundoCenario = new CenarioBasico(outraCategoria, outroTipo, outroTecnico, cenario.Cliente);
        await AbrirChamadoAsync(segundoCenario);

        var todas = await ConsultarAsync(DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow);
        todas.Series.Count.ShouldBe(2);

        var apenasUma = await ConsultarAsync(
            DateTimeOffset.UtcNow.AddMonths(-1),
            DateTimeOffset.UtcNow,
            $"&categoriaIds={cenario.Categoria.Id}");

        apenasUma.Series.Count.ShouldBe(1);
        apenasUma.TotalChamados.ShouldBe(1);
    }

    /// <summary>RF0072: comparacao de desempenho entre tecnicos.</summary>
    [Fact]
    public async Task Analise_agrupada_por_tecnico_usa_o_tecnico_como_serie()
    {
        var cenario = await MontarCenarioBasicoAsync();
        await AbrirEAtribuirAsync(cenario);

        var analise = await ConsultarAsync(
            DateTimeOffset.UtcNow.AddMonths(-1),
            DateTimeOffset.UtcNow,
            $"&agruparPor=tecnico&tecnicoIds={cenario.Tecnico.Id}");

        analise.Series.Count.ShouldBe(1);
        analise.Series.Single().Legenda.ShouldBe(cenario.Tecnico.Dados.NomeCompleto);
        analise.TotalChamados.ShouldBe(1);
    }

    /// <summary>RN0063: chamado cancelado nao entra na contagem.</summary>
    [Fact]
    public async Task Chamado_cancelado_nao_e_contabilizado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var cancelado = await AbrirChamadoAsync(cenario);
        await AbrirChamadoAsync(cenario);

        await (await cenario.Cliente.Http.PostarAsync($"/api/v1/chamados/{cancelado.Id}/cancelamento"))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);

        var analise = await ConsultarAsync(DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow);

        analise.TotalChamados.ShouldBe(1);
    }

    /// <summary>
    /// RN0062: o intervalo e contado em meses inteiros (RN0061), entao 25 meses estoura o
    /// maximo de 24 e e recusado.
    /// </summary>
    [Fact]
    public async Task Intervalo_maior_que_24_meses_e_recusado()
    {
        var resposta = await Admin.GetAsync(
            "/api/v1/analises/chamados"
            + $"?dataInicio={Formatar(DateTimeOffset.UtcNow.AddMonths(-25))}"
            + $"&dataFim={Formatar(DateTimeOffset.UtcNow)}");

        await resposta.DeveTerStatusAsync(HttpStatusCode.BadRequest);
    }

    /// <summary>RN0062: 24 meses ainda cabe no limite.</summary>
    [Fact]
    public async Task Intervalo_de_24_meses_e_aceito()
    {
        var resposta = await Admin.GetAsync(
            "/api/v1/analises/chamados"
            + $"?dataInicio={Formatar(DateTimeOffset.UtcNow.AddMonths(-23))}"
            + $"&dataFim={Formatar(DateTimeOffset.UtcNow)}");

        await resposta.DeveTerStatusAsync(HttpStatusCode.OK);
    }

    /// <summary>RN0062: data de fim anterior a de inicio e recusada.</summary>
    [Fact]
    public async Task Data_de_fim_anterior_a_de_inicio_e_recusada()
    {
        var resposta = await Admin.GetAsync(
            "/api/v1/analises/chamados"
            + $"?dataInicio={Formatar(DateTimeOffset.UtcNow)}"
            + $"&dataFim={Formatar(DateTimeOffset.UtcNow.AddMonths(-2))}");

        await resposta.DeveTerStatusAsync(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Data que o ASP.NET nao consegue converter e requisicao malformada, nao erro interno.
    /// Achado no teste manual: sem o tratamento a resposta saia como 500.
    /// </summary>
    [Fact]
    public async Task Data_malformada_devolve_400_e_nao_500()
    {
        var resposta = await Admin.GetAsync(
            "/api/v1/analises/chamados"
            + "?dataInicio=abacaxi"
            + $"&dataFim={Formatar(DateTimeOffset.UtcNow)}");

        await resposta.DeveTerStatusAsync(HttpStatusCode.BadRequest);
    }

    /// <summary>RF0074: exportacao em planilha (CSV).</summary>
    [Fact]
    public async Task Exportacao_devolve_csv_com_o_conteudo_do_grafico()
    {
        var cenario = await MontarCenarioBasicoAsync();
        await AbrirChamadoAsync(cenario);

        var resposta = await Admin.GetAsync(
            "/api/v1/analises/chamados/exportacao"
            + $"?dataInicio={Formatar(DateTimeOffset.UtcNow.AddMonths(-1))}"
            + $"&dataFim={Formatar(DateTimeOffset.UtcNow)}");

        await resposta.DeveTerStatusAsync(HttpStatusCode.OK);
        resposta.Content.Headers.ContentType?.MediaType.ShouldBe("text/csv");

        var csv = await resposta.TextoAsync();
        csv.ShouldContain(cenario.Categoria.Nome);
    }

    [Fact]
    public async Task Analise_e_restrita_ao_administrador()
    {
        var cliente = await CadastrarClienteAsync();

        var resposta = await cliente.Http.GetAsync(
            "/api/v1/analises/chamados"
            + $"?dataInicio={Formatar(DateTimeOffset.UtcNow.AddMonths(-1))}"
            + $"&dataFim={Formatar(DateTimeOffset.UtcNow)}");

        await resposta.DeveTerStatusAsync(HttpStatusCode.Forbidden);
    }

    private async Task<AnaliseDeChamadosDto> ConsultarAsync(
        DateTimeOffset inicio,
        DateTimeOffset fim,
        string extra = "") =>
        await (await Admin.GetAsync(
                $"/api/v1/analises/chamados?dataInicio={Formatar(inicio)}&dataFim={Formatar(fim)}{extra}"))
            .LerAsync<AnaliseDeChamadosDto>();

    private Task EnvelhecerAberturaAsync(Guid chamadoId, DateTimeOffset novaData) =>
        Ambiente.ComContextoAsync(async contexto =>
        {
            var chamado = await contexto.Chamados.SingleAsync(c => c.Id == chamadoId);

            contexto.Entry(chamado).Property(nameof(Chamado.DataHoraAbertura)).CurrentValue = novaData;

            await contexto.SaveChangesAsync();
        });

    private static string Formatar(DateTimeOffset data) =>
        Uri.EscapeDataString(data.ToString("O"));
}
