using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Financeiro;
using ChamadosManutencao.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace ChamadosManutencao.Infrastructure.Notificacao;

/// <summary>
/// RNF0041: notificacao a cada mudanca de status. Esta implementacao registra log estruturado
/// e grava em notificacao_enviada, o que permite trocar por SMTP ou push depois sem tocar no
/// dominio.
/// </summary>
public sealed class NotificadorPorLog : INotificador
{
    private const string Canal = "Log";

    private readonly AppDbContext _contexto;
    private readonly IRelogio _relogio;
    private readonly IGeradorId _geradorId;
    private readonly ILogger<NotificadorPorLog> _log;

    public NotificadorPorLog(
        AppDbContext contexto,
        IRelogio relogio,
        IGeradorId geradorId,
        ILogger<NotificadorPorLog> log)
    {
        _contexto = contexto;
        _relogio = relogio;
        _geradorId = geradorId;
        _log = log;
    }

    public Task NotificarMudancaDeStatusAsync(
        Chamado chamado,
        StatusChamado? statusAnterior,
        CancellationToken cancellationToken = default)
    {
        var assunto = $"Chamado {chamado.Numero}: status alterado para {chamado.Status}.";
        var corpo = statusAnterior is null
            ? $"O chamado {chamado.Numero} foi aberto com status {chamado.Status}."
            : $"O chamado {chamado.Numero} passou de {statusAnterior} para {chamado.Status}.";

        return RegistrarAsync(chamado.ClienteId, assunto, corpo, "Chamado", chamado.Id, cancellationToken);
    }

    public Task SolicitarAvaliacaoAsync(Chamado chamado, CancellationToken cancellationToken = default)
    {
        var assunto = $"Avalie o atendimento do chamado {chamado.Numero}.";
        var corpo = "O atendimento foi concluido. Voce tem 15 dias corridos para registrar sua avaliacao.";

        return RegistrarAsync(chamado.ClienteId, assunto, corpo, "Chamado", chamado.Id, cancellationToken);
    }

    public Task NotificarVencimentoDeFaturaAsync(Fatura fatura, CancellationToken cancellationToken = default)
    {
        var assunto = $"Fatura {fatura.Numero} vencida.";
        var corpo = $"A fatura {fatura.Numero}, no valor de {fatura.ValorTotal:F2}, venceu em "
            + $"{fatura.DataVencimento:dd/MM/yyyy}.";

        return RegistrarAsync(fatura.ClienteId, assunto, corpo, "Fatura", fatura.Id, cancellationToken);
    }

    private async Task RegistrarAsync(
        Guid destinatarioId,
        string assunto,
        string corpo,
        string referenciaTipo,
        Guid referenciaId,
        CancellationToken cancellationToken)
    {
        _log.LogInformation(
            "Notificacao para {DestinatarioId}: {Assunto}",
            destinatarioId,
            assunto);

        _contexto.NotificacoesEnviadas.Add(new NotificacaoEnviada
        {
            Id = _geradorId.NovoId(),
            DataHora = _relogio.Agora,
            DestinatarioId = destinatarioId,
            Canal = Canal,
            Assunto = assunto,
            Corpo = corpo,
            ReferenciaTipo = referenciaTipo,
            ReferenciaId = referenciaId
        });

        await _contexto.SaveChangesAsync(cancellationToken);
    }
}
