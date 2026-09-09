using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChamadosManutencao.Infrastructure.Jobs;

/// <summary>
/// Base dos jobs de segundo plano. Cada execucao roda em um escopo proprio de DI, usa
/// <c>IRelogio</c> como fonte de tempo e registra o que processou. Uma falha em uma execucao
/// nao derruba o host: o erro e registrado e o job tenta de novo no proximo intervalo.
/// </summary>
public abstract class JobPeriodico : BackgroundService
{
    /// <summary>Intervalo padrao entre execucoes (secao 8 da arquitetura).</summary>
    public static readonly TimeSpan IntervaloPadrao = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _fabricaDeEscopo;
    private readonly ILogger _log;

    protected JobPeriodico(IServiceScopeFactory fabricaDeEscopo, ILogger log)
    {
        _fabricaDeEscopo = fabricaDeEscopo;
        _log = log;
    }

    /// <summary>Nome usado nos logs.</summary>
    protected abstract string Nome { get; }

    protected virtual TimeSpan Intervalo => IntervaloPadrao;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(Intervalo);

        do
        {
            try
            {
                using var escopo = _fabricaDeEscopo.CreateScope();
                var processados = await ProcessarAsync(escopo.ServiceProvider, stoppingToken);

                if (processados > 0)
                {
                    _log.LogInformation("{Job}: {Processados} registro(s) processado(s).", Nome, processados);
                }
                else
                {
                    _log.LogDebug("{Job}: nada a processar.", Nome);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception excecao)
            {
                _log.LogError(excecao, "{Job}: falha na execucao. Nova tentativa em {Intervalo}.", Nome, Intervalo);
            }
        }
        while (await EsperarAsync(temporizador, stoppingToken));
    }

    /// <summary>
    /// Devolve quantos registros a execucao processou. E publico para que o teste de
    /// integracao possa disparar uma execucao sem esperar o intervalo.
    /// </summary>
    public abstract Task<int> ProcessarAsync(
        IServiceProvider provedor,
        CancellationToken cancellationToken);

    private static async Task<bool> EsperarAsync(PeriodicTimer temporizador, CancellationToken stoppingToken)
    {
        try
        {
            return await temporizador.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
