using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Eventos;
using ChamadosManutencao.Domain.Financeiro;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// Nucleo do dominio: identidade das entidades, acumulo e limpeza de eventos (base da RNF0041
/// e da RN0051) e as guardas de invariante reutilizadas por todas as entidades.
/// </summary>
[Trait("Requisito", "RNF0041")]
public class NucleoDoDominioTests
{
    [Fact]
    public void Entidade_com_identificador_vazio_e_recusada()
    {
        Should.Throw<ExcecaoDeDominio>(() => new Domain.Pessoas.Imovel(
            Guid.Empty,
            Construtor.Id(1),
            "Casa",
            TipoImovel.Casa,
            "Rua das Palmeiras",
            "120",
            null,
            "Vila Mariana",
            "04567000",
            "Sao Paulo",
            "SP"));
    }

    [Fact]
    public void Identidade_e_o_criterio_de_igualdade()
    {
        var primeiro = Construtor.Imovel(Construtor.Id(1));
        var segundo = Construtor.Imovel(Construtor.Id(1));

        primeiro.Equals(segundo).ShouldBeTrue();
        primeiro.GetHashCode().ShouldBe(segundo.GetHashCode());
        primeiro.Equals("outro tipo").ShouldBeFalse();
        primeiro.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Eventos_sao_acumulados_na_raiz_e_podem_ser_limpos()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.Chamado(gerador: gerador);

        chamado.Eventos.ShouldHaveSingleItem().ShouldBeOfType<StatusDoChamadoAlterado>();

        chamado.LimparEventos();

        chamado.Eventos.ShouldBeEmpty();
    }

    [Fact]
    public void Evento_de_status_carrega_o_momento_e_o_destinatario()
    {
        var gerador = Construtor.Gerador();
        var chamado = Construtor.Chamado(gerador: gerador);

        var evento = chamado.Eventos.OfType<StatusDoChamadoAlterado>().Single();

        evento.ChamadoId.ShouldBe(chamado.Id);
        evento.ClienteId.ShouldBe(chamado.ClienteId);
        evento.NumeroDoChamado.ShouldBe(chamado.Numero);
        evento.StatusNovo.ShouldBe(StatusChamado.Aberto);
        evento.OcorridoEm.ShouldBe(Construtor.Agora);
    }

    [Fact]
    public void Evento_de_fatura_vencida_carrega_numero_e_cliente()
    {
        var fatura = new Fatura(
            Construtor.Id(800),
            10,
            Construtor.Id(50),
            Construtor.Id(1),
            Construtor.Id(5),
            Construtor.Agora,
            100m,
            0m,
            0m);

        fatura.RegistrarVencimento(Construtor.Agora.AddDays(16)).ShouldBeTrue();

        var evento = fatura.Eventos.OfType<FaturaVencida>().ShouldHaveSingleItem();
        evento.NumeroDaFatura.ShouldBe(10);
        evento.ClienteId.ShouldBe(Construtor.Id(1));
    }

    [Fact]
    public void Fatura_paga_ou_cancelada_nao_vence_de_novo()
    {
        var fatura = new Fatura(
            Construtor.Id(801),
            11,
            Construtor.Id(50),
            Construtor.Id(1),
            Construtor.Id(5),
            Construtor.Agora,
            100m,
            0m,
            0m);

        fatura.Cancelar();

        fatura.Status.ShouldBe(StatusFatura.Cancelada);
        fatura.RegistrarVencimento(Construtor.Agora.AddDays(30)).ShouldBeFalse();

        Should.Throw<ExcecaoDeDominio>(() => fatura.RegistrarPagamento(new Pagamento(
            Construtor.Id(802),
            fatura.Id,
            Construtor.Id(99),
            Construtor.Agora,
            fatura.ValorTotal,
            "TRX-9999",
            StatusPagamento.Aprovado)));
    }

    [Fact]
    public void Fatura_nao_vence_antes_do_prazo()
    {
        var fatura = new Fatura(
            Construtor.Id(803),
            12,
            Construtor.Id(50),
            Construtor.Id(1),
            Construtor.Id(5),
            Construtor.Agora,
            100m,
            0m,
            0m);

        fatura.RegistrarVencimento(Construtor.Agora.AddDays(1)).ShouldBeFalse();
        fatura.Status.ShouldBe(StatusFatura.Emitida);
    }

    [Fact]
    public void Pagamento_de_outra_fatura_e_recusado()
    {
        var fatura = new Fatura(
            Construtor.Id(804),
            13,
            Construtor.Id(50),
            Construtor.Id(1),
            Construtor.Id(5),
            Construtor.Agora,
            100m,
            0m,
            0m);

        Should.Throw<ExcecaoDeDominio>(() => fatura.RegistrarPagamento(new Pagamento(
            Construtor.Id(805),
            Construtor.Id(999),
            Construtor.Id(99),
            Construtor.Agora,
            fatura.ValorTotal,
            "TRX-8888",
            StatusPagamento.Aprovado)));
    }

    [Fact]
    public void Pagamento_pode_ser_estornado()
    {
        var pagamento = new Pagamento(
            Construtor.Id(806),
            Construtor.Id(804),
            Construtor.Id(99),
            Construtor.Agora,
            100m,
            "TRX-7777",
            StatusPagamento.Aprovado);

        pagamento.Estornar();

        pagamento.Status.ShouldBe(StatusPagamento.Estornado);
    }

