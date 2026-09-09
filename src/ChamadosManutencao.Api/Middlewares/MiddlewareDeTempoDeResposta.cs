using System.Diagnostics;

namespace ChamadosManutencao.Api.Middlewares;

/// <summary>
/// RNF0011: mede a duracao de cada requisicao e emite log Warning acima de 1000 ms.
/// O tempo tambem vai no cabecalho X-Tempo-De-Resposta-Ms, o que ajuda no diagnostico.
/// </summary>
public sealed class MiddlewareDeTempoDeResposta
{
    private const long LimiteEmMilissegundos = 1000;

    private readonly RequestDelegate _proximo;
    private readonly ILogger<MiddlewareDeTempoDeResposta> _log;

    public MiddlewareDeTempoDeResposta(RequestDelegate proximo, ILogger<MiddlewareDeTempoDeResposta> log)
    {
        _proximo = proximo;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        var cronometro = Stopwatch.StartNew();

        contexto.Response.OnStarting(() =>
        {
            contexto.Response.Headers["X-Tempo-De-Resposta-Ms"] =
                cronometro.ElapsedMilliseconds.ToString();
            return Task.CompletedTask;
        });

        await _proximo(contexto);

        cronometro.Stop();

        if (cronometro.ElapsedMilliseconds > LimiteEmMilissegundos)
        {
            _log.LogWarning(
                "RNF0011: {Metodo} {Rota} respondeu em {Duracao} ms, acima do limite de {Limite} ms.",
                contexto.Request.Method,
                contexto.Request.Path,
                cronometro.ElapsedMilliseconds,
                LimiteEmMilissegundos);
        }
    }
}
