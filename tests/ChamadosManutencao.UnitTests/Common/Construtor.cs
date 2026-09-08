using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Pessoas;

namespace ChamadosManutencao.UnitTests.Common;

/// <summary>
/// Construtores de objetos de dominio para os testes. Cada metodo entrega um objeto valido,
/// e o teste altera apenas o que a regra em prova exige.
/// </summary>
public static class Construtor
{
    public static readonly DateTimeOffset Agora =
        new(2026, 6, 15, 9, 0, 0, TimeSpan.FromHours(-3));

    public static IGeradorId Gerador() => new GeradorIdFalso();

    public static Guid Id(int semente) => new($"00000000-0000-0000-0001-{semente:000000000000}");

    public static Imovel Imovel(Guid clienteId, string cep = "04567000") =>
        new(
            Id(100),
            clienteId,
            "Casa",
            TipoImovel.Casa,
            "Rua das Palmeiras",
            "120",
            complemento: null,
            "Vila Mariana",
            cep,
            "Sao Paulo",
            "SP");

    public static Cliente Cliente(Guid? id = null, string cep = "04567000")
    {
        var clienteId = id ?? Id(1);

        return new Cliente(
            clienteId,
            "CLI-000001",
            "Maria de Souza",
            "39053344705",
            "maria@exemplo.com",
            "11999990000",
            "hash::Senha@123",
            [Imovel(clienteId, cep)]);
    }

    public static CategoriaServico Categoria(
        bool categoriaDeRisco = false,
        bool exigeFoto = false,
        Guid? id = null) =>
        new(
            id ?? Id(2),
            categoriaDeRisco ? "Eletrica" : "Ar-condicionado",
            "Servicos da categoria",
            exigeFoto,
            categoriaDeRisco);

    public static AreaAtendimento Area(
        Guid tecnicoId,
        string cepInicial = "04000000",
        string cepFinal = "05000000",
        decimal taxa = 45m) =>
        new(Id(3), tecnicoId, "Vila Mariana", cepInicial, cepFinal, taxa);

    public static Tecnico Tecnico(
        CategoriaServico? especialidade = null,
        Guid? id = null,
        string cepInicial = "04000000",
        string cepFinal = "05000000")
    {
        var tecnicoId = id ?? Id(4);

        var tecnico = new Tecnico(
            tecnicoId,
            "TEC-000001",
            "Joao Ferreira",
            "52998224725",
            "joao@exemplo.com",
            "11988880000",
            "hash::Senha@123",
            [especialidade ?? Categoria()]);

        tecnico.DefinirAreasAtendimento([Area(tecnicoId, cepInicial, cepFinal)]);

        return tecnico;
    }

    public static Chamado Chamado(
        Guid? clienteId = null,
        Guid? tipoServicoId = null,
        IGeradorId? gerador = null,
        Urgencia urgencia = Urgencia.Media) =>
        Domain.Chamados.Chamado.Abrir(
            Id(5),
            numero: 1,
            clienteId ?? Id(1),
            imovelId: Id(100),
            tipoServicoId ?? Id(6),
            "O ar-condicionado nao gela.",
            urgencia,
            Agora,
            gerador ?? Gerador());

    /// <summary>Leva o chamado ate o status desejado percorrendo transicoes validas.</summary>
    public static Chamado ChamadoNoStatus(StatusChamado status, IGeradorId gerador)
    {
        var chamado = Chamado(gerador: gerador);
        var usuario = Id(9);

        if (status == StatusChamado.Aberto)
        {
            return chamado;
        }

        chamado.AlterarStatus(StatusChamado.EmAnalise, usuario, null, Agora, gerador);

        if (status == StatusChamado.EmAnalise)
        {
            return chamado;
        }

        if (status == StatusChamado.Cancelado)
        {
            chamado.AlterarStatus(StatusChamado.Cancelado, usuario, null, Agora, gerador);
            return chamado;
        }

        chamado.AlterarStatus(StatusChamado.Agendado, usuario, null, Agora, gerador);

        if (status == StatusChamado.Agendado)
        {
            return chamado;
        }

        chamado.AlterarStatus(StatusChamado.EmAtendimento, usuario, null, Agora, gerador);

        if (status == StatusChamado.EmAtendimento)
        {
            return chamado;
        }

        chamado.AlterarStatus(StatusChamado.Concluido, usuario, null, Agora, gerador);

        return chamado;
    }
}
