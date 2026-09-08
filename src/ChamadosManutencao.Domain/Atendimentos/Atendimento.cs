using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Eventos;

namespace ChamadosManutencao.Domain.Atendimentos;

/// <summary>
/// Execucao do servico no imovel do cliente. Raiz do agregado que guarda os orcamentos e a
/// garantia gerada na conclusao.
/// Requisitos: RF0054, RF0055, RF0056, RF0057, RN0042, RN0043, RN0051, RN0071, RN0072.
/// </summary>
public sealed class Atendimento : RaizDeAgregado
{
    private readonly List<Orcamento> _orcamentos = [];

    public Atendimento(
        Guid id,
        Guid chamadoId,
        Guid tecnicoId,
        DateTimeOffset dataHoraInicio)
        : base(id)
    {
        ChamadoId = chamadoId;
        TecnicoId = tecnicoId;
        DataHoraInicio = dataHoraInicio;
    }

    private Atendimento()
    {
    }

    public Guid ChamadoId { get; private set; }

    public Guid TecnicoId { get; private set; }

    public DateTimeOffset DataHoraInicio { get; private set; }

    public DateTimeOffset? DataHoraConclusao { get; private set; }

    public string? RelatoTecnico { get; private set; }

    public IReadOnlyCollection<Orcamento> Orcamentos => _orcamentos.AsReadOnly();

    public Guid? GarantiaId { get; private set; }

    public bool EstaConcluido => DataHoraConclusao is not null;

    /// <summary>
    /// RF0055. RN0042 permite um novo orcamento depois de uma recusa, entao o atendimento
    /// guarda a serie de orcamentos, e nao apenas o ultimo.
    /// </summary>
    public void RegistrarOrcamento(Orcamento orcamento)
    {
        Garantir.NaoNulo(orcamento, "orcamento", "RF0055");

        if (EstaConcluido)
        {
            throw new ExcecaoDeDominio(
                "Nao e possivel registrar orcamento em atendimento ja concluido.",
                "RF0055");
        }

        if (orcamento.AtendimentoId != Id)
        {
            throw new ExcecaoDeDominio("O orcamento pertence a outro atendimento.", "RF0055");
        }

        if (_orcamentos.Any(o => o.Status == StatusOrcamento.Pendente))
        {
            throw new ExcecaoDeDominio(
                "Ja existe um orcamento pendente de decisao para este atendimento.",
                "RF0055");
        }

        if (_orcamentos.Any(o => o.Status == StatusOrcamento.Aprovado))
        {
            throw new ExcecaoDeDominio(
                "Este atendimento ja possui um orcamento aprovado.",
                "RF0055");
        }

        _orcamentos.Add(orcamento);
    }

    public Orcamento? OrcamentoPendente() =>
        _orcamentos.SingleOrDefault(o => o.Status == StatusOrcamento.Pendente);

    public Orcamento? OrcamentoAprovado() =>
        _orcamentos.SingleOrDefault(o => o.Status == StatusOrcamento.Aprovado);

    /// <summary>
    /// RF0057: conclusao do atendimento. Publica o evento que dispara a solicitacao de
    /// avaliacao (RN0051), a criacao da garantia e a geracao da fatura (RN0071).
    /// </summary>
    public Garantia Concluir(
        string relatoTecnico,
        Guid clienteId,
        bool chamadoDeGarantia,
        DateTimeOffset agora,
        IGeradorId geradorId)
    {
        if (EstaConcluido)
        {
            throw new ExcecaoDeDominio("Este atendimento ja foi concluido.", "RF0057");
        }

        if (agora < DataHoraInicio)
        {
            throw new ExcecaoDeDominio(
                "A conclusao nao pode ser anterior ao inicio do atendimento.",
                "RF0057");
        }

        if (OrcamentoPendente() is not null)
        {
            throw new ExcecaoDeDominio(
                "Ha um orcamento pendente de decisao do cliente. Conclua o atendimento apos a decisao.",
                "RN0043");
        }

        RelatoTecnico = Garantir.TextoComTamanhoMaximo(relatoTecnico, 2000, "relato tecnico", "RF0057");
        DataHoraConclusao = agora;

        var garantia = new Garantia(geradorId.NovoId(), Id, agora);
        GarantiaId = garantia.Id;

        RegistrarEvento(new AtendimentoConcluido(Id, ChamadoId, clienteId, chamadoDeGarantia, agora));

        return garantia;
    }
}
