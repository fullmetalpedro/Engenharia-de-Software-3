using System.Net;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.IntegrationTests.Comum;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// UC04 Abrir Chamado de Manutencao.
/// Requisitos: RF0041, RF0042, RF0043, RF0045, RF0050, RN0031, RN0032, RN0033, RN0034,
/// RN0035, RNF0042, RNF0043.
/// </summary>
[Trait("Escopo", "UC04")]
public class UC04ChamadosTests : TesteDeIntegracao
{
    public UC04ChamadosTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    /// <summary>RF0041, RNF0042: numero unico sequencial.</summary>
    [Fact]
    public async Task Abertura_cria_chamado_aberto_com_numero_sequencial()
    {
        var cenario = await MontarCenarioBasicoAsync();

        var primeiro = await AbrirChamadoAsync(cenario);
        var segundo = await AbrirChamadoAsync(cenario);

        primeiro.Status.ShouldBe(StatusChamado.Aberto);
        primeiro.Numero.ShouldBeGreaterThan(0);
        segundo.Numero.ShouldBe(primeiro.Numero + 1);
    }

    /// <summary>
    /// Corpo JSON com GUID fora do formato e requisicao malformada, nao erro interno. Achado
    /// ao montar a collection do Postman: sem o tratamento a resposta saia como 500.
    /// </summary>
    [Fact]
    public async Task Corpo_json_malformado_devolve_400_e_nao_500()
    {
        var cenario = await MontarCenarioBasicoAsync();

        var corpo = new StringContent(
            """
            {
              "imovelId": "nao-e-um-guid",
              "categoriaServicoId": "tambem-nao",
              "tipoServicoId": "nem-este",
              "descricaoProblema": "Corpo invalido de proposito.",
              "indicacaoDeRisco": false
            }
            """,
            System.Text.Encoding.UTF8,
            "application/json");

        var resposta = await cenario.Cliente.Http.PostAsync("/api/v1/chamados", corpo);

        await resposta.DeveTerStatusAsync(HttpStatusCode.BadRequest);
    }

    /// <summary>RN0031: categoria de risco com indicacao de risco vira urgencia ALTA.</summary>
    [Fact]
    public async Task Indicacao_de_risco_em_categoria_de_risco_forca_urgencia_alta()
    {
        var cenario = await MontarCenarioBasicoAsync(categoriaDeRisco: true);

        var chamado = await AbrirChamadoAsync(cenario, indicacaoDeRisco: true);

        chamado.Urgencia.ShouldBe(Urgencia.Alta);
    }

    /// <summary>
    /// RF0041 e RF0046: o cliente nao escolhe a urgencia. Sem indicacao de risco o chamado
    /// nasce com a urgencia padrao e so o administrador a classifica.
    /// </summary>
    [Fact]
    public async Task Sem_indicacao_de_risco_o_chamado_nasce_com_a_urgencia_padrao()
    {
        var cenario = await MontarCenarioBasicoAsync(categoriaDeRisco: true);

        var chamado = await AbrirChamadoAsync(cenario);

        chamado.Urgencia.ShouldBe(PoliticaUrgencia.Padrao);
    }

    /// <summary>RN0032: categoria que exige foto recusa abertura sem anexo.</summary>
    [Fact]
    public async Task Categoria_que_exige_foto_recusa_abertura_sem_anexo()
    {
        var cenario = await MontarCenarioBasicoAsync(exigeFoto: true);

        var comando = new AbrirChamadoCommand(
            cenario.Cliente.ImovelId,
            cenario.Categoria.Id,
            cenario.TipoServico.Id,
            "Equipamento com defeito, sem foto.",
            false);

        var resposta = await cenario.Cliente.Http.PostarAsync("/api/v1/chamados", comando);

        await resposta.DeveTerStatusAsync(HttpStatusCode.Conflict);
    }

    /// <summary>RN0032: com a foto na mesma requisicao, a abertura passa.</summary>
    [Fact]
    public async Task Categoria_que_exige_foto_aceita_abertura_com_anexo()
    {
        var cenario = await MontarCenarioBasicoAsync(exigeFoto: true);

        var resposta = await AbrirChamadoComFotoAsync(cenario);

        await resposta.DeveTerStatusAsync(HttpStatusCode.Created);
    }

    /// <summary>RF0042 e RNF0043: no maximo 5 anexos por origem.</summary>
    [Fact]
    public async Task Sexto_anexo_do_chamado_e_recusado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirChamadoAsync(cenario);

        for (var indice = 0; indice < Chamado.MaximoDeAnexos; indice++)
        {
            using var formulario = new MultipartFormDataContent
            {
                { ConteudoDeFoto(), "arquivo", $"foto{indice}.jpg" }
            };

            await (await cenario.Cliente.Http.PostAsync($"/api/v1/chamados/{chamado.Id}/anexos", formulario))
                .DeveTerStatusAsync(HttpStatusCode.Created);
        }

        using var excedente = new MultipartFormDataContent
        {
            { ConteudoDeFoto(), "arquivo", "foto-extra.jpg" }
        };

        var resposta = await cenario.Cliente.Http.PostAsync(
            $"/api/v1/chamados/{chamado.Id}/anexos",
            excedente);

        resposta.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>RF0043: o cliente consulta apenas os proprios chamados.</summary>
    [Fact]
    public async Task Cliente_consulta_somente_os_proprios_chamados()
    {
        var cenario = await MontarCenarioBasicoAsync();
        await AbrirChamadoAsync(cenario);

        var outroCliente = await CadastrarClienteAsync();

        var meus = await (await cenario.Cliente.Http.GetAsync("/api/v1/chamados/meus"))
            .LerAsync<IReadOnlyCollection<ChamadoResumoDto>>();

        meus.Count.ShouldBe(1);

        var doOutro = await (await outroCliente.Http.GetAsync("/api/v1/chamados/meus"))
            .LerAsync<IReadOnlyCollection<ChamadoResumoDto>>();

        doOutro.Count.ShouldBe(0);
    }

