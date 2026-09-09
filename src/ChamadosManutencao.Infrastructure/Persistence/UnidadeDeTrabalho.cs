using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Infrastructure.Persistence;

/// <summary>
/// Unidade de trabalho: confirma a transacao e so entao despacha os eventos de dominio.
/// Se o SaveChanges falhar, nenhuma notificacao e disparada.
/// </summary>
public sealed class UnidadeDeTrabalho : IUnitOfWork
{
    private readonly AppDbContext _contexto;
    private readonly IDespachanteDeEventos _despachante;

    public UnidadeDeTrabalho(AppDbContext contexto, IDespachanteDeEventos despachante)
    {
        _contexto = contexto;
        _despachante = despachante;
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        var raizes = _contexto.ChangeTracker
            .Entries<RaizDeAgregado>()
            .Where(entrada => entrada.Entity.Eventos.Count > 0)
            .Select(entrada => entrada.Entity)
            .ToList();

        var eventos = raizes.SelectMany(raiz => raiz.Eventos).ToList();

        var afetados = await _contexto.SaveChangesAsync(cancellationToken);

        foreach (var raiz in raizes)
        {
            raiz.LimparEventos();
        }

        if (eventos.Count > 0)
        {
            await _despachante.DespacharAsync(eventos, cancellationToken);
        }

        return afetados;
    }
}
