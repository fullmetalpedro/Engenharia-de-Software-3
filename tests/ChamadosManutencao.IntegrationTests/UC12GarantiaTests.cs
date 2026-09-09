using System.Net;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.Application.UC07Atendimento;
using ChamadosManutencao.Application.UC10FormasPagamento;
using ChamadosManutencao.Application.UC11Faturas;
using ChamadosManutencao.Application.UC12Garantia;
using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.IntegrationTests.Comum;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// UC12 Acionar Garantia do Servico.
/// Requisitos: RF0085, RN0072, RNF0042.
/// </summary>
[Trait("Escopo", "UC12")]
public class UC12GarantiaTests : TesteDeIntegracao
{
    public UC12GarantiaTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    /// <summary>RF0085: a garantia de 90 dias fica consultavel apos a conclusao.</summary>
    [Fact]
    public async Task Garantia_do_atendimento_e_consultavel()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (_, atendimento, conclusao) = await ConcluirAtendimentoAsync(cenario);

        var garantia = await (await cenario.Cliente.Http.GetAsync(
                $"/api/v1/atendimentos/{atendimento.Id}/garantia"))
            .LerAsync<GarantiaDto>();

        garantia.Id.ShouldBe(conclusao.Garantia.Id);
        garantia.PrazoDias.ShouldBe(Garantia.PrazoPadraoEmDias);
        garantia.Vigente.ShouldBeTrue();
        (garantia.DataFim - garantia.DataInicio).Days.ShouldBe(Garantia.PrazoPadraoEmDias);
    }

    /// <summary>RF0085 e RNF0042: o acionamento gera chamado de garantia vinculado ao original.</summary>
    [Fact]
    public async Task Acionamento_gera_chamado_de_garantia_vinculado_ao_original()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (original, atendimento, _) = await ConcluirAtendimentoAsync(cenario);
        await QuitarFaturaAsync(cenario);

        var acionamento = await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/atendimentos/{atendimento.Id}/garantia/acionamento",
                new AcionarGarantiaCommand("O mesmo defeito voltou uma semana depois.")))
            .LerAsync<ResultadoDoAcionamentoDto>();

        acionamento.ChamadoOriginalId.ShouldBe(original.Id);
        acionamento.NumeroDoChamado.ShouldBeGreaterThan(original.Numero);

        var detalhe = await (await cenario.Cliente.Http.GetAsync($"/api/v1/chamados/{acionamento.ChamadoId}"))
            .LerAsync<ChamadoDetalheDto>();

        detalhe.ChamadoDeGarantia.ShouldBeTrue();
        detalhe.ChamadoOriginalId.ShouldBe(original.Id);
        detalhe.GarantiaId.ShouldBe(acionamento.GarantiaId);
        detalhe.Urgencia.ShouldBe(Urgencia.Alta);
    }

    /// <summary>RF0085: o chamado de garantia volta para o tecnico que executou o servico.</summary>
    [Fact]
    public async Task Chamado_de_garantia_volta_para_o_tecnico_original()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (_, atendimento, _) = await ConcluirAtendimentoAsync(cenario);
        await QuitarFaturaAsync(cenario);

        var acionamento = await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/atendimentos/{atendimento.Id}/garantia/acionamento",
                new AcionarGarantiaCommand("Defeito reincidente.")))
            .LerAsync<ResultadoDoAcionamentoDto>();

        acionamento.AtribuidoAoTecnicoOriginal.ShouldBeTrue();
        acionamento.TecnicoId.ShouldBe(cenario.Tecnico.Id);
        acionamento.MotivoDaTriagemManual.ShouldBeNull();
    }

    /// <summary>RN0072: sem a fatura quitada, a garantia nao pode ser acionada.</summary>
    [Fact]
    public async Task Acionamento_com_fatura_em_aberto_e_recusado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (_, atendimento, _) = await ConcluirAtendimentoAsync(cenario);

        var resposta = await cenario.Cliente.Http.PostarAsync(
            $"/api/v1/atendimentos/{atendimento.Id}/garantia/acionamento",
            new AcionarGarantiaCommand("Defeito reincidente com fatura em aberto."));

        resposta.StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
        (await resposta.RequisitoVioladoAsync()).ShouldBe("RN0072");
    }

    /// <summary>RN0072: passados 90 dias corridos a garantia venceu.</summary>
    [Fact]
    public async Task Acionamento_fora_do_prazo_de_90_dias_e_recusado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (_, atendimento, conclusao) = await ConcluirAtendimentoAsync(cenario);
        await QuitarFaturaAsync(cenario);

        await Ambiente.ComContextoAsync(async contexto =>
        {
            var garantia = await contexto.Garantias.SingleAsync(g => g.Id == conclusao.Garantia.Id);

            // Inicio e fim andam juntos: a tabela tem check de coerencia entre eles e o prazo.
            var novoInicio = DateTimeOffset.UtcNow.AddDays(-Garantia.PrazoPadraoEmDias - 1);

            contexto.Entry(garantia).Property(nameof(Garantia.DataInicio)).CurrentValue = novoInicio;
            contexto.Entry(garantia).Property(nameof(Garantia.DataFim)).CurrentValue =
                novoInicio.AddDays(Garantia.PrazoPadraoEmDias);

            await contexto.SaveChangesAsync();
        });

        var resposta = await cenario.Cliente.Http.PostarAsync(
            $"/api/v1/atendimentos/{atendimento.Id}/garantia/acionamento",
            new AcionarGarantiaCommand("Acionamento tardio."));

        resposta.StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
        (await resposta.RequisitoVioladoAsync()).ShouldBe("RN0072");
    }

    [Fact]
    public async Task Cliente_nao_aciona_garantia_de_atendimento_de_outro_cliente()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (_, atendimento, _) = await ConcluirAtendimentoAsync(cenario);
        await QuitarFaturaAsync(cenario);

        var intruso = await CadastrarClienteAsync();

        var resposta = await intruso.Http.PostarAsync(
            $"/api/v1/atendimentos/{atendimento.Id}/garantia/acionamento",
            new AcionarGarantiaCommand("Acionamento indevido."));

        resposta.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    private async Task QuitarFaturaAsync(CenarioBasico cenario)
    {
        var forma = await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/clientes/{cenario.Cliente.Id}/formas-pagamento",
                new CadastrarFormaPagamentoCommand(
                    "Pix",
                    "PIX principal",
                    ChavePix: cenario.Cliente.Email,
                    TipoChave: TipoChavePix.Email)))
            .LerAsync<FormaPagamentoDto>();

        var faturas = await (await cenario.Cliente.Http.GetAsync("/api/v1/faturas/minhas"))
            .LerAsync<Application.Common.ResultadoPaginado<FaturaDto>>();

        var emAberto = faturas.Itens.First(f => f.Status == StatusFatura.Emitida);

        await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/faturas/{emAberto.Id}/pagamentos",
                new PagarFaturaCommand(forma.Id)))
            .DeveTerStatusAsync(HttpStatusCode.Created);
    }
}
