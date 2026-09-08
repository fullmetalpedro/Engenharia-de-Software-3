using ChamadosManutencao.Domain.Common;

namespace ChamadosManutencao.UnitTests.Common;

/// <summary>
/// Hash de senha reversivel usado apenas em teste unitario, para nao pagar o custo do BCrypt
/// com work factor 12 em cada assercao.
/// </summary>
public sealed class HashFalso : IServicoDeHashDeSenha
{
    public string GerarHash(string senha) => $"hash::{senha}";

    public bool Verificar(string senha, string hash) => hash == $"hash::{senha}";
}
