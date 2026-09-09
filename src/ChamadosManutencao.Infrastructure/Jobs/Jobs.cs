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
