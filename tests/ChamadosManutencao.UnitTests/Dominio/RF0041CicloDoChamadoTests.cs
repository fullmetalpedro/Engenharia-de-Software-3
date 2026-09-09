using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RF0041, RF0046, RF0047, RF0051 e RF0054: comportamento do agregado Chamado ao longo do
/// ciclo, alem das regras ja cobertas pelas classes de RN.
/// </summary>
[Trait("Requisito", "RF0041")]
public class RF0041CicloDoChamadoTests
{
    [Fact]
    public void Chamado_aberto_ja_nasce_com_historico_e_evento()
    {
        var gerador = Construtor.Gerador();

        var chamado = Construtor.Chamado(gerador: gerador);

        chamado.Status.ShouldBe(StatusChamado.Aberto);
        chamado.Numero.ShouldBe(1);
        chamado.ChamadoDeGarantia.ShouldBeFalse();
        chamado.ChamadoOriginalId.ShouldBeNull();
        chamado.HistoricoStatus.ShouldHaveSingleItem().StatusAnterior.ShouldBeNull();
        chamado.Eventos.Count.ShouldBe(1);
    }

    [Fact]
    public void Descricao_do_problema_e_obrigatoria()
    {
        var excecao = Should.Throw<ExcecaoDeDominio>(() => Chamado.Abrir(
            Construtor.Id(5),
            numero: 2,
            Construtor.Id(1),
            Construtor.Id(100),
            Construtor.Id(6),
            "   ",
            Urgencia.Media,
            Construtor.Agora,
            Construtor.Gerador()));

        excecao.Requisito.ShouldBe("RF0041");
    }

    [Fact]
    public void Atribuicao_de_chamado_concluido_e_recusada()
    {
        var gerador = Construtor.Gerador();
        var categoria = Construtor.Categoria(id: Construtor.Id(20));
        var tecnico = Construtor.Tecnico(categoria);
        var chamado = Construtor.ChamadoNoStatus(StatusChamado.Concluido, gerador);

        var excecao = Should.Throw<ExcecaoDeDominio>(() => chamado.AtribuirTecnico(
            tecnico,
            categoria.Id,
            "04567000",
            Construtor.Id(9),
            Construtor.Agora,
            gerador));

        excecao.Requisito.ShouldBe("RF0047");
    }

    [Fact]
    public void Atribuicao_a_tecnico_inativo_e_recusada()
    {
        var gerador = Construtor.Gerador();
        var categoria = Construtor.Categoria(id: Construtor.Id(20));
        var tecnico = Construtor.Tecnico(categoria);
        tecnico.Inativar();

        var chamado = Construtor.Chamado(gerador: gerador);

        var excecao = Should.Throw<ExcecaoDeDominio>(() => chamado.AtribuirTecnico(
            tecnico,
            categoria.Id,
            "04567000",
            Construtor.Id(9),
            Construtor.Agora,
            gerador));

        excecao.Requisito.ShouldBe("RF0023");
    }

    [Fact]
    public void Reatribuicao_em_chamado_ja_em_analise_registra_historico_sem_mudar_status()
    {
        var gerador = Construtor.Gerador();
        var categoria = Construtor.Categoria(id: Construtor.Id(20));
        var primeiro = Construtor.Tecnico(categoria, id: Construtor.Id(4));
        var segundo = Construtor.Tecnico(categoria, id: Construtor.Id(41));
        var chamado = Construtor.Chamado(gerador: gerador);

        chamado.AtribuirTecnico(primeiro, categoria.Id, "04567000", Construtor.Id(9), Construtor.Agora, gerador);
        var historicoAposPrimeira = chamado.HistoricoStatus.Count;

        chamado.AtribuirTecnico(segundo, categoria.Id, "04567000", Construtor.Id(9), Construtor.Agora, gerador);

        chamado.TecnicoId.ShouldBe(segundo.Id);
        chamado.Status.ShouldBe(StatusChamado.EmAnalise);
        chamado.HistoricoStatus.Count.ShouldBe(historicoAposPrimeira + 1);
        chamado.HistoricoStatus.Last().Observacao.ShouldNotBeNull().ShouldContain("reatribuido");
    }

