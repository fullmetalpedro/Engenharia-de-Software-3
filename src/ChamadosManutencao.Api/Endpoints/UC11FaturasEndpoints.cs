using ChamadosManutencao.Api.Seguranca;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC11Faturas;
using ChamadosManutencao.Domain.Enums;
using FluentValidation;

namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// UC11 Faturar e Receber Atendimento.
/// Requisitos: RF0083, RF0084, RNF0011, RNF0061. A geracao da fatura (RF0082) nao tem
/// endpoint proprio: e efeito da conclusao do atendimento (UC07).
/// </summary>
public static class UC11FaturasEndpoints
{
    public static IEndpointRouteBuilder MapearUC11Faturas(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/v1/faturas").WithTags("UC11 Faturas");

        grupo.MapGet("/", async (
                ConsultarFaturasHandler handler,
                CancellationToken cancellationToken,
                StatusFatura? status = null,
                DateTimeOffset? dataInicio = null,
                DateTimeOffset? dataFim = null,
                long? numeroChamado = null,
                Guid? clienteId = null,
                int? page = null,
                int? pageSize = null) =>
                Results.Ok(await handler.ConsultarAsync(
                    new FiltroDeFaturas(status, dataInicio, dataFim, numeroChamado, clienteId, page, pageSize),
                    cancellationToken)))
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Consulta as faturas de todos os clientes.")
            .WithDescription("Requisitos: RF0083, RNF0011.");

        grupo.MapGet("/minhas", async (
                ConsultarFaturasHandler handler,
                CancellationToken cancellationToken,
                StatusFatura? status = null,
                DateTimeOffset? dataInicio = null,
                DateTimeOffset? dataFim = null,
                long? numeroChamado = null,
                int? page = null,
                int? pageSize = null) =>
                Results.Ok(await handler.MinhasFaturasAsync(
                    new FiltroDeFaturas(status, dataInicio, dataFim, numeroChamado, null, page, pageSize),
                    cancellationToken)))
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Consulta as faturas do cliente autenticado.")
            .WithDescription("Requisitos: RF0083, RNF0011.");

        grupo.MapGet("/{id:guid}", async (
                Guid id,
                ConsultarFaturasHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ObterAsync(id, cancellationToken)))
            .RequireAuthorization()
            .WithSummary("Consulta uma fatura pelo identificador.")
            .WithDescription("Requisitos: RF0083.");

        grupo.MapPost("/{id:guid}/pagamentos", async (
                Guid id,
                PagarFaturaCommand comando,
                PagarFaturaHandler handler,
                IValidator<PagarFaturaCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                var pagamento = await handler.ExecutarAsync(id, comando, cancellationToken);
                return Results.Created($"/api/v1/faturas/{id}/pagamentos/{pagamento.Id}", pagamento);
            })
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Registra o pagamento da fatura.")
            .WithDescription("Requisitos: RF0084, RNF0061.");

        grupo.MapGet("/{id:guid}/pagamentos", async (
                Guid id,
                ConsultarFaturasHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ConsultarPagamentosAsync(id, cancellationToken)))
            .RequireAuthorization()
            .WithSummary("Consulta os pagamentos da fatura.")
            .WithDescription("Requisitos: RF0084.");

        return rotas;
    }
}
