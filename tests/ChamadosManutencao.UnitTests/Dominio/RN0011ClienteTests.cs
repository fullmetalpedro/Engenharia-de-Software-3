using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Pessoas;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0011: nome completo, CPF, telefone, e-mail, senha e ao menos um imovel sao obrigatorios
/// no cadastro do cliente.
/// </summary>
[Trait("Requisito", "RN0011")]
public class RN0011ClienteTests
{
    [Fact]
    public void Cliente_com_todos_os_dados_e_um_imovel_e_criado()
    {
        var cliente = Construtor.Cliente();

        cliente.NomeCompleto.ShouldBe("Maria de Souza");
        cliente.Cpf.ShouldBe("39053344705");
        cliente.Email.ShouldBe("maria@exemplo.com");
        cliente.Imoveis.Count.ShouldBe(1);
        cliente.Ativo.ShouldBeTrue();
        cliente.CodigoCliente.ShouldBe("CLI-000001");
    }

    [Fact]
    public void Cliente_sem_imovel_e_recusado()
    {
        var excecao = Should.Throw<ExcecaoDeDominio>(() => new Cliente(
            Construtor.Id(1),
            "CLI-000002",
            "Maria de Souza",
            "39053344705",
            "maria@exemplo.com",
            "11999990000",
            "hash::Senha@123",
            []));

        excecao.Requisito.ShouldBe("RN0011");
    }

    [Theory]
    [InlineData("", "39053344705", "maria@exemplo.com", "11999990000")]
    [InlineData("Maria", "123", "maria@exemplo.com", "11999990000")]
    [InlineData("Maria", "39053344705", "email-invalido", "11999990000")]
    [InlineData("Maria", "39053344705", "maria@exemplo.com", "")]
    public void Cliente_com_dado_obrigatorio_invalido_e_recusado(
        string nome,
        string cpf,
        string email,
        string telefone)
    {
        var clienteId = Construtor.Id(1);

        Should.Throw<ExcecaoDeDominio>(() => new Cliente(
            clienteId,
            "CLI-000003",
            nome,
            cpf,
            email,
            telefone,
            "hash::Senha@123",
            [Construtor.Imovel(clienteId)]));
    }

    [Fact]
    public void Remover_o_ultimo_imovel_e_bloqueado()
    {
        var cliente = Construtor.Cliente();
        var imovelId = cliente.Imoveis.Single().Id;

        var excecao = Should.Throw<ExcecaoDeDominio>(() => cliente.RemoverImovel(imovelId));

        excecao.Requisito.ShouldBe("RN0011");
        cliente.Imoveis.Count.ShouldBe(1);
    }

    [Fact]
    public void Remover_imovel_e_permitido_quando_sobra_outro()
    {
        var cliente = Construtor.Cliente();
        var segundo = new Imovel(
            Construtor.Id(101),
            cliente.Id,
            "Apartamento da praia",
            Domain.Enums.TipoImovel.Apartamento,
            "Avenida Beira Mar",
            "500",
            "Apto 42",
            "Centro",
            "11700000",
            "Praia Grande",
            "SP");

        cliente.AdicionarImovel(segundo);
        cliente.RemoverImovel(segundo.Id);

        cliente.Imoveis.Count.ShouldBe(1);
    }
}
