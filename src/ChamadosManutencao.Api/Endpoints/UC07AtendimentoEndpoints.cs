using ChamadosManutencao.Api.Seguranca;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.Application.UC07Atendimento;
using FluentValidation;

namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// UC07 Executar Atendimento.
/// Requisitos: RF0054, RF0055, RF0056, RF0057, RF0082, RN0034, RN0042, RN0043, RN0051,
/// RN0071, RNF0043.
/// </summary>
public static class UC07AtendimentoEndpoints
{
    public static IEndpointRouteBuilder MapearUC07Atendimento(this IEndpointRouteBuilder rotas)
    {
        var chamados = rotas.MapGroup("/api/v1/chamados").WithTags("UC07 Atendimento");

        chamados.MapPost("/{id:guid}/atendimento", async (
                Guid id,
                IniciarAtendimentoHandler handler,
                CancellationToken cancellationToken) =>
            {
                var atendimento = await handler.ExecutarAsync(id, cancellationToken);
                return Results.Created($"/api/v1/atendimentos/{atendimento.Id}", atendimento);
            })
            .RequireAuthorization(Politicas.Tecnico)
            .WithSummary("Registra o inicio do atendimento no local do imovel.")
            .WithDescription("Requisitos: RF0054, RN0034.");

        var atendimentos = rotas.MapGroup("/api/v1/atendimentos").WithTags("UC07 Atendimento");

        atendimentos.MapPost("/{id:guid}/orcamento", async (
                Guid id,
                RegistrarOrcamentoCommand comando,
                RegistrarOrcamentoHandler handler,
                IValidator<RegistrarOrcamentoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                var orcamento = await handler.ExecutarAsync(id, comando, cancellationToken);
                return Results.Created($"/api/v1/atendimentos/{id}/orcamento", orcamento);
            })
            .RequireAuthorization(Politicas.Tecnico)
            .WithSummary("Registra o orcamento de pecas e mao de obra.")
            .WithDescription("Requisitos: RF0055, RN0043 (48 horas para a decisao do cliente).");

        atendimentos.MapGet("/{id:guid}/orcamento", async (
                Guid id,
                ConsultarOrcamentoHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.ExecutarAsync(id, cancellationToken)))
            .RequireAuthorization()
            .WithSummary("Consulta os orcamentos do atendimento.")
            .WithDescription("Requisitos: RF0055.");

        atendimentos.MapPost("/{id:guid}/conclusao", async (
                Guid id,
                HttpRequest requisicao,
                ConcluirAtendimentoHandler handler,
                IValidator<ConcluirAtendimentoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                ConcluirAtendimentoCommand comando;
                List<ArquivoRecebido> fotos = [];

                if (LeitorDeFormulario.EhFormulario(requisicao))
                {
                    var formulario = await requisicao.ReadFormAsync(cancellationToken);
                    comando = new ConcluirAtendimentoCommand(formulario["relatoTecnico"].ToString());
                    fotos = LeitorDeFormulario.LerArquivos(formulario);
                }
                else
                {
                    comando = await requisicao.ReadFromJsonAsync<ConcluirAtendimentoCommand>(cancellationToken)
                        ?? throw new ValidacaoException("corpo", "Corpo da requisicao ausente ou invalido.");
                }

                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);

                return Results.Ok(await handler.ExecutarAsync(id, comando, fotos, cancellationToken));
            })
            .RequireAuthorization(Politicas.Tecnico)
            .DisableAntiforgery()
            .Accepts<ConcluirAtendimentoCommand>("application/json", "multipart/form-data")
            .WithSummary("Registra a conclusao do atendimento, com as fotos do servico finalizado.")
            .WithDescription("Requisitos: RF0057, RF0082, RN0034, RN0051, RN0071, RNF0043. "
                + "Cria a garantia de 90 dias e, quando o chamado nao e de garantia, gera a fatura.");

        var orcamentos = rotas.MapGroup("/api/v1/orcamentos").WithTags("UC07 Atendimento");

        orcamentos.MapPost("/{id:guid}/decisao", async (
                Guid id,
                DecisaoDeOrcamentoCommand comando,
                DecidirOrcamentoHandler handler,
                IValidator<DecisaoDeOrcamentoCommand> validator,
                CancellationToken cancellationToken) =>
            {
                await Validacao.GarantirValidoAsync(validator, comando, cancellationToken);
                return Results.Ok(await handler.ExecutarAsync(id, comando, cancellationToken));
            })
            .RequireAuthorization(Politicas.Cliente)
            .WithSummary("Aprova ou recusa o orcamento.")
            .WithDescription("Requisitos: RF0056, RN0042, RN0043.");

        return rotas;
    }
}
