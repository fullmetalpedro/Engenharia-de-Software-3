using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Eventos;
using ChamadosManutencao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChamadosManutencao.Infrastructure.Notificacao;

/// <summary>
/// Roteia os eventos de dominio para os consumidores. Hoje o unico consumidor e o notificador
/// (RNF0041, RN0051); acrescentar outro nao exige mudanca no dominio.
/// </summary>
public sealed class DespachanteDeEventos : IDespachanteDeEventos
{
    private readonly AppDbContext _contexto;
    private readonly INotificador _notificador;
    private readonly ILogger<DespachanteDeEventos> _log;

    public DespachanteDeEventos(
        AppDbContext contexto,
        INotificador notificador,
        ILogger<DespachanteDeEventos> log)
    {
        _contexto = contexto;
        _notificador = notificador;
        _log = log;
    }

    public async Task DespacharAsync(
        IReadOnlyCollection<IEventoDeDominio> eventos,
        CancellationToken cancellationToken = default)
    {
        foreach (var evento in eventos)
        {
            switch (evento)
            {
                case StatusDoChamadoAlterado alterado:
                    await NotificarStatusAsync(alterado, cancellationToken);
                    break;

                case AtendimentoConcluido concluido:
                    await SolicitarAvaliacaoAsync(concluido, cancellationToken);
                    break;

                case FaturaVencida vencida:
                    await NotificarFaturaAsync(vencida, cancellationToken);
                    break;

                default:
                    _log.LogDebug("Evento {Evento} sem consumidor registrado.", evento.GetType().Name);
                    break;
            }
        }
    }

    private async Task NotificarStatusAsync(
        StatusDoChamadoAlterado evento,
        CancellationToken cancellationToken)
    {
        var chamado = await _contexto.Chamados
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == evento.ChamadoId, cancellationToken);

        if (chamado is not null)
        {
            await _notificador.NotificarMudancaDeStatusAsync(
                chamado,
                evento.StatusAnterior,
                cancellationToken);
        }
    }

    private async Task SolicitarAvaliacaoAsync(
        AtendimentoConcluido evento,
        CancellationToken cancellationToken)
    {
        var chamado = await _contexto.Chamados
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == evento.ChamadoId, cancellationToken);

        if (chamado is not null)
        {
            await _notificador.SolicitarAvaliacaoAsync(chamado, cancellationToken);
        }
    }

    private async Task NotificarFaturaAsync(FaturaVencida evento, CancellationToken cancellationToken)
    {
        var fatura = await _contexto.Faturas
            .AsNoTracking()
            .SingleOrDefaultAsync(f => f.Id == evento.FaturaId, cancellationToken);

        if (fatura is not null)
        {
            await _notificador.NotificarVencimentoDeFaturaAsync(fatura, cancellationToken);
        }
    }
}
