using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Avaliacoes;
using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Financeiro;
using ChamadosManutencao.Domain.Pessoas;
using ChamadosManutencao.Infrastructure.Auditoria;
using ChamadosManutencao.Infrastructure.Notificacao;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Infrastructure.Persistence;

/// <summary>
/// Contexto unico da aplicacao. Todo mapeamento vem de classes IEntityTypeConfiguration; nao
/// ha data annotation nas entidades de dominio nem mapeamento por convencao implicita.
/// </summary>
public sealed class AppDbContext : DbContext
{
    /// <summary>Sequence do numero de chamado (RNF0042).</summary>
    public const string SequenceNumeroChamado = "seq_numero_chamado";

    /// <summary>Sequence do numero de fatura.</summary>
    public const string SequenceNumeroFatura = "seq_numero_fatura";

    /// <summary>Sequence do codigo de cliente (RNF0023).</summary>
    public const string SequenceCodigoCliente = "seq_codigo_cliente";

    /// <summary>Sequence do codigo de tecnico (RNF0031).</summary>
    public const string SequenceCodigoTecnico = "seq_codigo_tecnico";

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Tecnico> Tecnicos => Set<Tecnico>();

    public DbSet<Administrador> Administradores => Set<Administrador>();

    public DbSet<Imovel> Imoveis => Set<Imovel>();

    public DbSet<CategoriaServico> CategoriasServico => Set<CategoriaServico>();

    public DbSet<TipoServico> TiposServico => Set<TipoServico>();

    public DbSet<AreaAtendimento> AreasAtendimento => Set<AreaAtendimento>();

    public DbSet<Chamado> Chamados => Set<Chamado>();

    public DbSet<Anexo> Anexos => Set<Anexo>();

    public DbSet<HistoricoStatus> HistoricosDeStatus => Set<HistoricoStatus>();

    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();

    public DbSet<Atendimento> Atendimentos => Set<Atendimento>();

    public DbSet<Orcamento> Orcamentos => Set<Orcamento>();

    public DbSet<ItemOrcamento> ItensDeOrcamento => Set<ItemOrcamento>();

    public DbSet<Garantia> Garantias => Set<Garantia>();

    public DbSet<Avaliacao> Avaliacoes => Set<Avaliacao>();

    public DbSet<RespostaAvaliacao> RespostasDeAvaliacao => Set<RespostaAvaliacao>();

    public DbSet<Fatura> Faturas => Set<Fatura>();

    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();

    public DbSet<FormaPagamento> FormasDePagamento => Set<FormaPagamento>();

    public DbSet<LogTransacao> LogsDeTransacao => Set<LogTransacao>();

    public DbSet<NotificacaoEnviada> NotificacoesEnviadas => Set<NotificacaoEnviada>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>(SequenceNumeroChamado).StartsAt(1).IncrementsBy(1);
        modelBuilder.HasSequence<long>(SequenceNumeroFatura).StartsAt(1).IncrementsBy(1);
        modelBuilder.HasSequence<long>(SequenceCodigoCliente).StartsAt(1).IncrementsBy(1);
        modelBuilder.HasSequence<long>(SequenceCodigoTecnico).StartsAt(1).IncrementsBy(1);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
