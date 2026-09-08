using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Pessoas;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0041: nao ha agendamento em horario ja ocupado do tecnico. A janela de atendimento tem
/// duracao configuravel (decisao D08, padrao de 120 minutos).
/// </summary>
[Trait("Requisito", "RN0041")]
public class RN0041DisponibilidadeTests
{
    private static readonly DateTimeOffset Inicio = Construtor.Agora;

    [Fact]
    public void Tecnico_sem_compromisso_esta_disponivel()
    {
        var tecnico = Construtor.Tecnico();

        tecnico.EstaDisponivel(Inicio, Inicio.AddHours(2), []).ShouldBeTrue();
    }

    [Theory]
    // Sobreposicao total.
    [InlineData(0, 2, false)]
    // Comeca dentro do compromisso existente.
    [InlineData(1, 3, false)]
    // Termina dentro do compromisso existente.
    [InlineData(-1, 1, false)]
    // Encosta no fim do compromisso, sem sobrepor.
    [InlineData(2, 4, true)]
    // Termina exatamente quando o compromisso comeca.
    [InlineData(-2, 0, true)]
    public void Sobreposicao_de_janela_bloqueia_o_agendamento(
        int deslocamentoEmHoras,
        int fimEmHoras,
        bool esperado)
    {
        var tecnico = Construtor.Tecnico();
        var compromissos = new[]
        {
            new JanelaDeAtendimento(Inicio, Inicio.AddHours(2))
        };

        var disponivel = tecnico.EstaDisponivel(
            Inicio.AddHours(deslocamentoEmHoras),
            Inicio.AddHours(fimEmHoras),
            compromissos);

        disponivel.ShouldBe(esperado);
    }

    [Fact]
    public void Janela_com_fim_anterior_ao_inicio_e_recusada()
    {
        var tecnico = Construtor.Tecnico();

        var excecao = Should.Throw<ExcecaoDeDominio>(
            () => tecnico.EstaDisponivel(Inicio.AddHours(2), Inicio, []));

        excecao.Requisito.ShouldBe("RN0041");
    }

    [Fact]
    public void Agendamento_cancelado_deixa_de_ocupar_a_agenda()
    {
        var agendamento = new Agendamento(
            Construtor.Id(40),
            Construtor.Id(5),
            Inicio,
            120,
            OrigemProposta.Tecnico);

        agendamento.OcupaAgenda().ShouldBeTrue();

        agendamento.Cancelar();

        agendamento.OcupaAgenda().ShouldBeFalse();
    }

    [Fact]
    public void Agendamento_confirmado_continua_ocupando_a_agenda()
    {
        var agendamento = new Agendamento(
            Construtor.Id(41),
            Construtor.Id(5),
            Inicio,
            120,
            OrigemProposta.Tecnico);

        agendamento.Confirmar(Inicio.AddMinutes(-30));

        agendamento.Status.ShouldBe(StatusAgendamento.Confirmado);
        agendamento.OcupaAgenda().ShouldBeTrue();
        agendamento.Janela().Fim.ShouldBe(Inicio.AddHours(2));
    }
}
