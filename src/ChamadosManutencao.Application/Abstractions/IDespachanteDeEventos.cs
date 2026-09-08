using ChamadosManutencao.Domain.Common;

namespace ChamadosManutencao.Application.Abstractions;

/// <summary>
/// Despacha os eventos de dominio acumulados nas raizes de agregado depois que a transacao
/// e confirmada. E o caminho usado por RNF0041 e RN0051.
/// </summary>
public interface IDespachanteDeEventos
{
    Task DespacharAsync(
        IReadOnlyCollection<IEventoDeDominio> eventos,
        CancellationToken cancellationToken = default);
}
