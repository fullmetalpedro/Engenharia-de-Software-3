using ChamadosManutencao.Api.Seguranca;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC12Garantia;
using FluentValidation;

namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// UC12 Acionar Garantia do Servico.
/// Requisitos: RF0085, RF0041, RF0047, RN0022, RN0023, RN0072, RNF0042.
/// </summary>
public static class UC12GarantiaEndpoints
{
    public static IEndpointRouteBuilder MapearUC12Garantia(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/v1/atendimentos").WithTags("UC12 Garantia");

        grupo.MapPost("/{id:guid}/garantia/acionamento", async (
                Guid id,
                AcionarGarantiaCommand comando,
                AcionarGarantiaHandler handler,
                IValidator<AcionarGarantiaCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                var resultado = await handler.ExecutarAsync(id, comando, cancellationToken);
                return Results.Created($"/api/v1/chamados/{resultado.ChamadoId}", resultado);
            })
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Aciona a garantia do atendimento, gerando um chamado de garantia.")
            .WithDescription("Requisitos: RF0085, RN0072, RN0022, RN0023, RNF0042. O chamado de "
                + "garantia nao gera fatura; se o tecnico original nao servir mais, ele nasce sem "
                + "tecnico, em EM ANALISE, e a resposta informa o motivo.");

        grupo.MapGet("/{id:guid}/garantia", async (
                Guid id,
                ConsultarGarantiaHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ExecutarAsync(id, cancellationToken)))
            .RequireAuthorization(Politicas.ClienteOuAdministrador)
            .WithSummary("Consulta a garantia do atendimento.")
            .WithDescription("Requisitos: RF0085, RN0072.");

        return rotas;
    }
}
