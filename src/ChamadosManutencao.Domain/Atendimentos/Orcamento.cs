using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Atendimentos;

/// <summary>
/// Orcamento de pecas e mao de obra registrado pelo tecnico durante o atendimento.
/// Requisitos: RF0055, RF0056, RN0042, RN0043, RN0071.
/// </summary>
public sealed class Orcamento : RaizDeAgregado
{
    /// <summary>RN0043: prazo do cliente para aprovar ou recusar.</summary>
    public const int PrazoDeAprovacaoEmHoras = 48;

    private readonly List<ItemOrcamento> _itens = [];

    public Orcamento(
        Guid id,
        Guid atendimentoId,
        DateTimeOffset dataHoraRegistro,
        IEnumerable<ItemOrcamento> itens)
        : base(id)
    {
        AtendimentoId = atendimentoId;
        DataHoraRegistro = dataHoraRegistro;
        PrazoAprovacao = dataHoraRegistro.AddHours(PrazoDeAprovacaoEmHoras);
        Status = StatusOrcamento.Pendente;

        var lista = itens?.ToList() ?? [];

        if (lista.Count == 0)
        {
            throw new ExcecaoDeDominio(
                "O orcamento deve conter ao menos um item de peca ou de mao de obra.",
                "RF0055");
        }

        _itens.AddRange(lista);
    }

    private Orcamento()
    {
    }

    public Guid AtendimentoId { get; private set; }

    public DateTimeOffset DataHoraRegistro { get; private set; }

    /// <summary>RN0043: registro + 48 horas.</summary>
    public DateTimeOffset PrazoAprovacao { get; private set; }

    public StatusOrcamento Status { get; private set; }

    public DateTimeOffset? DataHoraDecisao { get; private set; }

    public IReadOnlyCollection<ItemOrcamento> Itens => _itens.AsReadOnly();

    public decimal CalcularValorPecas() =>
        _itens.Where(i => i.Tipo == TipoItem.Peca).Sum(i => i.CalcularSubtotal());

    public decimal CalcularValorMaoObra() =>
        _itens.Where(i => i.Tipo == TipoItem.MaoDeObra).Sum(i => i.CalcularSubtotal());

    public decimal ValorTotal() => CalcularValorPecas() + CalcularValorMaoObra();

    /// <summary>RF0056.</summary>
    public void Aprovar(DateTimeOffset agora)
    {
        GarantirQueEstaPendente(agora);
        Status = StatusOrcamento.Aprovado;
        DataHoraDecisao = agora;
    }

    /// <summary>RF0056 e RN0042.</summary>
    public void Recusar(DateTimeOffset agora)
    {
        GarantirQueEstaPendente(agora);
        Status = StatusOrcamento.Recusado;
        DataHoraDecisao = agora;
    }

    /// <summary>RN0043: expiracao aplicada pelo ExpiracaoOrcamentoJob.</summary>
    public void Expirar(DateTimeOffset agora)
    {
        if (Status != StatusOrcamento.Pendente)
        {
            throw new ExcecaoDeDominio(
                $"Somente orcamento pendente pode expirar. Status atual: {Status}.",
                "RN0043");
        }

        if (agora <= PrazoAprovacao)
        {
            throw new ExcecaoDeDominio(
                "O prazo de aprovacao do orcamento ainda nao venceu.",
                "RN0043");
        }

        Status = StatusOrcamento.Expirado;
        DataHoraDecisao = agora;
    }

    public bool EstaVencido(DateTimeOffset agora) =>
        Status == StatusOrcamento.Pendente && agora > PrazoAprovacao;

    private void GarantirQueEstaPendente(DateTimeOffset agora)
    {
        if (Status != StatusOrcamento.Pendente)
        {
            throw new ExcecaoDeDominio(
                $"O orcamento ja foi decidido. Status atual: {Status}.",
                "RF0056");
        }

        if (agora > PrazoAprovacao)
        {
            throw new ExcecaoDeDominio(
                "O prazo de 48 horas para aprovar ou recusar o orcamento ja expirou.",
                "RN0043");
        }
    }
}
