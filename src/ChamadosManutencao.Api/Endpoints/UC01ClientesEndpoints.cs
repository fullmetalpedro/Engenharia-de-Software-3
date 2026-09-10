using ChamadosManutencao.Api.Seguranca;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC01Clientes;
using FluentValidation;

namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// UC01 Gerenciar Cadastro de Clientes.
/// Requisitos: RF0011 a RF0016, RN0011, RN0012, RNF0011, RNF0021, RNF0022, RNF0023.
/// </summary>
public static class UC01ClientesEndpoints
{
    public static IEndpointRouteBuilder MapearUC01Clientes(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/v1/clientes").WithTags("UC01 Clientes");

        grupo.MapPost("/", async (
                CadastrarClienteCommand comando,
                CadastrarClienteHandler handler,
                IValidator<CadastrarClienteCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                var cliente = await handler.ExecutarAsync(comando, cancellationToken);
                return Results.Created($"/api/v1/clientes/{cliente.Id}", cliente);
            })
            .AllowAnonymous()
            .WithSummary("Cadastra um cliente com seus imoveis.")
            .WithDescription("Requisitos: RF0011, RN0011, RN0012, RNF0021, RNF0022, RNF0023.");

        grupo.MapGet("/", async (
                ConsultarClientesHandler handler,
                CancellationToken cancellationToken,
                string? nome = null,
                string? cpf = null,
                string? email = null,
                string? telefone = null,
                string? codigo = null,
                bool? ativo = null) =>
                Results.Ok(await handler.ExecutarAsync(
                    new FiltroDeClientes(nome, cpf, email, telefone, codigo, ativo),
                    cancellationToken)))
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Consulta clientes por filtro combinavel.")
            .WithDescription("Requisitos: RF0015, RNF0011.");

        grupo.MapGet("/{id:guid}", async (
                Guid id,
                ObterClienteHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ExecutarAsync(id, cancellationToken)))
            .RequireAuthorization(Politicas.ClienteOuAdministrador)
            .WithSummary("Consulta um cliente pelo identificador.")
            .WithDescription("Requisitos: RF0015.");

        grupo.MapPut("/{id:guid}", async (
                Guid id,
                AlterarClienteCommand comando,
                AlterarClienteHandler handler,
                IValidator<AlterarClienteCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                return Results.Ok(await handler.ExecutarAsync(id, comando, cancellationToken));
            })
            .RequireAuthorization(Politicas.ClienteOuAdministrador)
            .WithSummary("Altera os dados cadastrais do cliente.")
            .WithDescription("Requisitos: RF0012.");

        grupo.MapPatch("/{id:guid}/inativacao", async (
                Guid id,
                AlterarSituacaoDoClienteHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ExecutarAsync(id, ativar: false, cancellationToken)))
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Inativa o cadastro do cliente.")
            .WithDescription("Requisitos: RF0013.");

        grupo.MapPatch("/{id:guid}/ativacao", async (
                Guid id,
                AlterarSituacaoDoClienteHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ExecutarAsync(id, ativar: true, cancellationToken)))
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Reativa o cadastro do cliente.")
            .WithDescription("Requisitos: RF0014.");

        grupo.MapPost("/{id:guid}/imoveis", async (
                Guid id,
                ImovelCommand comando,
                ImoveisDoClienteHandler handler,
                IValidator<ImovelCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                var imovel = await handler.AdicionarAsync(id, comando, cancellationToken);
                return Results.Created($"/api/v1/clientes/{id}/imoveis/{imovel.Id}", imovel);
            })
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Cadastra um imovel do cliente.")
            .WithDescription("Requisitos: RF0016, RN0012.");

        grupo.MapGet("/{id:guid}/imoveis", async (
                Guid id,
                ImoveisDoClienteHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ListarAsync(id, cancellationToken)))
            .RequireAuthorization(Politicas.ClienteOuAdministrador)
            .WithSummary("Lista os imoveis do cliente.")
            .WithDescription("Requisitos: RF0016.");

        grupo.MapPut("/{id:guid}/imoveis/{imovelId:guid}", async (
                Guid id,
                Guid imovelId,
                ImovelCommand comando,
                ImoveisDoClienteHandler handler,
                IValidator<ImovelCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                return Results.Ok(await handler.AlterarAsync(id, imovelId, comando, cancellationToken));
            })
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Altera um imovel do cliente.")
            .WithDescription("Requisitos: RF0016, RN0012.");

        grupo.MapDelete("/{id:guid}/imoveis/{imovelId:guid}", async (
                Guid id,
                Guid imovelId,
                ImoveisDoClienteHandler handler,
                CancellationToken cancellationToken) =>
            {
                await handler.RemoverAsync(id, imovelId, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Remove um imovel do cliente.")
            .WithDescription("Requisitos: RF0016, RN0011 (bloqueia remover o ultimo imovel).");

        return rotas;
    }
}
