using ChamadosManutencao.Api.Seguranca;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC02Tecnicos;
using ChamadosManutencao.Application.UC08Avaliacoes;
using FluentValidation;

namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// UC02 Gerenciar Cadastro de Tecnicos.
/// Requisitos: RF0021 a RF0027, RF0062, RN0021, RNF0011, RNF0031, RNF0032.
/// </summary>
public static class UC02TecnicosEndpoints
{
    public static IEndpointRouteBuilder MapearUC02Tecnicos(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/v1/tecnicos").WithTags("UC02 Tecnicos");

        grupo.MapPost("/", async (
                CadastrarTecnicoCommand comando,
                CadastrarTecnicoHandler handler,
                IValidator<CadastrarTecnicoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                var tecnico = await handler.ExecutarAsync(comando, cancellationToken);
                return Results.Created($"/api/v1/tecnicos/{tecnico.Id}", tecnico);
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Cadastra um tecnico.")
            .WithDescription("Requisitos: RF0021, RN0021, RNF0031.");

        grupo.MapGet("/", async (
                ConsultarTecnicosHandler handler,
                CancellationToken cancellationToken,
                string? nome = null,
                string? cpf = null,
                Guid? especialidadeId = null,
                string? bairro = null,
                string? cep = null,
                bool? ativo = null) =>
                Results.Ok(await handler.ExecutarAsync(
                    new FiltroDeTecnicos(nome, cpf, especialidadeId, bairro, cep, ativo),
                    cancellationToken)))
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Consulta tecnicos por filtro, incluindo especialidade e area.")
            .WithDescription("Requisitos: RF0025, RNF0011.");

        grupo.MapGet("/{id:guid}", async (
                Guid id,
                ObterTecnicoHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ExecutarAsync(id, cancellationToken)))
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Consulta um tecnico pelo identificador.")
            .WithDescription("Requisitos: RF0025.");

        grupo.MapPut("/{id:guid}", async (
                Guid id,
                AlterarTecnicoCommand comando,
                ManterTecnicoHandler handler,
                IValidator<AlterarTecnicoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                return Results.Ok(await handler.AlterarAsync(id, comando, cancellationToken));
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Altera os dados cadastrais do tecnico.")
            .WithDescription("Requisitos: RF0022.");

        grupo.MapPatch("/{id:guid}/inativacao", async (
                Guid id,
                ManterTecnicoHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.AlterarSituacaoAsync(id, ativar: false, cancellationToken)))
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Inativa o cadastro do tecnico.")
            .WithDescription("Requisitos: RF0023.");

        grupo.MapPatch("/{id:guid}/ativacao", async (
                Guid id,
                ManterTecnicoHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.AlterarSituacaoAsync(id, ativar: true, cancellationToken)))
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Reativa o cadastro do tecnico.")
            .WithDescription("Requisitos: RF0024.");

        grupo.MapPut("/{id:guid}/especialidades", async (
                Guid id,
                DefinirEspecialidadesCommand comando,
                ManterTecnicoHandler handler,
                IValidator<DefinirEspecialidadesCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                return Results.Ok(await handler.DefinirEspecialidadesAsync(id, comando, cancellationToken));
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Define as especialidades do tecnico.")
            .WithDescription("Requisitos: RF0026, RN0021.");

        grupo.MapPut("/{id:guid}/areas-atendimento", async (
                Guid id,
                DefinirAreasCommand comando,
                ManterTecnicoHandler handler,
                IValidator<DefinirAreasCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                return Results.Ok(await handler.DefinirAreasAsync(id, comando, cancellationToken));
            })
            .RequireAuthorization(Politicas.Administrador)
            .WithSummary("Define as areas de atendimento do tecnico.")
            .WithDescription("Requisitos: RF0027, RN0023.");

        grupo.MapPost("/{id:guid}/documentos", async (
                Guid id,
                IFormFile arquivo,
                AnexarDocumentoDoTecnicoHandler handler,
                CancellationToken cancellationToken) =>
            {
                await using var conteudo = arquivo.OpenReadStream();

                var documento = await handler.ExecutarAsync(
                    id,
                    arquivo.FileName,
                    arquivo.ContentType,
                    arquivo.Length,
                    conteudo,
                    cancellationToken);

                return Results.Created($"/api/v1/tecnicos/{id}/documentos/{documento.Id}", documento);
            })
            .RequireAuthorization(Politicas.Administrador)
            .DisableAntiforgery()
            .WithSummary("Anexa documento de certificacao do tecnico.")
            .WithDescription("Requisitos: RNF0032 (aceita apenas PDF, PNG e JPEG).");

        grupo.MapGet("/{id:guid}/avaliacoes", async (
                Guid id,
                ConsultarAvaliacoesDoTecnicoHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ExecutarAsync(id, cancellationToken)))
            .RequireAuthorization()
            .WithSummary("Consulta as avaliacoes recebidas pelo tecnico.")
            .WithDescription("Requisitos: RF0062. Decisao D16: qualquer usuario autenticado.");

        return rotas;
    }
}
