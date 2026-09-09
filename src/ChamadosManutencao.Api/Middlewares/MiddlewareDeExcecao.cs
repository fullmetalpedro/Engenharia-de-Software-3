using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Api.Middlewares;

/// <summary>
/// Traduz as excecoes conhecidas para ProblemDetails (RFC 9457). Erros de validacao levam a
/// colecao errors; violacoes de regra de negocio levam o identificador do requisito.
/// </summary>
public sealed class MiddlewareDeExcecao
{
    private readonly RequestDelegate _proximo;
    private readonly ILogger<MiddlewareDeExcecao> _log;

    public MiddlewareDeExcecao(RequestDelegate proximo, ILogger<MiddlewareDeExcecao> log)
    {
        _proximo = proximo;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await _proximo(contexto);
        }
        catch (Exception excecao)
        {
            await EscreverProblemaAsync(contexto, excecao);
        }
    }

    private async Task EscreverProblemaAsync(HttpContext contexto, Exception excecao)
    {
        var (status, titulo, tipo) = Classificar(excecao);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _log.LogError(excecao, "Falha nao tratada em {Rota}.", contexto.Request.Path);
        }
        else
        {
            _log.LogInformation(
                "Requisicao recusada em {Rota}: {Mensagem}",
                contexto.Request.Path,
                excecao.Message);
        }

        var problema = new ProblemDetails
        {
            Type = tipo,
            Title = titulo,
            Status = status,
            Detail = status >= StatusCodes.Status500InternalServerError
                ? "Ocorreu um erro inesperado ao processar a requisicao."
                : excecao.Message,
            Instance = contexto.Request.Path
        };

        switch (excecao)
        {
            case ValidacaoException validacao:
                problema.Extensions["errors"] = validacao.Erros;
                break;

            case FluentValidation.ValidationException fluent:
                problema.Extensions["errors"] = fluent.Errors
                    .GroupBy(erro => erro.PropertyName)
                    .ToDictionary(
                        grupo => grupo.Key,
                        grupo => grupo.Select(erro => erro.ErrorMessage).ToArray());
                problema.Detail = "Um ou mais campos estao invalidos.";
                break;

            case ExcecaoDeDominio dominio when dominio.Requisito is not null:
                problema.Extensions["requisito"] = dominio.Requisito;
                break;

            case ConflitoException conflito when conflito.Requisito is not null:
                problema.Extensions["requisito"] = conflito.Requisito;
                break;
        }

        contexto.Response.Clear();
        contexto.Response.StatusCode = status;
        contexto.Response.ContentType = "application/problem+json";

        await contexto.Response.WriteAsJsonAsync(problema);
    }

    private static (int Status, string Titulo, string Tipo) Classificar(Exception excecao) => excecao switch
    {
        // Parametro de rota ou de query que o proprio ASP.NET nao conseguiu converter
        // (data, GUID ou enum malformado). Sem este caso o erro sairia como 500.
        BadHttpRequestException requisicao => (
            requisicao.StatusCode,
            "Requisicao malformada",
            "https://tools.ietf.org/html/rfc9110#section-15.5.1"),

        ValidacaoException or FluentValidation.ValidationException => (
            StatusCodes.Status400BadRequest,
            "Falha de validacao",
            "https://tools.ietf.org/html/rfc9110#section-15.5.1"),

        CredenciaisInvalidasException => (
            StatusCodes.Status401Unauthorized,
            "Nao autenticado",
            "https://tools.ietf.org/html/rfc9110#section-15.5.2"),

        AcessoNegadoException => (
            StatusCodes.Status403Forbidden,
            "Acesso negado",
            "https://tools.ietf.org/html/rfc9110#section-15.5.4"),

        RecursoNaoEncontradoException => (
            StatusCodes.Status404NotFound,
            "Recurso nao encontrado",
            "https://tools.ietf.org/html/rfc9110#section-15.5.5"),

        ConflitoException or DbUpdateConcurrencyException => (
            StatusCodes.Status409Conflict,
            "Conflito com o estado atual do recurso",
            "https://tools.ietf.org/html/rfc9110#section-15.5.10"),

        TransicaoDeStatusInvalidaException => (
            StatusCodes.Status422UnprocessableEntity,
            "Transicao de status invalida",
            "https://tools.ietf.org/html/rfc9110#section-15.5.21"),

        ExcecaoDeDominio => (
            StatusCodes.Status422UnprocessableEntity,
            "Regra de negocio violada",
            "https://tools.ietf.org/html/rfc9110#section-15.5.21"),

        _ => (
            StatusCodes.Status500InternalServerError,
            "Erro interno",
            "https://tools.ietf.org/html/rfc9110#section-15.6.1")
    };
}
