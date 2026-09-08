using ChamadosManutencao.Domain.Common;

namespace ChamadosManutencao.Domain.Avaliacoes;

/// <summary>
/// Avaliacao do atendimento pelo cliente. Requisitos: RF0061, RF0063, RN0051, RN0052.
/// </summary>
public sealed class Avaliacao : RaizDeAgregado
{
    /// <summary>RN0052: prazo em dias corridos apos a conclusao do atendimento.</summary>
    public const int PrazoParaAvaliarEmDias = 15;

    public Avaliacao(
        Guid id,
        Guid chamadoId,
        Guid clienteId,
        Guid? tecnicoId,
        int nota,
        string? comentario,
        DateTimeOffset dataHoraRegistro)
        : base(id)
    {
        if (nota is < 1 or > 5)
        {
            throw new ExcecaoDeDominio("A nota da avaliacao deve estar entre 1 e 5.", "RF0061");
        }

        ChamadoId = chamadoId;
        ClienteId = clienteId;
        TecnicoId = tecnicoId;
        Nota = nota;
        Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
        DataHoraRegistro = dataHoraRegistro;
    }

    private Avaliacao()
    {
    }

    public Guid ChamadoId { get; private set; }

    public Guid ClienteId { get; private set; }

    /// <summary>Tecnico que executou o atendimento, usado por RF0062.</summary>
    public Guid? TecnicoId { get; private set; }

    public int Nota { get; private set; }

    public string? Comentario { get; private set; }

    public DateTimeOffset DataHoraRegistro { get; private set; }

    public RespostaAvaliacao? Resposta { get; private set; }

    /// <summary>RN0052: a janela de avaliacao e derivada da data de conclusao (decisao D13).</summary>
    public static bool DentroDoPrazo(DateTimeOffset dataHoraConclusao, DateTimeOffset agora) =>
        agora <= dataHoraConclusao.AddDays(PrazoParaAvaliarEmDias);

    /// <summary>RF0063: resposta publica do administrador.</summary>
    public RespostaAvaliacao Responder(
        Guid respostaId,
        Guid administradorId,
        string texto,
        DateTimeOffset agora)
    {
        if (Resposta is not null)
        {
            throw new ExcecaoDeDominio("Esta avaliacao ja foi respondida.", "RF0063");
        }

        Resposta = new RespostaAvaliacao(respostaId, Id, texto, agora, administradorId);
        return Resposta;
    }
}
