using System.Net;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.Application.UC06Agendamento;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.IntegrationTests.Comum;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// UC06 Agendar Atendimento.
/// Requisitos: RF0051, RF0052, RF0053, RN0034, RN0041.
/// </summary>
[Trait("Escopo", "UC06")]
public class UC06AgendamentoTests : TesteDeIntegracao
{
    public UC06AgendamentoTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    /// <summary>RF0051 e RN0034: o agendamento leva o chamado para AGENDADO.</summary>
    [Fact]
    public async Task Tecnico_propoe_agendamento_e_o_chamado_fica_agendado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirEAtribuirAsync(cenario);

        var agendamento = await (await cenario.Tecnico.Http.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/agendamentos",
                new AgendarCommand(DateTimeOffset.UtcNow.AddDays(1), 120)))
            .LerAsync<AgendamentoDto>();

        agendamento.Status.ShouldBe(StatusAgendamento.Proposto);
        agendamento.OrigemProposta.ShouldBe(OrigemProposta.Tecnico);
        agendamento.DuracaoEmMinutos.ShouldBe(120);

        var detalhe = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}"))
            .LerAsync<ChamadoDetalheDto>();

        detalhe.Status.ShouldBe(StatusChamado.Agendado);
    }

    /// <summary>RF0051: o administrador tambem pode propor o horario.</summary>
    [Fact]
    public async Task Administrador_tambem_propoe_agendamento()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirEAtribuirAsync(cenario);

        var agendamento = await (await Admin.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/agendamentos",
                new AgendarCommand(DateTimeOffset.UtcNow.AddDays(2))))
            .LerAsync<AgendamentoDto>();

        agendamento.OrigemProposta.ShouldBe(OrigemProposta.Administrador);
    }

    /// <summary>RF0051: sem tecnico atribuido nao ha o que agendar.</summary>
    [Fact]
    public async Task Agendamento_de_chamado_sem_tecnico_e_recusado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirChamadoAsync(cenario);

        var resposta = await Admin.PostarAsync(
            $"/api/v1/chamados/{chamado.Id}/agendamentos",
            new AgendarCommand(DateTimeOffset.UtcNow.AddDays(1)));

        await resposta.DeveTerStatusAsync(HttpStatusCode.Conflict);
    }

    /// <summary>RF0052.</summary>
    [Fact]
    public async Task Cliente_confirma_o_agendamento_proposto()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, agendamento) = await AgendarAsync(cenario);

        var agendamentos = await (await cenario.Cliente.Http.GetAsync(
                $"/api/v1/chamados/{chamado.Id}/agendamentos"))
            .LerAsync<IReadOnlyCollection<AgendamentoDto>>();

        var confirmado = agendamentos.Single(a => a.Id == agendamento.Id);
        confirmado.Status.ShouldBe(StatusAgendamento.Confirmado);
        confirmado.DataHoraConfirmacao.ShouldNotBeNull();
    }

    /// <summary>RF0053: o reagendamento marca o anterior como REAGENDADO.</summary>
    [Fact]
    public async Task Reagendamento_substitui_o_agendamento_anterior()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, primeiro) = await AgendarAsync(cenario);

        var novo = await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/agendamentos/{primeiro.Id}/reagendamento",
                new AgendarCommand(DateTimeOffset.UtcNow.AddDays(5), 90)))
            .LerAsync<AgendamentoDto>();

        novo.Id.ShouldNotBe(primeiro.Id);

        var agendamentos = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}/agendamentos"))
            .LerAsync<IReadOnlyCollection<AgendamentoDto>>();

        agendamentos.Single(a => a.Id == primeiro.Id).Status.ShouldBe(StatusAgendamento.Reagendado);
        agendamentos.Count.ShouldBe(2);
    }

    /// <summary>
    /// RN0034: agendar um chamado concluido e recusado, e a recusa nao pode deixar o
    /// agendamento que ja estava confirmado marcado como reagendado.
    /// </summary>
    [Fact]
    public async Task Agendamento_recusado_nao_mexe_no_agendamento_confirmado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _, _) = await ConcluirAtendimentoAsync(cenario);

        var antes = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}/agendamentos"))
            .LerAsync<IReadOnlyCollection<AgendamentoDto>>();

        var resposta = await cenario.Tecnico.Http.PostarAsync(
            $"/api/v1/chamados/{chamado.Id}/agendamentos",
            new AgendarCommand(DateTimeOffset.UtcNow.AddDays(20), 120));

        resposta.StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);

        var depois = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}/agendamentos"))
            .LerAsync<IReadOnlyCollection<AgendamentoDto>>();

        depois.Count.ShouldBe(antes.Count);
        depois.Select(a => a.Status).ShouldBe(antes.Select(a => a.Status));
    }

    /// <summary>RN0041: o tecnico nao pode ter dois atendimentos na mesma janela.</summary>
    [Fact]
    public async Task Agendamento_sobreposto_para_o_mesmo_tecnico_e_recusado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var horario = DateTimeOffset.UtcNow.AddDays(3);

        await AgendarAsync(cenario, horario);

        // Segundo chamado, mesmo tecnico, janela sobreposta.
        var segundoCliente = await CadastrarClienteAsync();
        var segundoCenario = cenario with { Cliente = segundoCliente };
        var segundoChamado = await AbrirEAtribuirAsync(segundoCenario);

        var resposta = await cenario.Tecnico.Http.PostarAsync(
            $"/api/v1/chamados/{segundoChamado.Id}/agendamentos",
            new AgendarCommand(horario.AddMinutes(30), 120));

        await resposta.DeveTerStatusAsync(HttpStatusCode.Conflict);
    }

    /// <summary>RN0041: fora da janela ocupada o agendamento passa.</summary>
    [Fact]
    public async Task Agendamento_em_janela_livre_do_mesmo_tecnico_e_aceito()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var horario = DateTimeOffset.UtcNow.AddDays(3);

        await AgendarAsync(cenario, horario);

        var segundoCliente = await CadastrarClienteAsync();
        var segundoCenario = cenario with { Cliente = segundoCliente };
        var segundoChamado = await AbrirEAtribuirAsync(segundoCenario);

        var resposta = await cenario.Tecnico.Http.PostarAsync(
            $"/api/v1/chamados/{segundoChamado.Id}/agendamentos",
            new AgendarCommand(horario.AddHours(4), 120));

        await resposta.DeveTerStatusAsync(HttpStatusCode.Created);
    }
}
