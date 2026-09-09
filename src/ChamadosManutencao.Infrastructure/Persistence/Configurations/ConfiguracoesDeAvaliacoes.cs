using ChamadosManutencao.Domain.Avaliacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChamadosManutencao.Infrastructure.Persistence.Configurations;

/// <summary>Avaliacao do atendimento (RF0061, RN0052).</summary>
public sealed class AvaliacaoConfiguration : IEntityTypeConfiguration<Avaliacao>
{
    public void Configure(EntityTypeBuilder<Avaliacao> builder)
    {
        builder.ToTable("avaliacao", tabela => tabela.HasCheckConstraint(
            "ck_avaliacao_nota",
            "nota BETWEEN 1 AND 5"));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Nota).IsRequired();
        builder.Property(a => a.Comentario).HasMaxLength(2000);
        builder.Property(a => a.DataHoraRegistro).IsRequired();

        builder.Ignore(a => a.Eventos);

        // Um chamado tem no maximo uma avaliacao.
        builder.HasIndex(a => a.ChamadoId).HasDatabaseName("ix_avaliacao_chamado_id").IsUnique();
        builder.HasIndex(a => a.TecnicoId).HasDatabaseName("ix_avaliacao_tecnico_id");

        builder.HasOne(a => a.Resposta)
            .WithOne()
            .HasForeignKey<RespostaAvaliacao>(r => r.AvaliacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(a => a.Resposta).UsePropertyAccessMode(PropertyAccessMode.Property);
    }
}

/// <summary>Resposta publica do administrador (RF0063).</summary>
public sealed class RespostaAvaliacaoConfiguration : IEntityTypeConfiguration<RespostaAvaliacao>
{
    public void Configure(EntityTypeBuilder<RespostaAvaliacao> builder)
    {
        builder.ToTable("resposta_avaliacao");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Texto).HasMaxLength(2000).IsRequired();
        builder.Property(r => r.DataHoraResposta).IsRequired();
        builder.Property(r => r.AdministradorId).IsRequired();

        builder.HasIndex(r => r.AvaliacaoId).HasDatabaseName("ix_resposta_avaliacao_avaliacao_id").IsUnique();
    }
}
