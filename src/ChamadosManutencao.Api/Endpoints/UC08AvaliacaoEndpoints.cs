using ChamadosManutencao.Api.Seguranca;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC08Avaliacoes;
using FluentValidation;

namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// UC08 Avaliar Atendimento.
/// Requisitos: RF0061, RF0062, RF0063, RN0051, RN0052.
/// </summary>
public static class UC08AvaliacaoEndpoints
{
    public static IEndpointRouteBuilder MapearUC08Avaliacao(this IEndpointRouteBuilder rotas)
    {
        var chamados = rotas.MapGroup("/api/v1/chamados").WithTags("UC08 Avaliacao");

        chamados.MapPost("/{id:guid}/avaliacao", async (
                Guid id,
                RegistrarAvaliacaoCommand comando,
                RegistrarAvaliacaoHandler handler,
                IValidator<RegistrarAvaliacaoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                var avaliacao = await handler.ExecutarAsync(id, comando, cancellationToken);
                return Results.Created($"/api/v1/chamados/{id}/avaliacao", avaliacao);
            })
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Registra a avaliacao do atendimento concluido.")
            .WithDescription("Requisitos: RF0061, RN0052 (15 dias corridos apos a conclusao).");

        var avaliacoes = rotas.MapGroup("/api/v1/avaliacoes").WithTags("UC08 Avaliacao");

        avaliacoes.MapPost("/{id:guid}/resposta", async (
                Guid id,
                ResponderAvaliacaoCommand comando,
                ResponderAvaliacaoHandler handler,
                IValidator<ResponderAvaliacaoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                return Results.Ok(await handler.ExecutarAsync(id, comando, cancellationToken));
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Responde publicamente a uma avaliacao.")
            .WithDescription("Requisitos: RF0063.");

        return rotas;
    }
}
