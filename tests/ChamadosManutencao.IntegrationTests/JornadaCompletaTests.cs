using System.Net;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.Application.UC05Triagem;
using ChamadosManutencao.Application.UC06Agendamento;
using ChamadosManutencao.Application.UC07Atendimento;
using ChamadosManutencao.Application.UC08Avaliacoes;
using ChamadosManutencao.Application.UC10FormasPagamento;
using ChamadosManutencao.Application.UC11Faturas;
using ChamadosManutencao.Application.UC12Garantia;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.IntegrationTests.Comum;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// Jornada ponta a ponta prevista na secao 9 da arquitetura: cadastro, chamado, triagem,
/// agendamento, atendimento, orcamento, conclusao, fatura, pagamento, avaliacao e garantia.
/// </summary>
[Trait("Escopo", "Jornada")]
public class JornadaCompletaTests : TesteDeIntegracao
{
    public JornadaCompletaTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    [Fact]
    public async Task Do_cadastro_ao_acionamento_da_garantia()
    {
        // 1. Catalogo e equipe (UC02, UC03).
        var categoria = await CriarCategoriaAsync("Eletrica", categoriaDeRisco: true);
        var tipoServico = await CriarTipoServicoAsync(categoria.Id, "Troca de disjuntor");
        var tecnico = await CriarTecnicoAsync(categoria.Id, taxaDeslocamento: 30m);

        // 2. Cadastro do cliente com imovel (UC01, RN0011).
        var cliente = await CadastrarClienteAsync();
        cliente.Dados.CodigoCliente.ShouldStartWith("CLI-");

        var cenario = new CenarioBasico(categoria, tipoServico, tecnico, cliente);

        // 3. Forma de pagamento tokenizada (UC10, RNF0061).
        var forma = await (await cliente.Http.PostarAsync(
                $"/api/v1/clientes/{cliente.Id}/formas-pagamento",
                new CadastrarFormaPagamentoCommand(
                    "Pix",
                    "PIX principal",
                    ChavePix: cliente.Email,
                    TipoChave: TipoChavePix.Email)))
            .LerAsync<FormaPagamentoDto>();

        // 4. Abertura com indicacao de risco: urgencia ALTA automatica (UC04, RN0031).
        var chamado = await AbrirChamadoAsync(cenario, indicacaoDeRisco: true);
        chamado.Urgencia.ShouldBe(Urgencia.Alta);
        chamado.Status.ShouldBe(StatusChamado.Aberto);

        // 5. Triagem: atribuicao do tecnico (UC05, RF0047).
        await (await Admin.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/atribuicao",
                new AtribuirTecnicoCommand(tecnico.Id)))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);

        // 6. Agendamento proposto e confirmado (UC06, RF0051, RF0052).
        var agendamento = await (await tecnico.Http.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/agendamentos",
                new AgendarCommand(DateTimeOffset.UtcNow.AddDays(1), 120)))
            .LerAsync<AgendamentoDto>();

        await (await cliente.Http.PostarAsync($"/api/v1/agendamentos/{agendamento.Id}/confirmacao"))
            .DeveTerStatusAsync(HttpStatusCode.OK);

        // 7. Execucao: inicio, orcamento e aprovacao (UC07, RF0054, RF0055, RF0056).
        var atendimento = await (await tecnico.Http.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/atendimento"))
            .LerAsync<AtendimentoDto>();

        var orcamento = await (await tecnico.Http.PostarAsync(
                $"/api/v1/atendimentos/{atendimento.Id}/orcamento",
                new RegistrarOrcamentoCommand(
                [
                    new ItemOrcamentoCommand("Disjuntor 25A", TipoItem.Peca, 2, 45m),
                    new ItemOrcamentoCommand("Mao de obra", TipoItem.MaoDeObra, 1, 160m)
                ])))
            .LerAsync<OrcamentoDto>();

        orcamento.ValorTotal.ShouldBe(250m);

        await (await cliente.Http.PostarAsync(
                $"/api/v1/orcamentos/{orcamento.Id}/decisao",
                new DecisaoDeOrcamentoCommand("Aprovado")))
            .DeveTerStatusAsync(HttpStatusCode.OK);

        // 8. Conclusao: garantia de 90 dias e fatura (RF0057, RF0082, RN0071).
        var conclusao = await (await tecnico.Http.PostarAsync(
                $"/api/v1/atendimentos/{atendimento.Id}/conclusao",
                new ConcluirAtendimentoCommand("Disjuntores trocados e circuito testado.")))
            .LerAsync<ResultadoDaConclusaoDto>();

        conclusao.ValorDaFatura.ShouldBe(250m + 30m);
        conclusao.Garantia.Vigente.ShouldBeTrue();

        // 9. Pagamento da fatura (UC11, RF0084).
        var pagamento = await (await cliente.Http.PostarAsync(
                $"/api/v1/faturas/{conclusao.FaturaId}/pagamentos",
                new PagarFaturaCommand(forma.Id)))
            .LerAsync<PagamentoDto>();

        pagamento.Status.ShouldBe(StatusPagamento.Aprovado);

        var fatura = await (await cliente.Http.GetAsync($"/api/v1/faturas/{conclusao.FaturaId}"))
            .LerAsync<FaturaDto>();
        fatura.Status.ShouldBe(StatusFatura.Paga);

        // 10. Avaliacao dentro do prazo (UC08, RF0061).
        var avaliacao = await (await cliente.Http.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/avaliacao",
                new RegistrarAvaliacaoCommand(5, "Chegou no horario e resolveu.")))
            .LerAsync<AvaliacaoDto>();

        avaliacao.Nota.ShouldBe(5);

        // 11. Resposta publica do administrador (RF0063).
        await (await Admin.PostarAsync(
                $"/api/v1/avaliacoes/{avaliacao.Id}/resposta",
                new ResponderAvaliacaoCommand("Obrigado pelo retorno.")))
            .DeveTerStatusAsync(HttpStatusCode.OK);

        // 12. Acionamento da garantia (UC12, RF0085, RN0072).
        var acionamento = await (await cliente.Http.PostarAsync(
                $"/api/v1/atendimentos/{atendimento.Id}/garantia/acionamento",
                new AcionarGarantiaCommand("O disjuntor voltou a desarmar.")))
            .LerAsync<ResultadoDoAcionamentoDto>();

        acionamento.ChamadoOriginalId.ShouldBe(chamado.Id);
        acionamento.AtribuidoAoTecnicoOriginal.ShouldBeTrue();

        // 13. O historico do chamado original guarda toda a jornada (RF0050).
        var historico = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}/historico-status"))
            .LerAsync<IReadOnlyCollection<HistoricoStatusDto>>();

        historico.Select(h => h.StatusNovo).ShouldContain(StatusChamado.Aberto);
        historico.Select(h => h.StatusNovo).ShouldContain(StatusChamado.EmAnalise);
        historico.Select(h => h.StatusNovo).ShouldContain(StatusChamado.Agendado);
        historico.Select(h => h.StatusNovo).ShouldContain(StatusChamado.EmAtendimento);
        historico.Select(h => h.StatusNovo).ShouldContain(StatusChamado.Concluido);

        // 14. RNF0012: toda escrita ficou registrada no log de transacao.
        var transacoes = await Ambiente.ComContextoAsync(contexto =>
            Task.FromResult(contexto.LogsDeTransacao.Count()));

        transacoes.ShouldBeGreaterThan(0);

        // 15. A analise gerencial ja enxerga o chamado (UC09, RN0063).
        var analise = await (await Admin.GetAsync(
                "/api/v1/analises/chamados"
                + $"?dataInicio={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMonths(-1).ToString("O"))}"
                + $"&dataFim={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}"))
            .LerAsync<Application.UC09Analise.AnaliseDeChamadosDto>();

        analise.TotalChamados.ShouldBe(2);
    }

    /// <summary>
    /// RNF0041: cada mudanca de status gera notificacao ao cliente, gravada em
    /// notificacao_enviada pelo notificador padrao.
    /// </summary>
    [Fact]
    public async Task Cada_mudanca_de_status_gera_notificacao_ao_cliente()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _, _) = await ConcluirAtendimentoAsync(cenario);

        var notificacoes = await Ambiente.ComContextoAsync(contexto =>
            Task.FromResult(contexto.NotificacoesEnviadas.Count()));

        notificacoes.ShouldBeGreaterThanOrEqualTo(4);

        var historico = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}/historico-status"))
            .LerAsync<IReadOnlyCollection<HistoricoStatusDto>>();

        historico.Count.ShouldBeGreaterThanOrEqualTo(5);
    }
}
