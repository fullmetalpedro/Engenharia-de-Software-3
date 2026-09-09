using ChamadosManutencao.Api.Seguranca;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC09Analise;
using FluentValidation;

namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// UC09 Analisar Historico de Chamados.
/// Requisitos: RF0071, RF0072, RF0073, RF0074, RN0061, RN0062, RN0063, RNF0011, RNF0051.
/// </summary>
public static class UC09AnaliseEndpoints
{
    public static IEndpointRouteBuilder MapearUC09Analise(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/v1/analises/chamados").WithTags("UC09 Analise");

        grupo.MapGet("/", async (
                AnalisarChamadosHandler handler,
                IValidator<FiltroDeAnalise> validator,
                CancellationToken cancellationToken,
                DateTimeOffset dataInicio,
                DateTimeOffset dataFim,
                Guid[]? categoriaIds = null,
                Guid[]? tecnicoIds = null,
                string agruparPor = "categoria") =>
            {
                var filtro = new FiltroDeAnalise(
                    dataInicio,
                    dataFim,
                    categoriaIds ?? [],
                    tecnicoIds ?? [],
                    agruparPor);

                await Validacao.GarantirValidoAsync(validator, filtro, cancellationToken);

                return Results.Ok(await handler.ExecutarAsync(filtro, cancellationToken));
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Serie mensal de chamados por categoria ou tecnico.")
            .WithDescription("Requisitos: RF0071, RF0072, RF0073, RN0061, RN0062, RN0063, RNF0051. "
                + "A resposta ja vem no formato do grafico de linhas.");

        grupo.MapGet("/exportacao", async (
                AnalisarChamadosHandler handler,
                IValidator<FiltroDeAnalise> validator,
                CancellationToken cancellationToken,
                DateTimeOffset dataInicio,
                DateTimeOffset dataFim,
                Guid[]? categoriaIds = null,
                Guid[]? tecnicoIds = null,
                string agruparPor = "categoria") =>
            {
                var filtro = new FiltroDeAnalise(
                    dataInicio,
                    dataFim,
                    categoriaIds ?? [],
                    tecnicoIds ?? [],
                    agruparPor);

                await Validacao.GarantirValidoAsync(validator, filtro, cancellationToken);

                var csv = await handler.ExportarCsvAsync(filtro, cancellationToken);

                return Results.File(
                    csv,
                    "text/csv",
                    $"analise-chamados-{dataInicio:yyyyMM}-{dataFim:yyyyMM}.csv");
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Exporta a analise em CSV.")
            .WithDescription("Requisitos: RF0074 (decisao D18: CSV abre em qualquer planilha).");

        return rotas;
    }
}
