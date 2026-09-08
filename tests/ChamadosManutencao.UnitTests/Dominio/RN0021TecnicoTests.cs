using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Pessoas;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0021: nome completo, CPF, telefone, e-mail e ao menos uma especialidade sao obrigatorios
/// no cadastro do tecnico.
/// </summary>
[Trait("Requisito", "RN0021")]
public class RN0021TecnicoTests
{
    [Fact]
    public void Tecnico_com_especialidade_e_criado()
    {
        var tecnico = Construtor.Tecnico();

        tecnico.Especialidades.Count.ShouldBe(1);
        tecnico.CodigoTecnico.ShouldBe("TEC-000001");
        tecnico.Ativo.ShouldBeTrue();
    }

    [Fact]
    public void Tecnico_sem_especialidade_e_recusado()
    {
        var excecao = Should.Throw<ExcecaoDeDominio>(() => new Tecnico(
            Construtor.Id(4),
            "TEC-000002",
            "Joao Ferreira",
            "52998224725",
            "joao@exemplo.com",
            "11988880000",
            "hash::Senha@123",
            []));

        excecao.Requisito.ShouldBe("RN0021");
    }

    [Fact]
    public void Remover_todas_as_especialidades_e_bloqueado()
    {
        var tecnico = Construtor.Tecnico();

        var excecao = Should.Throw<ExcecaoDeDominio>(() => tecnico.DefinirEspecialidades([]));

        excecao.Requisito.ShouldBe("RN0021");
        tecnico.Especialidades.Count.ShouldBe(1);
    }
}
