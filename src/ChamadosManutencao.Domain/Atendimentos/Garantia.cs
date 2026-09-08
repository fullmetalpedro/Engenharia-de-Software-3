using ChamadosManutencao.Domain.Common;

namespace ChamadosManutencao.Domain.Atendimentos;

/// <summary>
/// Garantia do servico executado. Requisitos: RF0085, RN0072.
/// </summary>
public sealed class Garantia : Entidade
{
    /// <summary>RN0072: 90 dias corridos contados da conclusao do atendimento.</summary>
    public const int PrazoPadraoEmDias = 90;

    public Garantia(Guid id, Guid atendimentoId, DateTimeOffset dataInicio, int prazoDias = PrazoPadraoEmDias)
        : base(id)
    {
        if (prazoDias <= 0)
        {
            throw new ExcecaoDeDominio("O prazo da garantia deve ser positivo.", "RN0072");
        }

        AtendimentoId = atendimentoId;
        DataInicio = dataInicio;
        PrazoDias = prazoDias;
        DataFim = dataInicio.AddDays(prazoDias);
    }

    private Garantia()
    {
    }

    public Guid AtendimentoId { get; private set; }

    public DateTimeOffset DataInicio { get; private set; }

    public DateTimeOffset DataFim { get; private set; }

    public int PrazoDias { get; private set; }

    public bool EstaVigente(DateTimeOffset agora) => agora >= DataInicio && agora <= DataFim;

    /// <summary>
    /// RN0072: a garantia so e acionavel dentro do prazo, com a fatura correspondente quitada
    /// e para o mesmo tipo de servico executado. Chamado de garantia nao gera fatura, entao
    /// nesse caso a checagem recai sobre a fatura do atendimento original (decisao D12) — quem
    /// resolve qual fatura olhar e o handler, que passa o resultado em faturaQuitada.
    /// </summary>
    public bool PodeSerAcionada(
        DateTimeOffset agora,
        bool faturaQuitada,
        Guid tipoServicoDoAtendimento,
        Guid tipoServicoSolicitado) =>
        EstaVigente(agora)
        && faturaQuitada
        && tipoServicoDoAtendimento == tipoServicoSolicitado;

    /// <summary>Explica por que o acionamento foi recusado, para a resposta da API.</summary>
    public string? MotivoDeRecusa(
        DateTimeOffset agora,
        bool faturaQuitada,
        Guid tipoServicoDoAtendimento,
        Guid tipoServicoSolicitado)
    {
        if (!EstaVigente(agora))
        {
            return $"A garantia venceu em {DataFim:dd/MM/yyyy}.";
        }

        if (!faturaQuitada)
        {
            return "A fatura correspondente ao atendimento nao esta quitada.";
        }

        if (tipoServicoDoAtendimento != tipoServicoSolicitado)
        {
            return "O chamado de garantia deve se referir ao mesmo tipo de servico executado.";
        }

        return null;
    }
}