    [Fact]
    public void Item_de_orcamento_recusa_quantidade_e_valor_invalidos()
    {
        Should.Throw<ExcecaoDeDominio>(() => new ItemOrcamento(
            Construtor.Id(807), Construtor.Id(60), "Peca", TipoItem.Peca, 0, 10m));

        Should.Throw<ExcecaoDeDominio>(() => new ItemOrcamento(
            Construtor.Id(808), Construtor.Id(60), "Peca", TipoItem.Peca, 1, -1m));

        new ItemOrcamento(Construtor.Id(809), Construtor.Id(60), "Peca", TipoItem.Peca, 3, 25m)
            .CalcularSubtotal()
            .ShouldBe(75m);
    }

    [Fact]
    public void Orcamento_sem_itens_e_recusado()
    {
        var excecao = Should.Throw<ExcecaoDeDominio>(() => new Orcamento(
            Construtor.Id(810),
            Construtor.Id(50),
            Construtor.Agora,
            []));

        excecao.Requisito.ShouldBe("RF0055");
    }

    [Fact]
    public void Atendimento_nao_recebe_orcamento_de_outro_atendimento()
    {
        var atendimento = new Atendimento(
            Construtor.Id(811), Construtor.Id(5), Construtor.Id(4), Construtor.Agora);

        var orcamento = new Orcamento(
            Construtor.Id(812),
            Construtor.Id(999),
            Construtor.Agora,
            [new ItemOrcamento(Construtor.Id(813), Construtor.Id(812), "Peca", TipoItem.Peca, 1, 10m)]);

        Should.Throw<ExcecaoDeDominio>(() => atendimento.RegistrarOrcamento(orcamento));
    }

    [Fact]
    public void Atendimento_concluido_nao_recebe_novo_orcamento_nem_nova_conclusao()
    {
        var atendimento = new Atendimento(
            Construtor.Id(814), Construtor.Id(5), Construtor.Id(4), Construtor.Agora);

        atendimento.Concluir(
            "Servico executado.",
            Construtor.Id(1),
            chamadoDeGarantia: false,
            Construtor.Agora.AddHours(2),
            Construtor.Gerador());

        Should.Throw<ExcecaoDeDominio>(() => atendimento.Concluir(
            "Outra conclusao.",
            Construtor.Id(1),
            chamadoDeGarantia: false,
            Construtor.Agora.AddHours(3),
            Construtor.Gerador()));

        var orcamento = new Orcamento(
            Construtor.Id(815),
            atendimento.Id,
            Construtor.Agora,
            [new ItemOrcamento(Construtor.Id(816), Construtor.Id(815), "Peca", TipoItem.Peca, 1, 10m)]);

        Should.Throw<ExcecaoDeDominio>(() => atendimento.RegistrarOrcamento(orcamento));
    }

    [Fact]
    public void Conclusao_anterior_ao_inicio_e_recusada()
    {
        var atendimento = new Atendimento(
            Construtor.Id(817), Construtor.Id(5), Construtor.Id(4), Construtor.Agora);

        var excecao = Should.Throw<ExcecaoDeDominio>(() => atendimento.Concluir(
            "Servico executado.",
            Construtor.Id(1),
            chamadoDeGarantia: false,
            Construtor.Agora.AddHours(-1),
            Construtor.Gerador()));

        excecao.Requisito.ShouldBe("RF0057");
    }

    [Fact]
    public void Garantia_sem_motivo_de_recusa_devolve_nulo()
    {
        var garantia = new Garantia(Construtor.Id(818), Construtor.Id(50), Construtor.Agora);

        garantia
            .MotivoDeRecusa(Construtor.Agora.AddDays(5), true, Construtor.Id(6), Construtor.Id(6))
            .ShouldBeNull();
    }

    [Fact]
    public void Excecao_de_dominio_sem_requisito_mantem_a_mensagem()
    {
        var excecao = new ExcecaoDeDominio("Falha generica.");

        excecao.Message.ShouldBe("Falha generica.");
        excecao.Requisito.ShouldBeNull();
    }

    [Theory]
    [InlineData("  texto  ", "texto")]
    public void Guarda_de_texto_remove_espacos_das_pontas(string entrada, string esperado)
    {
        Garantir.TextoPreenchido(entrada, "campo").ShouldBe(esperado);
    }

    [Fact]
    public void Guardas_recusam_valores_invalidos()
    {
        Should.Throw<ExcecaoDeDominio>(() => Garantir.TextoPreenchido(null, "campo"));
        Should.Throw<ExcecaoDeDominio>(() => Garantir.TextoComTamanhoMaximo("abcdef", 3, "campo"));
        Should.Throw<ExcecaoDeDominio>(() => Garantir.Cpf("123"));
        Should.Throw<ExcecaoDeDominio>(() => Garantir.Email("sem-arroba"));
        Should.Throw<ExcecaoDeDominio>(() => Garantir.Email("a@b"));
        Should.Throw<ExcecaoDeDominio>(() => Garantir.Cep("123"));
        Should.Throw<ExcecaoDeDominio>(() => Garantir.ValorNaoNegativo(-1m, "campo"));
        Should.Throw<ExcecaoDeDominio>(() => Garantir.NaoNulo<string>(null, "campo"));
    }

    [Fact]
    public void Guardas_aceitam_valores_validos()
    {
        Garantir.Cpf("390.533.447-05").ShouldBe("39053344705");
        Garantir.Email("MARIA@Exemplo.com").ShouldBe("maria@exemplo.com");
        Garantir.Cep("04567-000").ShouldBe("04567000");
        Garantir.ValorNaoNegativo(0m, "campo").ShouldBe(0m);
        Garantir.TextoComTamanhoMaximo("abc", 3, "campo").ShouldBe("abc");
        Should.NotThrow(() => Garantir.NaoNulo("valor", "campo"));
    }
}
