using System.Diagnostics;

namespace ChamadosManutencao.Api.Middlewares;

/// <summary>
/// RNF0011: mede a duracao de cada requisicao e emite log Warning acima de 1000 ms.
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
