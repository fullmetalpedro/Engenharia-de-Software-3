using System.Net;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC10FormasPagamento;
using ChamadosManutencao.Application.UC07Atendimento;
using ChamadosManutencao.Application.UC11Faturas;
using ChamadosManutencao.Application.UC12Garantia;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Financeiro;
using ChamadosManutencao.IntegrationTests.Comum;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// UC11 Faturar e Receber Atendimento.
/// Requisitos: RF0082, RF0083, RF0084, RN0071, RNF0061.
/// </summary>
[Trait("Escopo", "UC11")]
public class UC11FaturasTests : TesteDeIntegracao
{
    public UC11FaturasTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    /// <summary>RF0083: o cliente ve apenas as proprias faturas.</summary>
    [Fact]
    public async Task Cliente_consulta_somente_as_proprias_faturas()
    {
        var cenario = await MontarCenarioBasicoAsync();
        await ConcluirAtendimentoAsync(cenario);

        var outro = await CadastrarClienteAsync();

        var minhas = await (await cenario.Cliente.Http.GetAsync("/api/v1/faturas/minhas"))
            .LerAsync<ResultadoPaginado<FaturaDto>>();
        minhas.Total.ShouldBe(1);

        var doOutro = await (await outro.Http.GetAsync("/api/v1/faturas/minhas"))
            .LerAsync<ResultadoPaginado<FaturaDto>>();
        doOutro.Total.ShouldBe(0);
    }

