using ChamadosManutencao.Api.Seguranca;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.Domain.Enums;
using FluentValidation;

namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// UC04 Abrir Chamado de Manutencao.
/// Requisitos: RF0041, RF0042, RF0043, RF0045, RF0050, RN0031, RN0032, RN0033, RN0034,
/// RN0035, RNF0011, RNF0041, RNF0042, RNF0043.
/// </summary>
public static class UC04ChamadosEndpoints
{
    public static IEndpointRouteBuilder MapearUC04Chamados(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/v1/chamados").WithTags("UC04 Chamados");

        grupo.MapPost("/", async (
                HttpRequest requisicao,
                AbrirChamadoHandler handler,
                IValidator<AbrirChamadoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                var (comando, anexos) = await LeitorDeFormulario.LerAberturaDeChamadoAsync(
                    requisicao,
                    cancellationToken);

                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);

                var chamado = await handler.ExecutarAsync(comando, anexos, cancellationToken);

                return Results.Created(
                    $"/api/v1/chamados/{chamado.Id}",
                    new { chamado.Id, chamado.Numero, chamado.Status, chamado.Urgencia });
            })
            .RequireAuthorization(Politicas.Cliente)
            .DisableAntiforgery()
            .Accepts<AbrirChamadoCommand>("application/json", "multipart/form-data")
            .WithSummary("Abre um chamado de manutencao.")
            .WithDescription("Requisitos: RF0041, RN0031, RN0032, RN0034, RNF0041, RNF0042. "
                + "Envie multipart/form-data com as fotos quando a categoria exigir foto (RN0032).");

        grupo.MapPost("/{id:guid}/anexos", async (
                Guid id,
                IFormFile arquivo,
                AnexarMidiaAoChamadoHandler handler,
                CancellationToken cancellationToken) =>
            {
                await using var conteudo = arquivo.OpenReadStream();

                var anexo = await handler.ExecutarAsync(
                    id,
                    new ArquivoRecebido(arquivo.FileName, arquivo.ContentType, arquivo.Length, conteudo),
                    cancellationToken);

                return Results.Created($"/api/v1/chamados/{id}/anexos/{anexo.Id}", anexo);
            })
            .RequireAuthorization(Politicas.Cliente)
            .DisableAntiforgery()
            .WithSummary("Anexa foto ou video ao chamado.")
            .WithDescription("Requisitos: RF0042, RNF0043 (5 arquivos por chamado, 10 MB cada).");

        grupo.MapGet("/meus", async (
                ConsultarChamadosHandler handler,
                CancellationToken cancellationToken,
                StatusChamado? status = null,
                Urgencia? urgencia = null,
                Guid? categoriaId = null,
                DateTimeOffset? dataInicio = null,
                DateTimeOffset? dataFim = null,
                long? numero = null) =>
                Results.Ok(await handler.MeusChamadosAsync(
                    new FiltroDeChamados(
                        status,
                        urgencia,
                        categoriaId,
                        TecnicoId: null,
                        ClienteId: null,
                        dataInicio,
                        dataFim,
                        numero),
                    cancellationToken)))
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Consulta os chamados do cliente autenticado.")
            .WithDescription("Requisitos: RF0043, RNF0011.");

        grupo.MapGet("/{id:guid}", async (
                Guid id,
                ObterChamadoHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ExecutarAsync(id, cancellationToken)))
            .RequireAuthorization()
            .WithSummary("Consulta um chamado pelo identificador.")
            .WithDescription("Requisitos: RF0043, RF0044. Cliente dono, tecnico atribuido ou administrador.");

        grupo.MapPost("/{id:guid}/cancelamento", async (
                Guid id,
                CancelarChamadoHandler handler,
                CancellationToken cancellationToken) =>
            {
                await handler.ExecutarAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Cancela o chamado a pedido do cliente.")
            .WithDescription("Requisitos: RF0045, RN0033, RN0034.");

        grupo.MapPost("/{id:guid}/reabertura", async (
                Guid id,
                ReabrirChamadoHandler handler,
                CancellationToken cancellationToken) =>
            {
                await handler.ExecutarAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Solicita a reabertura de um chamado concluido.")
            .WithDescription("Requisitos: RN0034, RN0035 (ate 7 dias corridos apos a conclusao).");

        grupo.MapGet("/{id:guid}/historico-status", async (
                Guid id,
                ConsultarHistoricoDeStatusHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ExecutarAsync(id, cancellationToken)))
            .RequireAuthorization()
            .WithSummary("Consulta o historico de mudancas de status do chamado.")
            .WithDescription("Requisitos: RF0050.");

        return rotas;
    }
}
