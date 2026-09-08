using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Eventos;

/// <summary>
/// Toda transicao de status publica este evento, consumido pelo notificador (RNF0041).
/// </summary>
public sealed record StatusDoChamadoAlterado(
    Guid ChamadoId,
    long NumeroDoChamado,
    Guid ClienteId,
    StatusChamado? StatusAnterior,
    StatusChamado StatusNovo,
    DateTimeOffset OcorridoEm) : IEventoDeDominio;

/// <summary>
/// Conclusao do atendimento (RF0057). Dispara a solicitacao de avaliacao (RN0051),
/// a criacao da garantia e a geracao da fatura (RN0071).
/// </summary>
public sealed record AtendimentoConcluido(
    Guid AtendimentoId,
    Guid ChamadoId,
    Guid ClienteId,
    bool ChamadoDeGarantia,
    DateTimeOffset OcorridoEm) : IEventoDeDominio;

/// <summary>Fatura que passou da data de vencimento sem quitacao (job de vencimento).</summary>
public sealed record FaturaVencida(
    Guid FaturaId,
    long NumeroDaFatura,
    Guid ClienteId,
    DateTimeOffset OcorridoEm) : IEventoDeDominio;

/// <summary>Orcamento que expirou sem manifestacao do cliente (RN0043).</summary>
public sealed record OrcamentoExpirado(
    Guid OrcamentoId,
    Guid AtendimentoId,
    DateTimeOffset OcorridoEm) : IEventoDeDominio;
