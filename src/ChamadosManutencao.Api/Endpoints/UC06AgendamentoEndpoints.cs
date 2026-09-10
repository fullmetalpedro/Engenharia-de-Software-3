using ChamadosManutencao.Api.Seguranca;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.Application.UC06Agendamento;
using FluentValidation;

namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// UC06 Agendar Atendimento.
/// Requisitos: RF0051, RF0052, RF0053, RN0034, RN0041, RNF0041.
/// </summary>
public static class UC06AgendamentoEndpoints
{
    public static IEndpointRouteBuilder MapearUC06Agendamento(this IEndpointRouteBuilder rotas)
    {
        var chamados = rotas.MapGroup("/api/v1/chamados").WithTags("UC06 Agendamento");

        chamados.MapPost("/{id:guid}/agendamentos", async (
                Guid id,
                AgendarCommand comando,
                AgendarAtendimentoHandler handler,
                IValidator<AgendarCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                var agendamento = await handler.ExecutarAsync(id, comando, cancellationToken);
                return Results.Created($"/api/v1/chamados/{id}/agendamentos/{agendamento.Id}", agendamento);
            })
            .RequireAuthorization(Politicas.TecnicoOuAdministrador)
            .WithSummary("Propoe data e hora para o atendimento.")
            .WithDescription("Requisitos: RF0051, RN0034, RN0041.");

        chamados.MapGet("/{id:guid}/agendamentos", async (
                Guid id,
                ConsultarAgendamentosHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ExecutarAsync(id, cancellationToken)))
            .RequireAuthorization()
            .WithSummary("Consulta os agendamentos do chamado.")
            .WithDescription("Requisitos: RF0051.");

        var agendamentos = rotas.MapGroup("/api/v1/agendamentos").WithTags("UC06 Agendamento");

        agendamentos.MapPost("/{id:guid}/confirmacao", async (
                Guid id,
                ConfirmarAgendamentoHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ExecutarAsync(id, cancellationToken)))
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Confirma o agendamento proposto.")
            .WithDescription("Requisitos: RF0052, RN0034, RNF0041.");

        agendamentos.MapPost("/{id:guid}/reagendamento", async (
                Guid id,
                AgendarCommand comando,
                ReagendarAtendimentoHandler handler,
                IValidator<AgendarCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                return Results.Ok(await handler.ExecutarAsync(id, comando, cancellationToken));
            })
            .RequireAuthorization(Politicas.ClienteOuTecnico)
            .WithSummary("Solicita o reagendamento do atendimento.")
            .WithDescription("Requisitos: RF0053, RN0041. Cliente dono ou tecnico atribuido.");

        return rotas;
    }
}
