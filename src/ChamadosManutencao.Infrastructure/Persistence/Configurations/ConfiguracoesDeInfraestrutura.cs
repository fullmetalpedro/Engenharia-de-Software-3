using ChamadosManutencao.Infrastructure.Auditoria;
using ChamadosManutencao.Infrastructure.Notificacao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChamadosManutencao.Infrastructure.Persistence.Configurations;

/// <summary>Log de transacao exigido pela RNF0012.</summary>
public sealed class LogTransacaoConfiguration : IEntityTypeConfiguration<LogTransacao>
{
    public void Configure(EntityTypeBuilder<LogTransacao> builder)
    {
        builder.ToTable("log_transacao");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.DataHora).IsRequired();
        builder.Property(l => l.Entidade).HasMaxLength(120).IsRequired();
        builder.Property(l => l.ChavePrimaria).HasMaxLength(120).IsRequired();
        builder.Property(l => l.Operacao).HasMaxLength(20).IsRequired();
        builder.Property(l => l.EstadoAnterior).HasColumnType("jsonb");
        builder.Property(l => l.EstadoNovo).HasColumnType("jsonb");

        builder.HasIndex(l => new { l.Entidade, l.ChavePrimaria })
            .HasDatabaseName("ix_log_transacao_entidade_chave");
        builder.HasIndex(l => l.DataHora).HasDatabaseName("ix_log_transacao_data_hora");
    }
}

/// <summary>Notificacao enviada ao cliente (RNF0041).</summary>
public sealed class NotificacaoEnviadaConfiguration : IEntityTypeConfiguration<NotificacaoEnviada>
{
    public void Configure(EntityTypeBuilder<NotificacaoEnviada> builder)
    {
        builder.ToTable("notificacao_enviada");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.DataHora).IsRequired();
        builder.Property(n => n.DestinatarioId).IsRequired();
        builder.Property(n => n.Canal).HasMaxLength(30).IsRequired();
        builder.Property(n => n.Assunto).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Corpo).HasMaxLength(2000).IsRequired();
        builder.Property(n => n.ReferenciaTipo).HasMaxLength(60).IsRequired();
        builder.Property(n => n.ReferenciaId).IsRequired();

        builder.HasIndex(n => n.DestinatarioId).HasDatabaseName("ix_notificacao_enviada_destinatario_id");
        builder.HasIndex(n => new { n.ReferenciaTipo, n.ReferenciaId })
            .HasDatabaseName("ix_notificacao_enviada_referencia");
    }
}