    /// <summary>RF0043: filtro por status e por urgencia.</summary>
    [Fact]
    public async Task Consulta_do_cliente_aceita_filtro_por_status_e_urgencia()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var primeiro = await AbrirChamadoAsync(cenario);
        await AbrirChamadoAsync(cenario);

        // RF0046: quem define urgencia e o administrador.
        await (await Admin.AlterarAsync(
                $"/api/v1/chamados/{primeiro.Id}/urgencia",
                new ChamadosManutencao.Application.UC05Triagem.ClassificarUrgenciaCommand(Urgencia.Alta)))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);

        var alta = await (await cenario.Cliente.Http.GetAsync("/api/v1/chamados/meus?urgencia=Alta"))
            .LerAsync<IReadOnlyCollection<ChamadoResumoDto>>();

        alta.Count.ShouldBe(1);

        var abertos = await (await cenario.Cliente.Http.GetAsync("/api/v1/chamados/meus?status=Aberto"))
            .LerAsync<IReadOnlyCollection<ChamadoResumoDto>>();

        abertos.Count.ShouldBe(2);
    }

    /// <summary>O chamado de outro cliente nao pode ser lido pelo cliente errado.</summary>
    [Fact]
    public async Task Cliente_nao_le_chamado_de_outro_cliente()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirChamadoAsync(cenario);
        var intruso = await CadastrarClienteAsync();

        var resposta = await intruso.Http.GetAsync($"/api/v1/chamados/{chamado.Id}");

        resposta.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    /// <summary>RF0045 e RN0033: cancelamento permitido com o chamado ABERTO.</summary>
    [Fact]
    public async Task Cliente_cancela_chamado_aberto()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirChamadoAsync(cenario);

        await (await cenario.Cliente.Http.PostarAsync($"/api/v1/chamados/{chamado.Id}/cancelamento"))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);

        var detalhe = await (await cenario.Cliente.Http.GetAsync($"/api/v1/chamados/{chamado.Id}"))
            .LerAsync<ChamadoDetalheDto>();

        detalhe.Status.ShouldBe(StatusChamado.Cancelado);
    }

    /// <summary>RN0033: depois do inicio do atendimento o cliente nao cancela mais.</summary>
    [Fact]
    public async Task Cliente_nao_cancela_chamado_em_atendimento()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _) = await IniciarAtendimentoAsync(cenario);

        var resposta = await cenario.Cliente.Http.PostarAsync($"/api/v1/chamados/{chamado.Id}/cancelamento");

        await resposta.DeveTerStatusAsync(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>RF0050: o historico registra cada transicao com data, hora e responsavel.</summary>
    [Fact]
    public async Task Historico_de_status_registra_a_abertura_e_o_cancelamento()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirChamadoAsync(cenario);

        await (await cenario.Cliente.Http.PostarAsync($"/api/v1/chamados/{chamado.Id}/cancelamento"))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);

        var historico = await (await cenario.Cliente.Http.GetAsync(
                $"/api/v1/chamados/{chamado.Id}/historico-status"))
            .LerAsync<IReadOnlyCollection<HistoricoStatusDto>>();

        historico.Count.ShouldBeGreaterThanOrEqualTo(2);
        historico.ShouldContain(h => h.StatusNovo == StatusChamado.Aberto);

        var cancelamento = historico.Single(h => h.StatusNovo == StatusChamado.Cancelado);
        cancelamento.UsuarioResponsavelId.ShouldBe(cenario.Cliente.Id);
        cancelamento.DataHoraAlteracao.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(1));
    }

    /// <summary>RN0035: reabertura dentro de 7 dias devolve o chamado para EM ANALISE.</summary>
    [Fact]
    public async Task Reabertura_dentro_do_prazo_devolve_o_chamado_para_em_analise()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _, _) = await ConcluirAtendimentoAsync(cenario);

        await (await cenario.Cliente.Http.PostarAsync($"/api/v1/chamados/{chamado.Id}/reabertura"))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);

        var detalhe = await (await cenario.Cliente.Http.GetAsync($"/api/v1/chamados/{chamado.Id}"))
            .LerAsync<ChamadoDetalheDto>();

        detalhe.Status.ShouldBe(StatusChamado.EmAnalise);
    }

    /// <summary>RN0035: passados os 7 dias a reabertura e recusada.</summary>
    [Fact]
    public async Task Reabertura_fora_do_prazo_e_recusada()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _, _) = await ConcluirAtendimentoAsync(cenario);

        // Envelhece a conclusao do atendimento, que e a data de onde o prazo e contado.
        await Ambiente.ComContextoAsync(async contexto =>
        {
            var atendimento = await contexto.Atendimentos.SingleAsync(a => a.ChamadoId == chamado.Id);

            contexto.Entry(atendimento).Property(nameof(Atendimento.DataHoraConclusao)).CurrentValue =
                atendimento.DataHoraConclusao!.Value.AddDays(-Chamado.PrazoDeReaberturaEmDias - 1);

            await contexto.SaveChangesAsync();
        });

        var resposta = await cenario.Cliente.Http.PostarAsync($"/api/v1/chamados/{chamado.Id}/reabertura");

        await resposta.DeveTerStatusAsync(HttpStatusCode.UnprocessableEntity);
    }
}
