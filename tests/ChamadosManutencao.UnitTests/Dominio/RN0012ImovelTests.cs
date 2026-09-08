using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Pessoas;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0012: o imovel exige apelido, tipo, logradouro, numero, bairro, CEP, cidade e estado.
/// O complemento e opcional.
/// </summary>
[Trait("Requisito", "RN0012")]
public class RN0012ImovelTests
{
    [Fact]
    public void Imovel_completo_e_criado_e_o_complemento_e_opcional()
    {
        var imovel = Construtor.Imovel(Construtor.Id(1));

        imovel.Apelido.ShouldBe("Casa");
        imovel.TipoImovel.ShouldBe(TipoImovel.Casa);
        imovel.Cep.ShouldBe("04567000");
        imovel.Estado.ShouldBe("SP");
        imovel.Complemento.ShouldBeNull();
    }

    [Theory]
    [InlineData("", "Rua A", "10", "Centro", "04567000", "Sao Paulo", "SP")]
    [InlineData("Casa", "", "10", "Centro", "04567000", "Sao Paulo", "SP")]
    [InlineData("Casa", "Rua A", "", "Centro", "04567000", "Sao Paulo", "SP")]
    [InlineData("Casa", "Rua A", "10", "", "04567000", "Sao Paulo", "SP")]
    [InlineData("Casa", "Rua A", "10", "Centro", "123", "Sao Paulo", "SP")]
    [InlineData("Casa", "Rua A", "10", "Centro", "04567000", "", "SP")]
    [InlineData("Casa", "Rua A", "10", "Centro", "04567000", "Sao Paulo", "Sao Paulo")]
    public void Imovel_com_composicao_incompleta_e_recusado(
        string apelido,
        string logradouro,
        string numero,
        string bairro,
        string cep,
        string cidade,
        string estado)
    {
        var excecao = Should.Throw<ExcecaoDeDominio>(() => new Imovel(
            Construtor.Id(100),
            Construtor.Id(1),
            apelido,
            TipoImovel.Casa,
            logradouro,
            numero,
            null,
            bairro,
            cep,
            cidade,
            estado));

        excecao.Requisito.ShouldBe("RN0012");
    }

    [Fact]
    public void Cep_e_normalizado_para_somente_digitos()
    {
        var imovel = Construtor.Imovel(Construtor.Id(1), "04567-000");

        imovel.Cep.ShouldBe("04567000");
    }
}
