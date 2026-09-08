using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Eventos;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0034: fluxo de status do chamado. Teste parametrizado cobrindo todas as transicoes
/// validas e uma amostra das invalidas.
/// </summary>
[Trait("Requisito", "RN0034")]
public class RN0034MaquinaDeEstadosTests
{
    public static TheoryData<StatusChamado, StatusChamado> TransicoesValidas()
    {
        var dados = new TheoryData<StatusChamado, StatusChamado>();

        foreach (var (origem, destino) in MaquinaDeEstadosDoChamado.TodasAsTransicoesValidas())
        {
            dados.Add(origem, destino);
        }

        return dados;
    }

    public static TheoryData<StatusChamado, StatusChamado> TransicoesInvalidas() => new()
    {
        { StatusChamado.Aberto, StatusChamado.Agendado },
        { StatusChamado.Aberto, StatusChamado.EmAtendimento },
        { StatusChamado.Aberto, StatusChamado.Concluido },
        { StatusChamado.EmAnalise, StatusChamado.EmAtendimento },
        { StatusChamado.EmAnalise, StatusChamado.Concluido },
        { StatusChamado.Agendado, StatusChamado.Concluido },
        { StatusChamado.EmAtendimento, StatusChamado.Agendado },
        { StatusChamado.EmAtendimento, StatusChamado.EmAnalise },
        { StatusChamado.Concluido, StatusChamado.Cancelado },
        { StatusChamado.Concluido, StatusChamado.EmAtendimento },
        { StatusChamado.Cancelado, StatusChamado.Aberto },
        { StatusChamado.Cancelado, StatusChamado.EmAnalise },
        { StatusChamado.Cancelado, StatusChamado.Concluido }
    };

    [Theory]
    [MemberData(nameof(TransicoesValidas))]
    public void Transicao_valida_e_aceita(StatusChamado origem, StatusChamado destino)
    {
        MaquinaDeEstadosDoChamado.PodeIr(origem, destino).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(TransicoesInvalidas))]
    public void Transicao_invalida_e_recusada_pela_tabela(StatusChamado origem, StatusChamado destino)
    {
        MaquinaDeEstadosDoChamado.PodeIr(origem, destino).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(TransicoesInvalidas))]
    public void Chamado_recusa_transicao_invalida_com_excecao_dedicada(
        StatusChamado origem,
        StatusChamado destino)
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.ChamadoNoStatus(origem, gerador);

        var excecao = Should.Throw<TransicaoDeStatusInvalidaException>(() => chamado.AlterarStatus(
            destino,
            Construtor.Id(9),
            null,
            Construtor.Agora,
            gerador));

        excecao.StatusAtual.ShouldBe(origem);
        excecao.StatusPretendido.ShouldBe(destino);
        excecao.Requisito.ShouldBe("RN0034");
        chamado.Status.ShouldBe(origem);
    }

    [Fact]
    public void Fluxo_completo_do_chamado_percorre_a_sequencia_da_regra()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.Chamado(gerador: gerador);
        var usuario = Construtor.Id(9);

        chamado.Status.ShouldBe(StatusChamado.Aberto);

        chamado.AlterarStatus(StatusChamado.EmAnalise, usuario, null, Construtor.Agora, gerador);
        chamado.AlterarStatus(StatusChamado.Agendado, usuario, null, Construtor.Agora, gerador);
        chamado.AlterarStatus(StatusChamado.EmAtendimento, usuario, null, Construtor.Agora, gerador);
        chamado.AlterarStatus(StatusChamado.Concluido, usuario, null, Construtor.Agora, gerador);

        chamado.Status.ShouldBe(StatusChamado.Concluido);
    }

    [Fact]
    public void Cada_transicao_grava_historico_e_publica_evento()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.Chamado(gerador: gerador);

        chamado.AlterarStatus(
            StatusChamado.EmAnalise,
            Construtor.Id(9),
            "Triagem iniciada.",
            Construtor.Agora,
            gerador);

        // Abertura + transicao.
        chamado.HistoricoStatus.Count.ShouldBe(2);
        chamado.Eventos.Count.ShouldBe(2);

        var ultimo = chamado.HistoricoStatus.Last();
        ultimo.StatusAnterior.ShouldBe(StatusChamado.Aberto);
        ultimo.StatusNovo.ShouldBe(StatusChamado.EmAnalise);
        ultimo.Observacao.ShouldBe("Triagem iniciada.");

        chamado.Eventos.OfType<StatusDoChamadoAlterado>().Last().StatusNovo
            .ShouldBe(StatusChamado.EmAnalise);
    }

    [Fact]
    public void Cancelado_e_estado_final()
    {
        MaquinaDeEstadosDoChamado.DestinosValidos(StatusChamado.Cancelado).ShouldBeEmpty();
    }
}
