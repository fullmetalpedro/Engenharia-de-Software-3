namespace ChamadosManutencao.Domain.Common;

/// <summary>
/// Abstracao do algoritmo de hash de senha. Declarada no dominio para que
/// <see cref="Pessoas.Usuario.Autenticar"/> exista como regra de dominio sem que o
/// projeto Domain dependa do BCrypt (RNF0022).
/// </summary>
public interface IServicoDeHashDeSenha
{
    string GerarHash(string senha);

    bool Verificar(string senha, string hash);
}
