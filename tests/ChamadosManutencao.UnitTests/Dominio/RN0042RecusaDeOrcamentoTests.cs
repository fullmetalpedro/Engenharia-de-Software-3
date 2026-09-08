using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0042: orcamento recusado leva o chamado a CANCELADO, salvo pedido de novo orcamento.
/// A decisao de cancelar ou aguardar novo orcamento vive no handler; aqui esta o que o
/// dominio garante: a recusa muda o status do orcamento e libera o registro de um novo.
/// </summary>
[Trait("Requisito", "RN0042")]
public class RN0042RecusaDeOrcamentoTests
{
    private static Atendimento AtendimentoComOrcamento(out Orcamento orcamento)
    {
        var atendimento = new Atendimento(
            Construtor.Id(50),
            Construtor.Id(5),
            Construtor.Id(4),
            Construtor.Agora);

        orcamento = new Orcamento(
            Construtor.Id(51),
            atendimento.Id,
            Construtor.Agora,
            [new ItemOrcamento(Construtor.Id(52), Construtor.Id(51), "Compressor", TipoItem.Peca, 1, 800m)]);

        atendimento.RegistrarOrcamento(orcamento);

        return atendimento;
    }

    [Fact]
    public void Recusa_marca_o_orcamento_como_recusado()
    {
        var atendimento = AtendimentoComOrcamento(out var orcamento);

        orcamento.Recusar(Construtor.Agora.AddHours(1));

        orcamento.Status.ShouldBe(StatusOrcamento.Recusado);
        orcamento.DataHoraDecisao.ShouldBe(Construtor.Agora.AddHours(1));
        atendimento.OrcamentoAprovado().ShouldBeNull();
    }

    [Fact]
    public void Apos_a_recusa_o_tecnico_pode_registrar_um_novo_orcamento()
    {
        var atendimento = AtendimentoComOrcamento(out var orcamento);
        orcamento.Recusar(Construtor.Agora.AddHours(1));

        var novoOrcamento = new Orcamento(
            Construtor.Id(53),
            atendimento.Id,
            Construtor.Agora.AddHours(2),
            [new ItemOrcamento(Construtor.Id(54), Construtor.Id(53), "Compressor recondicionado", TipoItem.Peca, 1, 500m)]);

        atendimento.RegistrarOrcamento(novoOrcamento);

        atendimento.Orcamentos.Count.ShouldBe(2);
        atendimento.OrcamentoPendente().ShouldBe(novoOrcamento);
    }

    [Fact]
    public void Nao_ha_dois_orcamentos_pendentes_ao_mesmo_tempo()
    {
        var atendimento = AtendimentoComOrcamento(out _);

        var segundo = new Orcamento(
            Construtor.Id(55),
            atendimento.Id,
            Construtor.Agora.AddHours(1),
            [new ItemOrcamento(Construtor.Id(56), Construtor.Id(55), "Mao de obra", TipoItem.MaoDeObra, 2, 120m)]);

        Should.Throw<ExcecaoDeDominio>(() => atendimento.RegistrarOrcamento(segundo));
    }

    [Fact]
    public void Orcamento_ja_decidido_nao_pode_ser_decidido_de_novo()
    {
        AtendimentoComOrcamento(out var orcamento);
        orcamento.Recusar(Construtor.Agora.AddHours(1));

        Should.Throw<ExcecaoDeDominio>(() => orcamento.Aprovar(Construtor.Agora.AddHours(2)));
    }
}
