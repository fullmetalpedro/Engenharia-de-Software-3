using ChamadosManutencao.Application.Autenticacao;
using ChamadosManutencao.Application.Common;
using FluentValidation;

namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// Autenticacao (transversal a todos os casos de uso).
/// Requisitos: RNF0021, RNF0022.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapearAutenticacao(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/v1/auth").WithTags("Autenticacao");

        grupo.MapPost("/login", async (
                LoginCommand comando,
                LoginHandler handler,
                IValidator<LoginCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                return Results.Ok(await handler.ExecutarAsync(comando, cancellationToken));
            })
            .AllowAnonymous()
            .WithSummary("Autentica o usuario e devolve o token JWT.")
            .WithDescription("Requisitos: RNF0022.");

        grupo.MapPost("/refresh", async (
                RefreshHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ExecutarAsync(cancellationToken)))
            .RequireAuthorization()
            .WithSummary("Reemite o token do usuario autenticado.");

        grupo.MapPost("/alterar-senha", async (
                AlterarSenhaCommand comando,
                AlterarSenhaHandler handler,
                IValidator<AlterarSenhaCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                await handler.ExecutarAsync(comando, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithSummary("Troca a senha do usuario autenticado.")
            .WithDescription("Requisitos: RNF0021, RNF0022.");

        return rotas;
    }
}
