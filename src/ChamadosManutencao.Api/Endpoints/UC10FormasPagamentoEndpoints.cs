using ChamadosManutencao.Api.Seguranca;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC10FormasPagamento;
using FluentValidation;

namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// UC10 Gerenciar Formas de Pagamento.
/// Requisitos: RF0081, RNF0061.
/// </summary>
public static class UC10FormasPagamentoEndpoints
{
    public static IEndpointRouteBuilder MapearUC10FormasDePagamento(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas
            .MapGroup("/api/v1/clientes/{id:guid}/formas-pagamento")
            .WithTags("UC10 Formas de pagamento");

        grupo.MapPost("/", async (
                Guid id,
                CadastrarFormaPagamentoCommand comando,
                FormasDePagamentoHandler handler,
                IValidator<CadastrarFormaPagamentoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                var forma = await handler.CadastrarAsync(id, comando, cancellationToken);
                return Results.Created($"/api/v1/clientes/{id}/formas-pagamento/{forma.Id}", forma);
            })
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Cadastra uma forma de pagamento do cliente.")
            .WithDescription("Requisitos: RF0081, RNF0061. O numero do cartao e tokenizado e "
                + "descartado; a resposta traz apenas bandeira, quatro ultimos digitos e validade.");

        grupo.MapGet("/", async (
                Guid id,
                FormasDePagamentoHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ListarAsync(id, cancellationToken)))
            .RequireAuthorization(Politicas.ClienteOuAdministrador)
            .WithSummary("Lista as formas de pagamento ativas do cliente.")
            .WithDescription("Requisitos: RF0081.");

        grupo.MapPut("/{formaId:guid}", async (
                Guid id,
                Guid formaId,
                AlterarFormaPagamentoCommand comando,
                FormasDePagamentoHandler handler,
                IValidator<AlterarFormaPagamentoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                return Results.Ok(await handler.AlterarAsync(id, formaId, comando, cancellationToken));
            })
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Altera o apelido da forma de pagamento.")
            .WithDescription("Requisitos: RF0081.");

        grupo.MapDelete("/{formaId:guid}", async (
                Guid id,
                Guid formaId,
                FormasDePagamentoHandler handler,
                CancellationToken cancellationToken) =>
            {
                await handler.RemoverAsync(id, formaId, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Remove a forma de pagamento.")
            .WithDescription("Requisitos: RF0081. A exclusao e logica, para preservar o historico "
                + "de pagamentos.");

        grupo.MapPatch("/{formaId:guid}/principal", async (
                Guid id,
                Guid formaId,
                FormasDePagamentoHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.DefinirPrincipalAsync(id, formaId, cancellationToken)))
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Define a forma de pagamento principal.")
            .WithDescription("Requisitos: RF0081.");

        return rotas;
    }
}
