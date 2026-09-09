using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Pessoas;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RNF0022: a senha so existe como hash, e a autenticacao passa pelo servico de hash.
/// RF0013, RF0014, RF0023 e RF0024: usuario inativo nao autentica.
/// </summary>
[Trait("Requisito", "RNF0022")]
public class RNF0022AutenticacaoTests
{
    private static readonly IServicoDeHashDeSenha Hash = new HashFalso();

    [Fact]
    public void Senha_correta_autentica_usuario_ativo()
    {
        var cliente = Construtor.Cliente();

        cliente.Autenticar("Senha@123", Hash).ShouldBeTrue();
    }

    [Fact]
    public void Senha_errada_nao_autentica()
    {
        var cliente = Construtor.Cliente();

        cliente.Autenticar("outra-senha", Hash).ShouldBeFalse();
    }

    [Fact]
    public void Senha_vazia_nao_autentica()
    {
        var cliente = Construtor.Cliente();

        cliente.Autenticar(string.Empty, Hash).ShouldBeFalse();
    }

    [Fact]
    public void Usuario_inativo_nao_autentica_mesmo_com_a_senha_correta()
    {
        var cliente = Construtor.Cliente();

        cliente.Inativar();

        cliente.Ativo.ShouldBeFalse();
        cliente.Autenticar("Senha@123", Hash).ShouldBeFalse();
    }

    [Fact]
    public void Reativacao_devolve_o_acesso()
    {
        var cliente = Construtor.Cliente();
        cliente.Inativar();

        cliente.Ativar();

        cliente.Autenticar("Senha@123", Hash).ShouldBeTrue();
    }

    [Fact]
    public void Troca_de_senha_muda_a_credencial_aceita()
    {
        var cliente = Construtor.Cliente();

        cliente.DefinirSenhaHash(Hash.GerarHash("NovaSenha@456"));

        cliente.Autenticar("Senha@123", Hash).ShouldBeFalse();
        cliente.Autenticar("NovaSenha@456", Hash).ShouldBeTrue();
    }

    [Fact]
    public void Hash_vazio_e_recusado()
    {
        var cliente = Construtor.Cliente();

        var excecao = Should.Throw<ExcecaoDeDominio>(() => cliente.DefinirSenhaHash("  "));

        excecao.Requisito.ShouldBe("RNF0022");
    }

    [Fact]
    public void Alterar_dados_normaliza_email_e_mantem_cpf()
    {
        var cliente = Construtor.Cliente();

        cliente.AlterarDados("Maria de Souza Lima", "MARIA.LIMA@Exemplo.COM", "11912345678");

        cliente.NomeCompleto.ShouldBe("Maria de Souza Lima");
        cliente.Email.ShouldBe("maria.lima@exemplo.com");
        cliente.Telefone.ShouldBe("11912345678");
        cliente.Cpf.ShouldBe("39053344705");
    }

    [Fact]
    public void Alterar_dados_com_email_invalido_e_recusado()
    {
        var cliente = Construtor.Cliente();

        Should.Throw<ExcecaoDeDominio>(
            () => cliente.AlterarDados("Maria", "sem-arroba", "11912345678"));
    }

    [Fact]
    public void Papel_vem_do_tipo_concreto_do_usuario()
    {
        Construtor.Cliente().Papel.ShouldBe(PapelUsuario.Cliente);
        Construtor.Tecnico().Papel.ShouldBe(PapelUsuario.Tecnico);
        Administrador().Papel.ShouldBe(PapelUsuario.Administrador);
    }

    [Fact]
    public void Administrador_exige_matricula()
    {
        Administrador().Matricula.ShouldBe("ADM-000001");

        Should.Throw<ExcecaoDeDominio>(() => new Administrador(
            Construtor.Id(9),
            "   ",
            "Ana Ribeiro",
            "39053344705",
            "ana@exemplo.com",
            "11977770000",
            "hash::Senha@123"));
    }

    [Fact]
    public void Administrador_autentica_como_qualquer_usuario()
    {
        Administrador().Autenticar("Senha@123", Hash).ShouldBeTrue();
    }

    private static Administrador Administrador() => new(
        Construtor.Id(9),
        "ADM-000001",
        "Ana Ribeiro",
        "39053344705",
        "ana@exemplo.com",
        "11977770000",
        "hash::Senha@123");
}
