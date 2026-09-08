using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Chamados;

/// <summary>
/// Proposta de data e hora para a execucao do atendimento.
/// Requisitos: RF0051, RF0052, RF0053, RN0041.
/// </summary>
public sealed class Agendamento : Entidade
{
    public Agendamento(
        Guid id,
        Guid chamadoId,
        DateTimeOffset dataHoraProposta,
        int duracaoEmMinutos,
        OrigemProposta origemProposta)
        : base(id)
    {
        if (duracaoEmMinutos <= 0)
        {
            throw new ExcecaoDeDominio(
                "A duracao do atendimento deve ser positiva.",
                "RN0041");
        }

        ChamadoId = chamadoId;
        DataHoraProposta = dataHoraProposta;
        DuracaoEmMinutos = duracaoEmMinutos;
        OrigemProposta = origemProposta;
        Status = StatusAgendamento.Proposto;
    }

    private Agendamento()
    {
    }

    public Guid ChamadoId { get; private set; }

    public DateTimeOffset DataHoraProposta { get; private set; }

    /// <summary>
    /// Duracao da janela ocupada pelo atendimento (decisao D08). O diagrama nao previa
    /// duracao, mas sem ela a RN0041 nao consegue detectar sobreposicao de horario.
    /// </summary>
    public int DuracaoEmMinutos { get; private set; }

    public DateTimeOffset? DataHoraConfirmacao { get; private set; }

    public OrigemProposta OrigemProposta { get; private set; }

    public StatusAgendamento Status { get; private set; }

    public DateTimeOffset FimPrevisto => DataHoraProposta.AddMinutes(DuracaoEmMinutos);

    /// <summary>Janela ocupada por este agendamento, usada pela RN0041.</summary>
    public Pessoas.JanelaDeAtendimento Janela() => new(DataHoraProposta, FimPrevisto);

    /// <summary>RF0052.</summary>
    public void Confirmar(DateTimeOffset agora)
    {
        if (Status is not (StatusAgendamento.Proposto or StatusAgendamento.Reagendado))
        {
            throw new ExcecaoDeDominio(
                $"Somente agendamento proposto ou reagendado pode ser confirmado. Status atual: {Status}.",
                "RF0052");
        }

        Status = StatusAgendamento.Confirmado;
        DataHoraConfirmacao = agora;
    }

    /// <summary>RF0053: a proposta antiga sai de cena e o chamado recebe uma nova.</summary>
    public void MarcarComoReagendado()
    {
        if (Status == StatusAgendamento.Cancelado)
        {
            throw new ExcecaoDeDominio(
                "Agendamento cancelado nao pode ser reagendado.",
                "RF0053");
        }

        Status = StatusAgendamento.Reagendado;
    }

    public void Cancelar() => Status = StatusAgendamento.Cancelado;

    /// <summary>Agendamentos que ainda ocupam a agenda do tecnico (RN0041).</summary>
    public bool OcupaAgenda() =>
        Status is StatusAgendamento.Proposto or StatusAgendamento.Confirmado;
}