    [Fact]
    public void Chamado_de_garantia_pode_nascer_sem_tecnico_para_triagem_manual()
    {
        var gerador = Construtor.Gerador();
        var categoria = Construtor.Categoria(id: Construtor.Id(20));
        var tecnico = Construtor.Tecnico(categoria);
        var chamado = Construtor.Chamado(gerador: gerador);

        chamado.AtribuirTecnico(tecnico, categoria.Id, "04567000", Construtor.Id(9), Construtor.Agora, gerador);
        chamado.DeixarSemTecnico();

        chamado.TecnicoId.ShouldBeNull();
        chamado.EstaAtribuidoAo(tecnico.Id).ShouldBeFalse();
    }

    [Fact]
    public void Classificar_urgencia_de_chamado_encerrado_e_recusado()
    {
        var gerador = Construtor.Gerador();
        var cancelado = Construtor.ChamadoNoStatus(StatusChamado.Cancelado, gerador);

        var excecao = Should.Throw<ExcecaoDeDominio>(
            () => cancelado.ClassificarUrgencia(Urgencia.Alta));

        excecao.Requisito.ShouldBe("RF0046");
    }

    [Fact]
    public void Agendamento_exige_tecnico_atribuido()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.ChamadoNoStatus(StatusChamado.EmAnalise, gerador);

        var agendamento = new Agendamento(
            Construtor.Id(600),
            chamado.Id,
            Construtor.Agora.AddDays(1),
            120,
            OrigemProposta.Tecnico);

        var excecao = Should.Throw<ExcecaoDeDominio>(() => chamado.AdicionarAgendamento(
            agendamento,
            Construtor.Id(9),
            Construtor.Agora,
            gerador));

