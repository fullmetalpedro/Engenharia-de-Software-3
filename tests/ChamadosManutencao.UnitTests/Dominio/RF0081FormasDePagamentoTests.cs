using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Financeiro;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RF0081 e RNF0061: formas de pagamento do cliente. O cartao guarda apenas bandeira, quatro
/// ultimos digitos, validade e o token da operadora; o numero completo nunca entra no modelo.
/// </summary>
[Trait("Requisito", "RF0081")]
public class RF0081FormasDePagamentoTests
{
    private static CartaoCredito Cartao(bool principal = false, string validade = "12/2030") =>
        new(
            Construtor.Id(400),
            Construtor.Id(1),
            "Cartao principal",
            principal,
            "Visa",
            "4321",
            "tok_abc123",
            validade,
            "MARIA DE SOUZA");

    private static Pix ChavePix(TipoChavePix tipo = TipoChavePix.Email, string chave = "maria@exemplo.com") =>
        new(Construtor.Id(401), Construtor.Id(1), "PIX pessoal", false, chave, tipo);

    private static Boleto BoletoPadrao() =>
        new(Construtor.Id(402), Construtor.Id(1), "Boleto", false, "Maria de Souza", "maria@exemplo.com");

    [Fact]
    public void Cartao_guarda_somente_dado_nao_sensivel()
    {
        var cartao = Cartao();

        cartao.Bandeira.ShouldBe("Visa");
        cartao.UltimosQuatroDigitos.ShouldBe("4321");
        cartao.TokenOperadora.ShouldBe("tok_abc123");
        cartao.Validade.ShouldBe("12/2030");
        cartao.Validar().ShouldBeTrue();
        cartao.Descricao().ShouldBe("Visa **** 4321");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("54321")]
    [InlineData("")]
    public void Cartao_com_final_diferente_de_quatro_digitos_e_recusado(string ultimosQuatro)
    {
        var excecao = Should.Throw<ExcecaoDeDominio>(() => new CartaoCredito(
            Construtor.Id(403),
            Construtor.Id(1),
            "Cartao",
            false,
            "Visa",
            ultimosQuatro,
            "tok_abc123",
            "12/2030",
            "MARIA DE SOUZA"));

        excecao.Requisito.ShouldBe("RNF0061");
    }

    [Theory]
    [InlineData("13/2030")]
    [InlineData("2030/12")]
    [InlineData("12-2030")]
    [InlineData("00/2030")]
    public void Cartao_com_validade_fora_do_formato_e_recusado(string validade)
    {
        var excecao = Should.Throw<ExcecaoDeDominio>(() => Cartao(validade: validade));

        excecao.Requisito.ShouldBe("RNF0061");
    }

    [Theory]
    [InlineData(TipoChavePix.Cpf, "39053344705", true)]
    [InlineData(TipoChavePix.Cpf, "390533447", false)]
    [InlineData(TipoChavePix.Email, "maria@exemplo.com", true)]
    [InlineData(TipoChavePix.Telefone, "11999990000", true)]
    [InlineData(TipoChavePix.Telefone, "1199", false)]
    [InlineData(TipoChavePix.Aleatoria, "8f1b2c3d4e5f60718293a4b5c6d7e8f9", true)]
    [InlineData(TipoChavePix.Aleatoria, "curta", false)]
    public void Chave_pix_e_validada_conforme_o_tipo(TipoChavePix tipo, string chave, bool valida)
    {
        if (valida)
        {
            ChavePix(tipo, chave).Validar().ShouldBeTrue();
            return;
        }

        Should.Throw<ExcecaoDeDominio>(() => ChavePix(tipo, chave));
    }

    [Fact]
    public void Boleto_exige_sacado_e_email_valido()
    {
        var boleto = BoletoPadrao();

        boleto.Validar().ShouldBeTrue();
        boleto.Descricao().ShouldBe("Boleto para maria@exemplo.com");

        Should.Throw<ExcecaoDeDominio>(() => new Boleto(
            Construtor.Id(404),
            Construtor.Id(1),
            "Boleto",
            false,
            "Maria",
            "email-invalido"));
    }

    [Fact]
    public void Primeira_forma_cadastrada_vira_principal_automaticamente()
    {
        var cliente = Construtor.Cliente();

        cliente.AdicionarFormaPagamento(ChavePix());

        cliente.FormasPagamento.ShouldHaveSingleItem().Principal.ShouldBeTrue();
    }

    [Fact]
    public void Cadastrar_forma_como_principal_tira_a_marca_da_anterior()
    {
        var cliente = Construtor.Cliente();
        var pix = ChavePix();
        cliente.AdicionarFormaPagamento(pix);

        var cartao = Cartao(principal: true);
        cliente.AdicionarFormaPagamento(cartao);

        cartao.Principal.ShouldBeTrue();
        pix.Principal.ShouldBeFalse();
        cliente.FormasPagamento.Count(f => f.Principal).ShouldBe(1);
    }

    [Fact]
    public void Definir_principal_move_a_marca_entre_as_formas()
    {
        var cliente = Construtor.Cliente();
        var pix = ChavePix();
        var boleto = BoletoPadrao();
        cliente.AdicionarFormaPagamento(pix);
        cliente.AdicionarFormaPagamento(boleto);

        cliente.DefinirFormaPagamentoPrincipal(boleto.Id);

        boleto.Principal.ShouldBeTrue();
        pix.Principal.ShouldBeFalse();
    }

    [Fact]
    public void Definir_principal_em_forma_inexistente_e_recusado()
    {
        var cliente = Construtor.Cliente();
        cliente.AdicionarFormaPagamento(ChavePix());

        Should.Throw<ExcecaoDeDominio>(
            () => cliente.DefinirFormaPagamentoPrincipal(Construtor.Id(999)));
    }

    [Fact]
    public void Forma_inativa_nao_pode_virar_principal()
    {
        var cliente = Construtor.Cliente();
        var pix = ChavePix();
        var boleto = BoletoPadrao();
        cliente.AdicionarFormaPagamento(pix);
        cliente.AdicionarFormaPagamento(boleto);

        cliente.RemoverFormaPagamento(boleto.Id);

        var excecao = Should.Throw<ExcecaoDeDominio>(
            () => cliente.DefinirFormaPagamentoPrincipal(boleto.Id));

        excecao.Requisito.ShouldBe("RF0081");
    }

    [Fact]
    public void Remocao_e_logica_e_passa_a_marca_de_principal_adiante()
    {
        var cliente = Construtor.Cliente();
        var pix = ChavePix();
        var boleto = BoletoPadrao();
        cliente.AdicionarFormaPagamento(pix);
        cliente.AdicionarFormaPagamento(boleto);

        cliente.RemoverFormaPagamento(pix.Id);

        pix.Ativa.ShouldBeFalse();
        pix.Principal.ShouldBeFalse();
        boleto.Principal.ShouldBeTrue();
        // A forma continua no agregado, para preservar o historico de pagamentos.
        cliente.FormasPagamento.Count.ShouldBe(2);
    }

    [Fact]
    public void Remover_forma_inexistente_e_recusado()
    {
        var cliente = Construtor.Cliente();

        Should.Throw<ExcecaoDeDominio>(() => cliente.RemoverFormaPagamento(Construtor.Id(998)));
    }

    [Fact]
    public void Apelido_pode_ser_alterado()
    {
        var pix = ChavePix();

        pix.AlterarApelido("PIX da empresa");

        pix.Apelido.ShouldBe("PIX da empresa");
        Should.Throw<ExcecaoDeDominio>(() => pix.AlterarApelido("  "));
    }
}
