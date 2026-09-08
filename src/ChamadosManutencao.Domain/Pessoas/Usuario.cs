using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Pessoas;

/// <summary>
/// Usuario do sistema. Heranca em tabela unica (TPH) com discriminador tipo_usuario.
/// Requisitos: RN0011, RN0021, RNF0021, RNF0022, RF0013, RF0014, RF0023, RF0024.
/// </summary>
public abstract class Usuario : RaizDeAgregado
{
    protected Usuario(
        Guid id,
        string nomeCompleto,
        string cpf,
        string email,
        string telefone,
        string senhaHash,
        string? requisito)
        : base(id)
    {
        NomeCompleto = Garantir.TextoComTamanhoMaximo(nomeCompleto, 200, "nome completo", requisito);
        Cpf = Garantir.Cpf(cpf, requisito);
        Email = Garantir.Email(email, requisito);
        Telefone = Garantir.TextoComTamanhoMaximo(telefone, 20, "telefone", requisito);
        SenhaHash = Garantir.TextoPreenchido(senhaHash, "senha", "RNF0022");
        Ativo = true;
    }

    protected Usuario()
    {
        NomeCompleto = null!;
        Cpf = null!;
        Email = null!;
        Telefone = null!;
        SenhaHash = null!;
    }

    public string NomeCompleto { get; private set; }

    public string Cpf { get; private set; }

    public string Email { get; private set; }

    public string Telefone { get; private set; }

    /// <summary>Hash BCrypt da senha (RNF0022). Nunca sai em DTO nem em log.</summary>
    public string SenhaHash { get; private set; }

    public bool Ativo { get; private set; }

    public abstract PapelUsuario Papel { get; }

    /// <summary>
    /// Autentica o usuario. Usuario inativo nunca autentica, mesmo com a senha correta
    /// (RF0013, RF0023).
    /// </summary>
    public bool Autenticar(string senha, IServicoDeHashDeSenha servicoDeHash)
    {
        if (!Ativo || string.IsNullOrEmpty(senha))
        {
            return false;
        }

        return servicoDeHash.Verificar(senha, SenhaHash);
    }

    /// <summary>RF0014 / RF0024.</summary>
    public void Ativar() => Ativo = true;

    /// <summary>RF0013 / RF0023.</summary>
    public void Inativar() => Ativo = false;

    /// <summary>RF0012 / RF0022.</summary>
    public void AlterarDados(string nomeCompleto, string email, string telefone)
    {
        NomeCompleto = Garantir.TextoComTamanhoMaximo(nomeCompleto, 200, "nome completo");
        Email = Garantir.Email(email);
        Telefone = Garantir.TextoComTamanhoMaximo(telefone, 20, "telefone");
    }

    /// <summary>Troca de senha. O hash ja chega pronto da camada de aplicacao (RNF0021, RNF0022).</summary>
    public void DefinirSenhaHash(string senhaHash) =>
        SenhaHash = Garantir.TextoPreenchido(senhaHash, "senha", "RNF0022");
}
