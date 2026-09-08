using ChamadosManutencao.Api.Seguranca;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC03Catalogo;
using FluentValidation;

namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// UC03 Gerenciar Catalogo de Servicos.
/// Requisitos: RF0031 a RF0034, RNF0011.
/// </summary>
public static class UC03CatalogoEndpoints
{
    public static IEndpointRouteBuilder MapearUC03Catalogo(this IEndpointRouteBuilder rotas)
    {
        var categorias = rotas.MapGroup("/api/v1/categorias-servico").WithTags("UC03 Catalogo");

        categorias.MapPost("/", async (
                CategoriaCommand comando,
                ManterCatalogoHandler handler,
                IValidator<CategoriaCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                var categoria = await handler.CadastrarCategoriaAsync(comando, cancellationToken);
                return Results.Created($"/api/v1/categorias-servico/{categoria.Id}", categoria);
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Cadastra uma categoria de servico.")
            .WithDescription("Requisitos: RF0031.");

        categorias.MapGet("/", async (
                ConsultarCatalogoHandler handler,
                CancellationToken cancellationToken,
                bool? ativa = null,
                int? page = null,
                int? pageSize = null) =>
                Results.Ok(await handler.ConsultarCategoriasAsync(ativa, page, pageSize, cancellationToken)))
            .RequireAuthorization()
            .WithSummary("Consulta as categorias de servico.")
            .WithDescription("Requisitos: RF0034, RNF0011.");

        categorias.MapPut("/{id:guid}", async (
                Guid id,
                CategoriaCommand comando,
                ManterCatalogoHandler handler,
                IValidator<CategoriaCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                return Results.Ok(await handler.AlterarCategoriaAsync(id, comando, cancellationToken));
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Altera uma categoria de servico.")
            .WithDescription("Requisitos: RF0033.");

        categorias.MapPost("/{id:guid}/tipos-servico", async (
                Guid id,
                TipoServicoCommand comando,
                ManterCatalogoHandler handler,
                IValidator<TipoServicoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                var tipo = await handler.CadastrarTipoServicoAsync(id, comando, cancellationToken);
                return Results.Created($"/api/v1/tipos-servico/{tipo.Id}", tipo);
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Cadastra um tipo de servico na categoria.")
            .WithDescription("Requisitos: RF0032.");

        var tipos = rotas.MapGroup("/api/v1/tipos-servico").WithTags("UC03 Catalogo");

        tipos.MapGet("/", async (
                ConsultarCatalogoHandler handler,
                CancellationToken cancellationToken,
                Guid? categoriaId = null,
                bool? ativo = null,
                int? page = null,
                int? pageSize = null) =>
                Results.Ok(await handler.ConsultarTiposDeServicoAsync(
                    categoriaId,
                    ativo,
                    page,
                    pageSize,
                    cancellationToken)))
            .RequireAuthorization()
            .WithSummary("Consulta os tipos de servico.")
            .WithDescription("Requisitos: RF0034, RNF0011.");

        tipos.MapPut("/{id:guid}", async (
                Guid id,
                TipoServicoCommand comando,
                ManterCatalogoHandler handler,
                IValidator<TipoServicoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                return Results.Ok(await handler.AlterarTipoServicoAsync(id, comando, cancellationToken));
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Altera um tipo de servico.")
            .WithDescription("Requisitos: RF0033.");

        return rotas;
    }
}
