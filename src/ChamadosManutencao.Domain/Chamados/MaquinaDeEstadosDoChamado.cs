using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Chamados;

/// <summary>
/// Tabela de transicoes validas do chamado (RN0034).
///
/// As transicoes EM_ATENDIMENTO -> CANCELADO e CONCLUIDO -> EM_ANALISE nao constam do texto
/// literal da RN0034, mas sao exigidas por outras regras aprovadas:
///   - RN0042 e RN0043 cancelam o chamado durante o atendimento (recusa e expiracao de orcamento);
///   - RN0035 devolve o chamado concluido para EM ANALISE na reabertura.
/// A ampliacao esta registrada em docs/DECISOES.md (decisao D05).
/// </summary>
public static class MaquinaDeEstadosDoChamado
{
    private static readonly Dictionary<StatusChamado, StatusChamado[]> Transicoes = new()
    {
        [StatusChamado.Aberto] =
        [
            StatusChamado.EmAnalise,
            StatusChamado.Cancelado
        ],
        [StatusChamado.EmAnalise] =
        [
            StatusChamado.Agendado,
            StatusChamado.Cancelado
        ],
        [StatusChamado.Agendado] =
        [
            StatusChamado.EmAtendimento,
            StatusChamado.Agendado,
            StatusChamado.EmAnalise,
            StatusChamado.Cancelado
        ],
        [StatusChamado.EmAtendimento] =
        [
            StatusChamado.Concluido,
            StatusChamado.Cancelado
        ],
        [StatusChamado.Concluido] =
        [
            StatusChamado.EmAnalise
        ],
        [StatusChamado.Cancelado] = []
    };

    public static bool PodeIr(StatusChamado atual, StatusChamado novo) =>
        Transicoes.TryGetValue(atual, out var destinos) && destinos.Contains(novo);

    public static IReadOnlyCollection<StatusChamado> DestinosValidos(StatusChamado atual) =>
        Transicoes.TryGetValue(atual, out var destinos) ? destinos : [];

    /// <summary>Todos os pares (origem, destino) validos, usado no teste parametrizado da RN0034.</summary>
    public static IEnumerable<(StatusChamado Origem, StatusChamado Destino)> TodasAsTransicoesValidas() =>
        Transicoes.SelectMany(par => par.Value.Select(destino => (par.Key, destino)));
}
