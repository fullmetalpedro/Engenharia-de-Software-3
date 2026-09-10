using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0031: categoria de risco com indicacao de risco recebe urgencia ALTA automaticamente.
/// </summary>
[Trait("Requisito", "RN0031")]
public class RN0031PoliticaUrgenciaTests
{
    [Fact]
    public void Categoria_de_risco_com_indicacao_recebe_urgencia_alta()
    {
        var categoria = Construtor.Categoria(categoriaDeRisco: true);

        var urgencia = PoliticaUrgencia.Definir(categoria, indicacaoDeRisco: true);

        urgencia.ShouldBe(Urgencia.Alta);
    }

    [Fact]
    public void Categoria_de_risco_sem_indicacao_nasce_com_a_urgencia_padrao()
    {
        var categoria = Construtor.Categoria(categoriaDeRisco: true);

        var urgencia = PoliticaUrgencia.Definir(categoria, indicacaoDeRisco: false);

        urgencia.ShouldBe(PoliticaUrgencia.Padrao);
    }

    [Fact]
    public void Categoria_comum_com_indicacao_de_risco_nasce_com_a_urgencia_padrao()
    {
        var categoria = Construtor.Categoria(categoriaDeRisco: false);

        var urgencia = PoliticaUrgencia.Definir(categoria, indicacaoDeRisco: true);

        urgencia.ShouldBe(PoliticaUrgencia.Padrao);
    }

    [Fact]
    public void Administrador_pode_reclassificar_a_urgencia_definida_automaticamente()
    {
        // Decisao D20: a classificacao automatica e o padrao inicial, nao uma trava.
        var gerador = Construtor.Gerador();
        var chamado = Construtor.Chamado(gerador: gerador, urgencia: Urgencia.Alta);

        chamado.ClassificarUrgencia(Urgencia.Media);

        chamado.Urgencia.ShouldBe(Urgencia.Media);
    }
}
