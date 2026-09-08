using ChamadosManutencao.Api.Seguranca;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.Application.UC05Triagem;
using ChamadosManutencao.Domain.Enums;
using FluentValidation;

namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// UC05 Triar e Atribuir Chamado.
/// Requisitos: RF0044, RF0045, RF0046, RF0047, RF0048, RF0049, RF0050, RN0022, RN0023,
/// RN0033, RN0034, RNF0011, RNF0041.
/// </summary>
public static class UC05TriagemEndpoints
{
    public static IEndpointRouteBuilder MapearUC05Triagem(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/v1/chamados").WithTags("UC05 Triagem");

        grupo.MapGet("/", async (
                ConsultarChamadosHandler handler,
                CancellationToken cancellationToken,
                StatusChamado? status = null,
                Urgencia? urgencia = null,
                Guid? categoriaId = null,
                Guid? tecnicoId = null,
                Guid? clienteId = null,
                DateTimeOffset? dataInicio = null,
                DateTimeOffset? dataFim = null,
                long? numero = null,
                int? page = null,
                int? pageSize = null) =>
                Results.Ok(await handler.ConsultarAsync(
                    new FiltroDeChamados(
                        status,
                        urgencia,
                        categoriaId,
                        tecnicoId,
                        clienteId,
                        dataInicio,
                        dataFim,
                        numero,
                        page,
                        pageSize),
                    cancellationToken)))
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Consulta todos os chamados da empresa por filtro.")
            .WithDescription("Requisitos: RF0044, RNF0011.");

        grupo.MapPatch("/{id:guid}/urgencia", async (
                Guid id,
                ClassificarUrgenciaCommand comando,
                ClassificarUrgenciaHandler handler,
                IValidator<ClassificarUrgenciaCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                await handler.ExecutarAsync(id, comando, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Classifica a urgencia do chamado.")
            .WithDescription("Requisitos: RF0046.");

        grupo.MapPost("/{id:guid}/atribuicao", async (
                Guid id,
                AtribuirTecnicoCommand comando,
                AtribuirTecnicoHandler handler,
                IValidator<AtribuirTecnicoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                await handler.ExecutarAsync(id, comando, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Atribui um tecnico ao chamado.")
            .WithDescription("Requisitos: RF0047, RN0022, RN0023, RN0034.");

        grupo.MapPut("/{id:guid}/atribuicao", async (
                Guid id,
                AtribuirTecnicoCommand comando,
                AtribuirTecnicoHandler handler,
                IValidator<AtribuirTecnicoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                await handler.ExecutarAsync(id, comando, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Reatribui o chamado a outro tecnico.")
            .WithDescription("Requisitos: RF0048, RN0022, RN0023.");

        grupo.MapPatch("/{id:guid}/status", async (
                Guid id,
                AlterarStatusCommand comando,
                AlterarStatusDoChamadoHandler handler,
                IValidator<AlterarStatusCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                await handler.ExecutarAsync(id, comando, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Altera o status do chamado.")
            .WithDescription("Requisitos: RF0049, RN0034, RNF0041.");

        grupo.MapPost("/{id:guid}/cancelamento-administrativo", async (
                Guid id,
                CancelamentoAdministrativoCommand comando,
                CancelamentoAdministrativoHandler handler,
                IValidator<CancelamentoAdministrativoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                await handler.ExecutarAsync(id, comando, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Cancela o chamado por decisao administrativa.")
            .WithDescription("Requisitos: RF0045, RN0033 (decisao D06).");

        return rotas;
    }
}
