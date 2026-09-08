using ChamadosManutencao.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ChamadosManutencao.Infrastructure.Persistence;

/// <summary>
/// Numeros e codigos sequenciais vindos das sequences do PostgreSQL (RNF0023, RNF0031,
/// RNF0042). Nunca MAX(numero) + 1: com concorrencia isso geraria numero duplicado.
/// </summary>
public sealed class GeradorDeSequencias : IGeradorDeSequencias
{
    private readonly AppDbContext _contexto;

    public GeradorDeSequencias(AppDbContext contexto) => _contexto = contexto;

    public Task<long> ProximoNumeroDeChamadoAsync(CancellationToken cancellationToken = default) =>
        ProximoValorAsync(AppDbContext.SequenceNumeroChamado, cancellationToken);

    public Task<long> ProximoNumeroDeFaturaAsync(CancellationToken cancellationToken = default) =>
        ProximoValorAsync(AppDbContext.SequenceNumeroFatura, cancellationToken);

    public async Task<string> ProximoCodigoDeClienteAsync(CancellationToken cancellationToken = default)
    {
        var valor = await ProximoValorAsync(AppDbContext.SequenceCodigoCliente, cancellationToken);
        return $"CLI-{valor:000000}";
    }

    public async Task<string> ProximoCodigoDeTecnicoAsync(CancellationToken cancellationToken = default)
    {
        var valor = await ProximoValorAsync(AppDbContext.SequenceCodigoTecnico, cancellationToken);
        return $"TEC-{valor:000000}";
    }

    private async Task<long> ProximoValorAsync(string sequence, CancellationToken cancellationToken)
    {
        await using var comando = _contexto.Database.GetDbConnection().CreateCommand();

        var conexao = _contexto.Database.GetDbConnection();

        if (conexao.State != System.Data.ConnectionState.Open)
        {
            await _contexto.Database.OpenConnectionAsync(cancellationToken);
        }

        comando.CommandText = $"SELECT nextval('\"{sequence}\"')";

        var transacao = _contexto.Database.CurrentTransaction;

        if (transacao is not null)
        {
            comando.Transaction = transacao.GetDbTransaction();
        }

        var resultado = await comando.ExecuteScalarAsync(cancellationToken);

        return Convert.ToInt64(resultado);
    }
}