    /// <summary>RF0083: filtros por status, periodo e numero do chamado.</summary>
    [Fact]
    public async Task Consulta_do_administrador_aceita_filtros_combinaveis()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _, _) = await ConcluirAtendimentoAsync(cenario);

        var porStatus = await (await Admin.GetAsync("/api/v1/faturas?status=Emitida"))
            .LerAsync<ResultadoPaginado<FaturaDto>>();
        porStatus.Total.ShouldBe(1);

        var porChamado = await (await Admin.GetAsync($"/api/v1/faturas?numeroChamado={chamado.Numero}"))
            .LerAsync<ResultadoPaginado<FaturaDto>>();
        porChamado.Total.ShouldBe(1);

        var porPeriodo = await (await Admin.GetAsync(
                "/api/v1/faturas"
                + $"?dataInicio={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}"
                + $"&dataFim={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"))}"))
            .LerAsync<ResultadoPaginado<FaturaDto>>();
        porPeriodo.Total.ShouldBe(1);

        var porCliente = await (await Admin.GetAsync($"/api/v1/faturas?clienteId={cenario.Cliente.Id}"))
            .LerAsync<ResultadoPaginado<FaturaDto>>();
        porCliente.Total.ShouldBe(1);
    }

    /// <summary>RF0084: pagamento aprovado quita a fatura e registra a transacao.</summary>
    [Fact]
    public async Task Pagamento_aprovado_quita_a_fatura()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (_, _, conclusao) = await ConcluirAtendimentoAsync(cenario);
        var forma = await CadastrarCartaoAsync(cenario.Cliente);

        var pagamento = await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/faturas/{conclusao.FaturaId}/pagamentos",
                new PagarFaturaCommand(forma.Id)))
            .LerAsync<PagamentoDto>();

        pagamento.Status.ShouldBe(StatusPagamento.Aprovado);
        pagamento.ValorPago.ShouldBe(conclusao.ValorDaFatura!.Value);
        pagamento.IdentificadorTransacao.ShouldNotBeNullOrWhiteSpace();

        var fatura = await (await cenario.Cliente.Http.GetAsync($"/api/v1/faturas/{conclusao.FaturaId}"))
            .LerAsync<FaturaDto>();

        fatura.Status.ShouldBe(StatusFatura.Paga);
    }

    /// <summary>RF0084: o historico de tentativas fica consultavel.</summary>
    [Fact]
    public async Task Pagamentos_da_fatura_ficam_no_historico()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (_, _, conclusao) = await ConcluirAtendimentoAsync(cenario);
        var forma = await CadastrarCartaoAsync(cenario.Cliente);

        await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/faturas/{conclusao.FaturaId}/pagamentos",
                new PagarFaturaCommand(forma.Id)))
            .DeveTerStatusAsync(HttpStatusCode.Created);

        var pagamentos = await (await cenario.Cliente.Http.GetAsync(
                $"/api/v1/faturas/{conclusao.FaturaId}/pagamentos"))
            .LerAsync<IReadOnlyCollection<PagamentoDto>>();

        pagamentos.Count.ShouldBe(1);
    }

    /// <summary>RF0084: fatura ja quitada nao aceita novo pagamento.</summary>
    [Fact]
    public async Task Fatura_ja_paga_nao_aceita_novo_pagamento()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (_, _, conclusao) = await ConcluirAtendimentoAsync(cenario);
        var forma = await CadastrarCartaoAsync(cenario.Cliente);

        await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/faturas/{conclusao.FaturaId}/pagamentos",
                new PagarFaturaCommand(forma.Id)))
            .DeveTerStatusAsync(HttpStatusCode.Created);

        var repetido = await cenario.Cliente.Http.PostarAsync(
            $"/api/v1/faturas/{conclusao.FaturaId}/pagamentos",
            new PagarFaturaCommand(forma.Id));

        repetido.StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Cliente_nao_paga_fatura_de_outro_cliente()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (_, _, conclusao) = await ConcluirAtendimentoAsync(cenario);

        var intruso = await CadastrarClienteAsync();
        var forma = await CadastrarCartaoAsync(intruso);

        var resposta = await intruso.Http.PostarAsync(
            $"/api/v1/faturas/{conclusao.FaturaId}/pagamentos",
            new PagarFaturaCommand(forma.Id));

        resposta.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    /// <summary>RN0071: chamado de garantia nao gera fatura.</summary>
    [Fact]
    public async Task Chamado_de_garantia_nao_gera_fatura()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (_, atendimento, conclusao) = await ConcluirAtendimentoAsync(cenario);
        var forma = await CadastrarCartaoAsync(cenario.Cliente);

        await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/faturas/{conclusao.FaturaId}/pagamentos",
                new PagarFaturaCommand(forma.Id)))
            .DeveTerStatusAsync(HttpStatusCode.Created);

        var acionamento = await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/atendimentos/{atendimento.Id}/garantia/acionamento",
                new AcionarGarantiaCommand("O mesmo defeito voltou depois do reparo.")))
            .LerAsync<ResultadoDoAcionamentoDto>();

        var cenarioDaGarantia = cenario;
        var novoAtendimento = await ExecutarAtendimentoDeGarantiaAsync(cenarioDaGarantia, acionamento.ChamadoId);

        novoAtendimento.FaturaId.ShouldBeNull();

        var faturas = await (await cenario.Cliente.Http.GetAsync("/api/v1/faturas/minhas"))
            .LerAsync<ResultadoPaginado<FaturaDto>>();

        faturas.Total.ShouldBe(1);
    }

    private async Task<ResultadoDaConclusaoDto> ExecutarAtendimentoDeGarantiaAsync(
        CenarioBasico cenario,
        Guid chamadoId)
    {
        // O chamado de garantia ja nasce atribuido ao tecnico original quando ele ainda atende.
        var agendamento = await (await cenario.Tecnico.Http.PostarAsync(
                $"/api/v1/chamados/{chamadoId}/agendamentos",
                new ChamadosManutencao.Application.UC06Agendamento.AgendarCommand(DateTimeOffset.UtcNow.AddDays(10), 120)))
            .LerAsync<ChamadosManutencao.Application.UC04Chamados.AgendamentoDto>();

        await (await cenario.Cliente.Http.PostarAsync($"/api/v1/agendamentos/{agendamento.Id}/confirmacao"))
            .DeveTerStatusAsync(HttpStatusCode.OK);

        var atendimento = await (await cenario.Tecnico.Http.PostarAsync(
                $"/api/v1/chamados/{chamadoId}/atendimento"))
            .LerAsync<AtendimentoDto>();

        return await (await cenario.Tecnico.Http.PostarAsync(
                $"/api/v1/atendimentos/{atendimento.Id}/conclusao",
                new ConcluirAtendimentoCommand(
                    "Reparo refeito em garantia, sem custo.")))
            .LerAsync<ResultadoDaConclusaoDto>();
    }

    private async Task<FormaPagamentoDto> CadastrarCartaoAsync(CenarioDeCliente cliente) =>
        await (await cliente.Http.PostarAsync(
                $"/api/v1/clientes/{cliente.Id}/formas-pagamento",
                new CadastrarFormaPagamentoCommand(
                    "CartaoCredito",
                    "Cartao principal",
                    Principal: true,
                    NumeroCartao: "4111111111111111",
                    NomeTitular: "CLIENTE DE TESTE",
                    Validade: "12/2030",
                    CodigoSeguranca: "123")))
            .LerAsync<FormaPagamentoDto>();
}
