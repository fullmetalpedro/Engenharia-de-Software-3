using System.Text.Json;
using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ChamadosManutencao.Infrastructure.Auditoria;

/// <summary>
/// RNF0012: grava em log_transacao data/hora, usuario, entidade, chave, operacao e o JSON do
/// estado anterior e do novo, para toda insercao, alteracao e exclusao.
///
/// O campo senha_hash nunca entra no log (RNF0022).
/// </summary>
public sealed class InterceptorDeAuditoria : SaveChangesInterceptor
{
    private static readonly string[] CamposSensiveis = ["SenhaHash", "TokenOperadora"];

    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IRelogio _relogio;
    private readonly IGeradorId _geradorId;

    public InterceptorDeAuditoria(IUsuarioAtual usuarioAtual, IRelogio relogio, IGeradorId geradorId)
    {
        _usuarioAtual = usuarioAtual;
        _relogio = relogio;
        _geradorId = geradorId;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            Auditar(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            Auditar(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void Auditar(DbContext contexto)
    {
        var entradas = contexto.ChangeTracker
            .Entries()
            .Where(entrada =>
                entrada.Entity is not LogTransacao
                && entrada.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entradas.Count == 0)
        {
            return;
        }

        var registros = new List<LogTransacao>(entradas.Count);

        foreach (var entrada in entradas)
        {
            registros.Add(new LogTransacao
            {
                Id = _geradorId.NovoId(),
                DataHora = _relogio.Agora,
                UsuarioId = _usuarioAtual.Id,
                Entidade = entrada.Metadata.ClrType.Name,
                ChavePrimaria = ChaveDe(entrada),
                Operacao = entrada.State switch
                {
                    EntityState.Added => "Insercao",
                    EntityState.Modified => "Alteracao",
                    _ => "Exclusao"
                },
                EstadoAnterior = entrada.State == EntityState.Added ? null : Serializar(entrada, valorOriginal: true),
                EstadoNovo = entrada.State == EntityState.Deleted ? null : Serializar(entrada, valorOriginal: false)
            });
        }

        contexto.Set<LogTransacao>().AddRange(registros);
    }

    private static string ChaveDe(EntityEntry entrada)
    {
        var chave = entrada.Metadata.FindPrimaryKey();

        if (chave is null)
        {
            return string.Empty;
        }

        var valores = chave.Properties
            .Select(propriedade => entrada.Property(propriedade.Name).CurrentValue?.ToString() ?? string.Empty);

        return string.Join('|', valores);
    }

    private static string Serializar(EntityEntry entrada, bool valorOriginal)
    {
        var estado = new Dictionary<string, object?>();

        foreach (var propriedade in entrada.Properties)
        {
            var nome = propriedade.Metadata.Name;

            if (CamposSensiveis.Contains(nome))
            {
                estado[nome] = "***";
                continue;
            }

            estado[nome] = valorOriginal ? propriedade.OriginalValue : propriedade.CurrentValue;
        }

        return JsonSerializer.Serialize(estado);
    }
}
