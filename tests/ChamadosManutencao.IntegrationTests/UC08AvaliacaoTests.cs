using System.Net;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC08Avaliacoes;
using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Avaliacoes;
using ChamadosManutencao.IntegrationTests.Comum;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// UC08 Avaliar Atendimento.
/// Requisitos: RF0061, RF0062, RF0063, RN0051, RN0052.
/// </summary>
[Trait("Escopo", "UC08")]
public class UC08AvaliacaoTests : TesteDeIntegracao
{
    public UC08AvaliacaoTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    /// <summary>RF0061.</summary>
    [Fact]
    public async Task Cliente_avalia_o_atendimento_concluido()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _, _) = await ConcluirAtendimentoAsync(cenario);

        var avaliacao = await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/avaliacao",
                new RegistrarAvaliacaoCommand(5, "Servico rapido e bem feito.")))
            .LerAsync<AvaliacaoDto>();

        avaliacao.Nota.ShouldBe(5);
        avaliacao.Comentario.ShouldBe("Servico rapido e bem feito.");
        avaliacao.TecnicoId.ShouldBe(cenario.Tecnico.Id);
        avaliacao.NumeroDoChamado.ShouldBe(chamado.Numero);
    }

    /// <summary>RF0061: a nota vai de 1 a 5.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Nota_fora_da_faixa_e_recusada(int nota)
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _, _) = await ConcluirAtendimentoAsync(cenario);

        var resposta = await cenario.Cliente.Http.PostarAsync(
            $"/api/v1/chamados/{chamado.Id}/avaliacao",
            new RegistrarAvaliacaoCommand(nota));

        await resposta.DeveTerStatusAsync(HttpStatusCode.BadRequest);
    }

    /// <summary>RN0051: so o chamado concluido pode ser avaliado.</summary>
    [Fact]
    public async Task Chamado_nao_concluido_nao_pode_ser_avaliado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirChamadoAsync(cenario);

        var resposta = await cenario.Cliente.Http.PostarAsync(
            $"/api/v1/chamados/{chamado.Id}/avaliacao",
            new RegistrarAvaliacaoCommand(4));

        resposta.StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>RF0061: uma avaliacao por chamado.</summary>
    [Fact]
    public async Task Segunda_avaliacao_do_mesmo_chamado_e_recusada()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _, _) = await ConcluirAtendimentoAsync(cenario);

        await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/avaliacao",
                new RegistrarAvaliacaoCommand(4)))
            .DeveTerStatusAsync(HttpStatusCode.Created);

        var repetida = await cenario.Cliente.Http.PostarAsync(
            $"/api/v1/chamados/{chamado.Id}/avaliacao",
            new RegistrarAvaliacaoCommand(5));

        repetida.StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>RN0052: passados 15 dias corridos, a janela fecha.</summary>
    [Fact]
    public async Task Avaliacao_fora_do_prazo_de_15_dias_e_recusada()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, atendimento, _) = await ConcluirAtendimentoAsync(cenario);

        await Ambiente.ComContextoAsync(async contexto =>
        {
            var registro = await contexto.Atendimentos.SingleAsync(a => a.Id == atendimento.Id);

            contexto.Entry(registro).Property(nameof(Atendimento.DataHoraConclusao)).CurrentValue =
                DateTimeOffset.UtcNow.AddDays(-Avaliacao.PrazoParaAvaliarEmDias - 1);

            await contexto.SaveChangesAsync();
        });

        var resposta = await cenario.Cliente.Http.PostarAsync(
            $"/api/v1/chamados/{chamado.Id}/avaliacao",
            new RegistrarAvaliacaoCommand(3));

        resposta.StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
        (await resposta.RequisitoVioladoAsync()).ShouldBe("RN0052");
    }

    /// <summary>RF0062: as avaliacoes recebidas por um tecnico.</summary>
    [Fact]
    public async Task Consulta_lista_as_avaliacoes_do_tecnico()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _, _) = await ConcluirAtendimentoAsync(cenario);

        await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/avaliacao",
                new RegistrarAvaliacaoCommand(4, "Bom atendimento.")))
            .DeveTerStatusAsync(HttpStatusCode.Created);

        var avaliacoes = await (await Admin.GetAsync(
                $"/api/v1/tecnicos/{cenario.Tecnico.Id}/avaliacoes"))
            .LerAsync<ResultadoPaginado<AvaliacaoDto>>();

        avaliacoes.Total.ShouldBe(1);
        avaliacoes.Itens.Single().Nota.ShouldBe(4);
    }

    /// <summary>RF0063: resposta publica do administrador.</summary>
    [Fact]
    public async Task Administrador_responde_publicamente_a_avaliacao()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _, _) = await ConcluirAtendimentoAsync(cenario);

        var avaliacao = await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/avaliacao",
                new RegistrarAvaliacaoCommand(2, "Demorou para chegar.")))
            .LerAsync<AvaliacaoDto>();

        var resposta = await (await Admin.PostarAsync(
                $"/api/v1/avaliacoes/{avaliacao.Id}/resposta",
                new ResponderAvaliacaoCommand("Lamentamos o atraso e ja ajustamos a rota da equipe.")))
            .LerAsync<RespostaAvaliacaoDto>();

        resposta.Texto.ShouldBe("Lamentamos o atraso e ja ajustamos a rota da equipe.");
        resposta.AdministradorId.ShouldBe(AdminId);

        // A resposta passa a acompanhar a avaliacao na consulta do tecnico (RF0062).
        var avaliacoes = await (await Admin.GetAsync(
                $"/api/v1/tecnicos/{cenario.Tecnico.Id}/avaliacoes"))
            .LerAsync<ResultadoPaginado<AvaliacaoDto>>();

        avaliacoes.Itens.Single().Resposta.ShouldNotBeNull();
    }

    [Fact]
    public async Task Cliente_nao_responde_avaliacao()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _, _) = await ConcluirAtendimentoAsync(cenario);

        var avaliacao = await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/avaliacao",
                new RegistrarAvaliacaoCommand(3)))
            .LerAsync<AvaliacaoDto>();

        var resposta = await cenario.Cliente.Http.PostarAsync(
            $"/api/v1/avaliacoes/{avaliacao.Id}/resposta",
            new ResponderAvaliacaoCommand("Resposta indevida."));

        await resposta.DeveTerStatusAsync(HttpStatusCode.Forbidden);
    }
}
