using System.Net;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.Application.UC07Atendimento;
using ChamadosManutencao.Application.UC11Faturas;
using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.IntegrationTests.Comum;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// UC07 Executar Atendimento.
/// Requisitos: RF0054, RF0055, RF0056, RF0057, RF0082, RN0034, RN0042, RN0043, RN0051,
/// RN0071, RNF0043.
/// </summary>
[Trait("Escopo", "UC07")]
public class UC07AtendimentoTests : TesteDeIntegracao
{
    public UC07AtendimentoTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    /// <summary>RF0054 e RN0034: o inicio leva o chamado para EM ATENDIMENTO.</summary>
    [Fact]
    public async Task Inicio_do_atendimento_muda_o_status_do_chamado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, atendimento) = await IniciarAtendimentoAsync(cenario);

        atendimento.TecnicoId.ShouldBe(cenario.Tecnico.Id);
        atendimento.DataHoraConclusao.ShouldBeNull();

        var detalhe = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}"))
            .LerAsync<ChamadoDetalheDto>();

        detalhe.Status.ShouldBe(StatusChamado.EmAtendimento);
    }

    /// <summary>RF0054: somente o tecnico atribuido inicia o atendimento.</summary>
    [Fact]
    public async Task Outro_tecnico_nao_inicia_o_atendimento()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var intruso = await CriarTecnicoAsync(cenario.Categoria.Id);
        var (chamado, _) = await AgendarAsync(cenario);

        var resposta = await intruso.Http.PostarAsync($"/api/v1/chamados/{chamado.Id}/atendimento");

        resposta.StatusCode.ShouldBeOneOf(
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>RF0055 e RN0043: o orcamento nasce pendente com prazo de 48 horas.</summary>
    [Fact]
    public async Task Orcamento_registrado_fica_pendente_com_prazo_de_48_horas()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (_, atendimento) = await IniciarAtendimentoAsync(cenario);

        var orcamento = await RegistrarOrcamentoAsync(cenario, atendimento.Id, 150m, 250m);

        orcamento.Status.ShouldBe(StatusOrcamento.Pendente);
        orcamento.ValorPecas.ShouldBe(150m);
        orcamento.ValorMaoObra.ShouldBe(250m);
        orcamento.ValorTotal.ShouldBe(400m);
        (orcamento.PrazoAprovacao - orcamento.DataHoraRegistro).TotalHours
            .ShouldBe(Orcamento.PrazoDeAprovacaoEmHoras, tolerance: 0.1);
    }

    [Fact]
    public async Task Orcamento_sem_itens_e_recusado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (_, atendimento) = await IniciarAtendimentoAsync(cenario);

        var resposta = await cenario.Tecnico.Http.PostarAsync(
            $"/api/v1/atendimentos/{atendimento.Id}/orcamento",
            new RegistrarOrcamentoCommand([]));

        await resposta.DeveTerStatusAsync(HttpStatusCode.BadRequest);
    }

    /// <summary>RF0056: aprovacao do cliente.</summary>
    [Fact]
    public async Task Cliente_aprova_o_orcamento()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (_, atendimento) = await IniciarAtendimentoAsync(cenario);
        var orcamento = await RegistrarOrcamentoAsync(cenario, atendimento.Id);

        await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/orcamentos/{orcamento.Id}/decisao",
                new DecisaoDeOrcamentoCommand("Aprovado")))
            .DeveTerStatusAsync(HttpStatusCode.OK);

        var atual = await ConsultarOrcamentoAsync(cenario, atendimento.Id, orcamento.Id);

        atual.Status.ShouldBe(StatusOrcamento.Aprovado);
        atual.DataHoraDecisao.ShouldNotBeNull();
    }

    /// <summary>RN0042: a recusa sem pedido de novo orcamento cancela o chamado.</summary>
    [Fact]
    public async Task Recusa_do_orcamento_cancela_o_chamado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, atendimento) = await IniciarAtendimentoAsync(cenario);
        var orcamento = await RegistrarOrcamentoAsync(cenario, atendimento.Id);

        await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/orcamentos/{orcamento.Id}/decisao",
                new DecisaoDeOrcamentoCommand("Recusado")))
            .DeveTerStatusAsync(HttpStatusCode.OK);

        var detalhe = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}"))
            .LerAsync<ChamadoDetalheDto>();

        detalhe.Status.ShouldBe(StatusChamado.Cancelado);
    }

    /// <summary>RN0042: pedindo novo orcamento, o chamado continua em atendimento.</summary>
    [Fact]
    public async Task Recusa_com_pedido_de_novo_orcamento_mantem_o_chamado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, atendimento) = await IniciarAtendimentoAsync(cenario);
        var orcamento = await RegistrarOrcamentoAsync(cenario, atendimento.Id);

        await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/orcamentos/{orcamento.Id}/decisao",
                new DecisaoDeOrcamentoCommand("Recusado", SolicitarNovoOrcamento: true)))
            .DeveTerStatusAsync(HttpStatusCode.OK);

        var detalhe = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}"))
            .LerAsync<ChamadoDetalheDto>();

        detalhe.Status.ShouldBe(StatusChamado.EmAtendimento);
    }

    /// <summary>RN0043: com o orcamento pendente, a conclusao e barrada.</summary>
    [Fact]
    public async Task Conclusao_com_orcamento_pendente_e_recusada()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (_, atendimento) = await IniciarAtendimentoAsync(cenario);
        await RegistrarOrcamentoAsync(cenario, atendimento.Id);

        var resposta = await cenario.Tecnico.Http.PostarAsync(
            $"/api/v1/atendimentos/{atendimento.Id}/conclusao",
            new ConcluirAtendimentoCommand("Tentativa de conclusao com orcamento pendente."));

        resposta.StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// RF0057 e RNF0043: a cota de 5 anexos e da midia do problema. Com o chamado no limite,
    /// o tecnico ainda conclui anexando as fotos do servico finalizado.
    /// </summary>
    [Fact]
    public async Task Fotos_da_conclusao_nao_esbarram_na_cota_de_anexos_do_chamado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, atendimento) = await IniciarAtendimentoAsync(cenario);

        for (var indice = 0; indice < Chamado.MaximoDeAnexos; indice++)
        {
            using var anexo = new MultipartFormDataContent
            {
                { ConteudoDeFoto(), "arquivo", $"problema{indice}.jpg" }
            };

            await (await cenario.Cliente.Http.PostAsync($"/api/v1/chamados/{chamado.Id}/anexos", anexo))
                .DeveTerStatusAsync(HttpStatusCode.Created);
        }

        using var conclusao = new MultipartFormDataContent
        {
            { new StringContent("Servico executado e testado com o cliente."), "relatoTecnico" },
            { ConteudoDeFoto(), "arquivos", "servico-1.jpg" },
            { ConteudoDeFoto(), "arquivos", "servico-2.jpg" }
        };

        var resultado = await (await cenario.Tecnico.Http.PostAsync(
                $"/api/v1/atendimentos/{atendimento.Id}/conclusao",
                conclusao))
            .LerAsync<ResultadoDaConclusaoDto>();

        resultado.Atendimento.FotosDaConclusao.Count.ShouldBe(2);
    }

    /// <summary>RF0057, RN0034, RN0051: conclusao gera garantia de 90 dias.</summary>
    [Fact]
    public async Task Conclusao_encerra_o_chamado_e_abre_a_garantia()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _, conclusao) = await ConcluirAtendimentoAsync(cenario);

        conclusao.Atendimento.DataHoraConclusao.ShouldNotBeNull();
        conclusao.Garantia.PrazoDias.ShouldBe(Garantia.PrazoPadraoEmDias);
        conclusao.Garantia.Vigente.ShouldBeTrue();

        var detalhe = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}"))
            .LerAsync<ChamadoDetalheDto>();

        detalhe.Status.ShouldBe(StatusChamado.Concluido);

        // GarantiaId no chamado aponta a garantia que ORIGINOU o chamado (RF0085); no chamado
        // comum ele continua nulo mesmo depois da conclusao.
        detalhe.GarantiaId.ShouldBeNull();
    }

    /// <summary>RF0082 e RN0071: fatura = pecas + mao de obra + taxa de deslocamento.</summary>
    [Fact]
    public async Task Conclusao_gera_fatura_com_o_valor_do_orcamento_mais_a_taxa()
    {
        const decimal taxa = 35m;

        var categoria = await CriarCategoriaAsync();
        var tipoServico = await CriarTipoServicoAsync(categoria.Id);
        var tecnico = await CriarTecnicoAsync(categoria.Id, taxaDeslocamento: taxa);
        var cliente = await CadastrarClienteAsync();
        var cenario = new CenarioBasico(categoria, tipoServico, tecnico, cliente);

        var (_, _, conclusao) = await ConcluirAtendimentoAsync(cenario, valorPeca: 120m, valorMaoObra: 180m);

        conclusao.FaturaId.ShouldNotBeNull();
        conclusao.ValorDaFatura.ShouldBe(120m + 180m + taxa);

        var fatura = await (await cliente.Http.GetAsync($"/api/v1/faturas/{conclusao.FaturaId}"))
            .LerAsync<FaturaDto>();

        fatura.ValorPecas.ShouldBe(120m);
        fatura.ValorMaoObra.ShouldBe(180m);
        fatura.TaxaDeslocamento.ShouldBe(taxa);
        fatura.ValorTotal.ShouldBe(335m);
        fatura.Status.ShouldBe(StatusFatura.Emitida);
    }

    /// <summary>RN0043: o job expira o orcamento vencido e cancela o chamado.</summary>
    [Fact]
    public async Task Orcamento_vencido_expira_e_cancela_o_chamado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, atendimento) = await IniciarAtendimentoAsync(cenario);
        var orcamento = await RegistrarOrcamentoAsync(cenario, atendimento.Id);

        // Envelhece o prazo para alem das 48 horas e roda o job.
        await Ambiente.ComContextoAsync(async contexto =>
        {
            var registro = await contexto.Orcamentos.SingleAsync(o => o.Id == orcamento.Id);

            contexto.Entry(registro).Property(nameof(Orcamento.PrazoAprovacao)).CurrentValue =
                DateTimeOffset.UtcNow.AddHours(-1);

            await contexto.SaveChangesAsync();
        });

        await Ambiente.ExecutarJobDeExpiracaoDeOrcamentoAsync();

        var atual = await ConsultarOrcamentoAsync(cenario, atendimento.Id, orcamento.Id);

        atual.Status.ShouldBe(StatusOrcamento.Expirado);

        var detalhe = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}"))
            .LerAsync<ChamadoDetalheDto>();

        detalhe.Status.ShouldBe(StatusChamado.Cancelado);
    }

    private async Task<OrcamentoDto> ConsultarOrcamentoAsync(
        CenarioBasico cenario,
        Guid atendimentoId,
        Guid orcamentoId)
    {
        var orcamentos = await (await cenario.Tecnico.Http.GetAsync(
                $"/api/v1/atendimentos/{atendimentoId}/orcamento"))
            .LerAsync<IReadOnlyCollection<OrcamentoDto>>();

        return orcamentos.Single(o => o.Id == orcamentoId);
    }

    private async Task<OrcamentoDto> RegistrarOrcamentoAsync(
        CenarioBasico cenario,
        Guid atendimentoId,
        decimal valorPeca = 100m,
        decimal valorMaoObra = 200m) =>
        await (await cenario.Tecnico.Http.PostarAsync(
                $"/api/v1/atendimentos/{atendimentoId}/orcamento",
                new RegistrarOrcamentoCommand(
                [
                    new ItemOrcamentoCommand("Peca de reposicao", TipoItem.Peca, 1, valorPeca),
                    new ItemOrcamentoCommand("Mao de obra", TipoItem.MaoDeObra, 1, valorMaoObra)
                ])))
            .LerAsync<OrcamentoDto>();
}
