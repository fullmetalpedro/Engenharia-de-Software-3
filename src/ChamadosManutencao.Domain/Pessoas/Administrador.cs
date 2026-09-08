using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Pessoas;

/// <summary>
/// Administrador da empresa. Responsavel por triagem, atribuicao e analise.
/// </summary>
public sealed class Administrador : Usuario
{
    public Administrador(
        Guid id,
        string matricula,
        string nomeCompleto,
        string cpf,
        string email,
        string telefone,
        string senhaHash)
        : base(id, nomeCompleto, cpf, email, telefone, senhaHash, null)
    {
        Matricula = Garantir.TextoComTamanhoMaximo(matricula, 20, "matricula");
    }

    private Administrador()
    {
        Matricula = null!;
    }

    public string Matricula { get; private set; }

    public override PapelUsuario Papel => PapelUsuario.Administrador;
}
