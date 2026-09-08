using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Domain.Common;

namespace ChamadosManutencao.Infrastructure.Servicos;

/// <summary>Relogio do sistema. Unico ponto do codigo que le a hora real.</summary>
public sealed class RelogioDoSistema : IRelogio
{
    public DateTimeOffset Agora => DateTimeOffset.UtcNow;
}

/// <summary>
/// Gerador de identificadores. Guid v7 e ordenavel no tempo, o que evita fragmentacao de
/// indice em chave primaria aleatoria.
/// </summary>
public sealed class GeradorIdV7 : IGeradorId
{
    public Guid NovoId() => Guid.CreateVersion7();
}

/// <summary>RNF0022: hash de senha com BCrypt, work factor 12.</summary>
public sealed class ServicoDeHashBCrypt : IServicoDeHashDeSenha
{
    private const int FatorDeTrabalho = 12;

    public string GerarHash(string senha) =>
        BCrypt.Net.BCrypt.HashPassword(senha, workFactor: FatorDeTrabalho);

    public bool Verificar(string senha, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(senha, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}

/// <summary>
/// RNF0021: senha com no minimo 8 caracteres, maiuscula, minuscula e caractere especial.
/// A mesma politica vale para o cadastro e para a troca de senha.
/// </summary>
public sealed class PoliticaDeSenhaForte : IPoliticaDeSenha
{
    public const int TamanhoMinimo = 8;

    public bool EhForte(string senha, out string? motivo)
    {
        if (string.IsNullOrWhiteSpace(senha) || senha.Length < TamanhoMinimo)
        {
            motivo = $"A senha deve ter ao menos {TamanhoMinimo} caracteres.";
            return false;
        }

        if (!senha.Any(char.IsUpper))
        {
            motivo = "A senha deve conter ao menos uma letra maiuscula.";
            return false;
        }

        if (!senha.Any(char.IsLower))
        {
            motivo = "A senha deve conter ao menos uma letra minuscula.";
            return false;
        }

        if (!senha.Any(caractere => !char.IsLetterOrDigit(caractere)))
        {
            motivo = "A senha deve conter ao menos um caractere especial.";
            return false;
        }

        motivo = null;
        return true;
    }
}
