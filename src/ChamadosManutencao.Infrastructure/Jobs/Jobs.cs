using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChamadosManutencao.Infrastructure.Jobs;

/// <summary>
/// RN0043: orcamento pendente por mais de 48 horas expira e o chamado e cancelado.
/// Idempotente: so alcanca orcamento ainda pendente com prazo vencido.
/// </summary>
public sealed class ExpiracaoOrcamentoJob : JobPeriodico
{
    public ExpiracaoOrcamentoJob(IServiceScopeFactory fabricaDeEscopo, ILogger<ExpiracaoOrcamentoJob> log)
        : base(fabricaDeEscopo, log)
    {
    }

    protected override string Nome => "ExpiracaoOrcamentoJob";

    public override async Task<int> ProcessarAsync(
        IServiceProvider provedor,
        CancellationToken cancellationToken)
    {
        var contexto = provedor.GetRequiredService<AppDbContext>();
        var relogio = provedor.GetRequiredService<IRelogio>();
        var geradorId = provedor.GetRequiredService<IGeradorId>();
        var unidadeDeTrabalho = provedor.GetRequiredService<IUnitOfWork>();

        var agora = relogio.Agora;

        var vencidos = await contexto.Orcamentos
            .Where(o => o.Status == StatusOrcamento.Pendente && o.PrazoAprovacao < agora)
            .ToListAsync(cancellationToken);

        if (vencidos.Count == 0)
        {
            return 0;
        }

        var atendimentoIds = vencidos.Select(o => o.AtendimentoId).ToList();

        var chamadoPorAtendimento = await contexto.Atendimentos
            .Where(a => atendimentoIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.ChamadoId, cancellationToken);

        var chamadoIds = chamadoPorAtendimento.Values.ToList();

        var chamados = await contexto.Chamados
            .Where(c => chamadoIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        foreach (var orcamento in vencidos)
        {
            orcamento.Expirar(agora);

            if (!chamadoPorAtendimento.TryGetValue(orcamento.AtendimentoId, out var chamadoId)
                || !chamados.TryGetValue(chamadoId, out var chamado))
            {
                continue;
            }

            if (chamado.Status is StatusChamado.Cancelado or StatusChamado.Concluido)
            {
                continue;
            }

            chamado.AlterarStatus(
                StatusChamado.Cancelado,
                chamado.ClienteId,
                "Orcamento expirado sem decisao do cliente no prazo de "
                + $"{Orcamento.PrazoDeAprovacaoEmHoras} horas.",
                agora,
                geradorId);
        }

        await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return vencidos.Count;
    }
}

/// <summary>
/// RF0083: fatura emitida cujo vencimento passou muda para VENCIDA e o cliente e notificado
/// pelo evento FaturaVencida. Idempotente: a fatura ja vencida nao entra na consulta.
/// </summary>
public sealed class VencimentoFaturaJob : JobPeriodico
{
    public VencimentoFaturaJob(IServiceScopeFactory fabricaDeEscopo, ILogger<VencimentoFaturaJob> log)
        : base(fabricaDeEscopo, log)
    {
    }

    protected override string Nome => "VencimentoFaturaJob";

    public override async Task<int> ProcessarAsync(
        IServiceProvider provedor,
        CancellationToken cancellationToken)
    {
        var contexto = provedor.GetRequiredService<AppDbContext>();
        var relogio = provedor.GetRequiredService<IRelogio>();
        var unidadeDeTrabalho = provedor.GetRequiredService<IUnitOfWork>();

        var agora = relogio.Agora;

        var faturas = await contexto.Faturas
            .Where(f => f.Status == StatusFatura.Emitida && f.DataVencimento < agora)
            .ToListAsync(cancellationToken);

        if (faturas.Count == 0)
        {
            return 0;
        }

        var vencidas = faturas.Count(fatura => fatura.RegistrarVencimento(agora));

        await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return vencidas;
    }
}

/// <summary>
/// RN0052: a janela de avaliacao dura 15 dias corridos apos a conclusao do atendimento.
///
/// A janela e derivada da data de conclusao (decisao D13), portanto nao ha estado a mudar no
/// banco: o job existe para dar visibilidade de quantas janelas se fecharam sem avaliacao no
/// intervalo, numero que alimenta o indicador de satisfacao do UC09.
/// </summary>
public sealed class EncerramentoJanelaAvaliacaoJob : JobPeriodico
{
    private readonly ILogger<EncerramentoJanelaAvaliacaoJob> _log;

    public EncerramentoJanelaAvaliacaoJob(
        IServiceScopeFactory fabricaDeEscopo,
        ILogger<EncerramentoJanelaAvaliacaoJob> log)
        : base(fabricaDeEscopo, log) => _log = log;

    protected override string Nome => "EncerramentoJanelaAvaliacaoJob";

    public override async Task<int> ProcessarAsync(
        IServiceProvider provedor,
        CancellationToken cancellationToken)
    {
        var contexto = provedor.GetRequiredService<AppDbContext>();
        var relogio = provedor.GetRequiredService<IRelogio>();

        var agora = relogio.Agora;
        var limite = agora.AddDays(-Domain.Avaliacoes.Avaliacao.PrazoParaAvaliarEmDias);
        var inicioDoIntervalo = limite.Subtract(IntervaloPadrao);

        var encerradas = await contexto.Atendimentos
            .Where(a => a.DataHoraConclusao != null
                && a.DataHoraConclusao > inicioDoIntervalo
                && a.DataHoraConclusao <= limite
                && !contexto.Avaliacoes.Any(av => av.ChamadoId == a.ChamadoId))
            .CountAsync(cancellationToken);

        if (encerradas > 0)
        {
            _log.LogInformation(
                "{Encerradas} janela(s) de avaliacao encerrada(s) sem nota do cliente.",
                encerradas);
        }

        return encerradas;
    }
}

/// <summary>
/// RN0072: a garantia dura 90 dias corridos a partir da conclusao do atendimento.
///
/// Como a vigencia tambem e derivada de data, o job apenas registra quantas garantias venceram
/// no intervalo. Sem isso, o vencimento so apareceria no momento em que o cliente tentasse
/// acionar a garantia.
/// </summary>
public sealed class EncerramentoGarantiaJob : JobPeriodico
{
    private readonly ILogger<EncerramentoGarantiaJob> _log;

    public EncerramentoGarantiaJob(
        IServiceScopeFactory fabricaDeEscopo,
        ILogger<EncerramentoGarantiaJob> log)
        : base(fabricaDeEscopo, log) => _log = log;

    protected override string Nome => "EncerramentoGarantiaJob";

    public override async Task<int> ProcessarAsync(
        IServiceProvider provedor,
        CancellationToken cancellationToken)
    {
        var contexto = provedor.GetRequiredService<AppDbContext>();
        var relogio = provedor.GetRequiredService<IRelogio>();

        var agora = relogio.Agora;
        var inicioDoIntervalo = agora.Subtract(IntervaloPadrao);

        var vencidas = await contexto.Garantias
            .Where(g => g.DataFim > inicioDoIntervalo && g.DataFim <= agora)
            .CountAsync(cancellationToken);

        if (vencidas > 0)
        {
            _log.LogInformation("{Vencidas} garantia(s) venceram no intervalo.", vencidas);
        }

        return vencidas;
    }
}
