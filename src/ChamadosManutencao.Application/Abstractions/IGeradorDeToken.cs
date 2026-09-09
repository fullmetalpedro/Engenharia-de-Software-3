using ChamadosManutencao.Domain.Pessoas;

namespace ChamadosManutencao.Application.Abstractions;

/// <summary>
/// Emissao do token JWT. A implementacao vive na Api, junto da configuracao de autenticacao.
/// </summary>
public interface IGeradorDeToken
{
    TokenEmitido Emitir(Usuario usuario);
}

/// <summary>Token devolvido pelo login e pelo refresh.</summary>
public sealed record TokenEmitido(string AccessToken, DateTimeOffset ExpiraEm, string Papel);
