using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0022: o tecnico so recebe chamado cuja categoria de servico esteja entre suas
/// especialidades.
/// </summary>
[Trait("Requisito", "RN0022")]
public class RN0022EspecialidadeTests
{
    [Fact]
    public void Tecnico_atende_categoria_que_e_sua_especialidade()
    {
        var categoria = Construtor.Categoria(id: Construtor.Id(20));
        var tecnico = Construtor.Tecnico(categoria);

        tecnico.AtendeCategoria(categoria.Id).ShouldBeTrue();
    }

    [Fact]
    public void Tecnico_nao_atende_categoria_fora_das_especialidades()
    {
        var especialidade = Construtor.Categoria(id: Construtor.Id(20));
        var outraCategoria = Construtor.Categoria(id: Construtor.Id(21));
        var tecnico = Construtor.Tecnico(especialidade);

        tecnico.AtendeCategoria(outraCategoria.Id).ShouldBeFalse();
    }

    [Fact]
    public void Atribuicao_de_tecnico_sem_a_especialidade_e_recusada()
    {
        var gerador = Construtor.Gerador();
        var especialidade = Construtor.Categoria(id: Construtor.Id(20));
        var tecnico = Construtor.Tecnico(especialidade);
        var chamado = Construtor.Chamado(gerador: gerador);
        var categoriaDoChamado = Construtor.Id(21);

        var excecao = Should.Throw<ExcecaoDeDominio>(() => chamado.AtribuirTecnico(
            tecnico,
            categoriaDoChamado,
            "04567000",
            Construtor.Id(9),
            Construtor.Agora,
            gerador));

        excecao.Requisito.ShouldBe("RN0022");
        chamado.TecnicoId.ShouldBeNull();
    }

    [Fact]
    public void Atribuicao_de_tecnico_com_a_especialidade_e_aceita()
    {
        var gerador = Construtor.Gerador();
        var especialidade = Construtor.Categoria(id: Construtor.Id(20));
        var tecnico = Construtor.Tecnico(especialidade);
        var chamado = Construtor.Chamado(gerador: gerador);

        chamado.AtribuirTecnico(
            tecnico,
            especialidade.Id,
            "04567000",
            Construtor.Id(9),
            Construtor.Agora,
            gerador);

        chamado.TecnicoId.ShouldBe(tecnico.Id);
    }
}