        excecao.Requisito.ShouldBe("RF0051");
    }

    [Fact]
    public void Novo_agendamento_marca_o_anterior_como_reagendado()
    {
        var gerador = Construtor.Gerador();
        var categoria = Construtor.Categoria(id: Construtor.Id(20));
        var tecnico = Construtor.Tecnico(categoria);
        var chamado = Construtor.Chamado(gerador: gerador);

        chamado.AtribuirTecnico(tecnico, categoria.Id, "04567000", Construtor.Id(9), Construtor.Agora, gerador);

        var primeiro = new Agendamento(
            Construtor.Id(601), chamado.Id, Construtor.Agora.AddDays(1), 120, OrigemProposta.Tecnico);
        chamado.AdicionarAgendamento(primeiro, tecnico.Id, Construtor.Agora, gerador);

        var segundo = new Agendamento(
            Construtor.Id(602), chamado.Id, Construtor.Agora.AddDays(2), 120, OrigemProposta.Cliente);
        chamado.AdicionarAgendamento(segundo, chamado.ClienteId, Construtor.Agora, gerador);

        primeiro.Status.ShouldBe(StatusAgendamento.Reagendado);
        chamado.Status.ShouldBe(StatusChamado.Agendado);
        chamado.AgendamentoVigente().ShouldBe(segundo);
    }

    [Fact]
    public void Agendamento_em_status_incompativel_e_recusado()
    {
        var gerador = Construtor.Gerador();
        var categoria = Construtor.Categoria(id: Construtor.Id(20));
        var tecnico = Construtor.Tecnico(categoria);
        var chamado = Construtor.ChamadoNoStatus(StatusChamado.EmAtendimento, gerador);

        chamado.AtribuirTecnico(tecnico, categoria.Id, "04567000", Construtor.Id(9), Construtor.Agora, gerador);

        var agendamento = new Agendamento(
            Construtor.Id(603), chamado.Id, Construtor.Agora.AddDays(1), 120, OrigemProposta.Tecnico);

        var excecao = Should.Throw<ExcecaoDeDominio>(() => chamado.AdicionarAgendamento(
            agendamento,
            tecnico.Id,
            Construtor.Agora,
            gerador));

        excecao.Requisito.ShouldBe("RN0034");
    }

    [Fact]
    public void Agendamento_de_outro_chamado_e_recusado()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.Chamado(gerador: gerador);

        var agendamento = new Agendamento(
            Construtor.Id(604), Construtor.Id(777), Construtor.Agora.AddDays(1), 120, OrigemProposta.Tecnico);

        Should.Throw<ExcecaoDeDominio>(() => chamado.AdicionarAgendamento(
            agendamento,
            Construtor.Id(9),
            Construtor.Agora,
            gerador));
    }

    [Fact]
    public void Somente_o_tecnico_atribuido_inicia_o_atendimento()
    {
        var gerador = Construtor.Gerador();
        var categoria = Construtor.Categoria(id: Construtor.Id(20));
        var tecnico = Construtor.Tecnico(categoria);
        var chamado = Construtor.ChamadoNoStatus(StatusChamado.Agendado, gerador);

        chamado.AtribuirTecnico(tecnico, categoria.Id, "04567000", Construtor.Id(9), Construtor.Agora, gerador);

        var excecao = Should.Throw<ExcecaoDeDominio>(() => chamado.RegistrarInicioDeAtendimento(
            Construtor.Id(999),
            Construtor.Agora,
            gerador));

        excecao.Requisito.ShouldBe("RF0054");

        chamado.RegistrarInicioDeAtendimento(tecnico.Id, Construtor.Agora, gerador);
        chamado.Status.ShouldBe(StatusChamado.EmAtendimento);
    }

    [Fact]
    public void Vinculo_de_garantia_e_propriedade_do_chamado()
    {
        var chamado = Construtor.Chamado();

        chamado.VincularGarantia(Construtor.Id(700));

        chamado.GarantiaId.ShouldBe(Construtor.Id(700));
        chamado.PertenceAoCliente(chamado.ClienteId).ShouldBeTrue();
        chamado.PertenceAoCliente(Construtor.Id(888)).ShouldBeFalse();
    }

    [Fact]
    public void Agendamento_confirmado_nao_pode_ser_confirmado_de_novo()
    {
        var agendamento = new Agendamento(
            Construtor.Id(605), Construtor.Id(5), Construtor.Agora.AddDays(1), 120, OrigemProposta.Tecnico);

        agendamento.Confirmar(Construtor.Agora);

        var excecao = Should.Throw<ExcecaoDeDominio>(() => agendamento.Confirmar(Construtor.Agora));

        excecao.Requisito.ShouldBe("RF0052");
    }

    [Fact]
    public void Agendamento_cancelado_nao_pode_ser_reagendado()
    {
        var agendamento = new Agendamento(
            Construtor.Id(606), Construtor.Id(5), Construtor.Agora.AddDays(1), 120, OrigemProposta.Tecnico);

        agendamento.Cancelar();

        var excecao = Should.Throw<ExcecaoDeDominio>(agendamento.MarcarComoReagendado);

        excecao.Requisito.ShouldBe("RF0053");
    }

    [Fact]
    public void Agendamento_reagendado_ainda_pode_ser_confirmado()
    {
        var agendamento = new Agendamento(
            Construtor.Id(607), Construtor.Id(5), Construtor.Agora.AddDays(1), 120, OrigemProposta.Cliente);

        agendamento.MarcarComoReagendado();
        agendamento.Confirmar(Construtor.Agora);

        agendamento.Status.ShouldBe(StatusAgendamento.Confirmado);
    }

    [Fact]
    public void Duracao_nao_positiva_no_agendamento_e_recusada()
    {
        var excecao = Should.Throw<ExcecaoDeDominio>(() => new Agendamento(
            Construtor.Id(608),
            Construtor.Id(5),
            Construtor.Agora,
            0,
            OrigemProposta.Tecnico));

        excecao.Requisito.ShouldBe("RN0041");
    }
}
