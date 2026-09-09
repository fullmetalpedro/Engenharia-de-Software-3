using ChamadosManutencao.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChamadosManutencao.Infrastructure.Persistence.Configurations;

/// <summary>Categoria de servico (RF0031, RN0031, RN0032).</summary>
public sealed class CategoriaServicoConfiguration : IEntityTypeConfiguration<CategoriaServico>
{
    public void Configure(EntityTypeBuilder<CategoriaServico> builder)
    {
        builder.ToTable("categoria_servico");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Nome).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Descricao).HasMaxLength(400).IsRequired();
        builder.Property(c => c.ExigeFoto).IsRequired();
        builder.Property(c => c.CategoriaDeRisco).IsRequired();
        builder.Property(c => c.Ativa).IsRequired();

        builder.Ignore(c => c.TiposServico);
        builder.Ignore(c => c.Eventos);

        builder.HasIndex(c => c.Nome).HasDatabaseName("ix_categoria_servico_nome").IsUnique();

        builder.HasMany<TipoServico>("_tiposServico")
            .WithOne()
            .HasForeignKey(t => t.CategoriaServicoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation("_tiposServico").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Tipo de servico vinculado a categoria (RF0032).</summary>
public sealed class TipoServicoConfiguration : IEntityTypeConfiguration<TipoServico>
{
    public void Configure(EntityTypeBuilder<TipoServico> builder)
    {
        builder.ToTable("tipo_servico");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Nome).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Descricao).HasMaxLength(400).IsRequired();
        builder.Property(t => t.Ativo).IsRequired();

        builder.HasIndex(t => t.CategoriaServicoId).HasDatabaseName("ix_tipo_servico_categoria_id");
    }
}

/// <summary>Area de atendimento do tecnico (RF0027, RN0023, RN0071).</summary>
public sealed class AreaAtendimentoConfiguration : IEntityTypeConfiguration<AreaAtendimento>
{
    public void Configure(EntityTypeBuilder<AreaAtendimento> builder)
    {
        builder.ToTable("area_atendimento", tabela => tabela.HasCheckConstraint(
            "ck_area_atendimento_faixa_cep",
            "cep_inicial <= cep_final"));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Bairro).HasMaxLength(120).IsRequired();
        builder.Property(a => a.CepInicial).HasMaxLength(8).IsRequired();
        builder.Property(a => a.CepFinal).HasMaxLength(8).IsRequired();
        builder.Property(a => a.TaxaDeslocamento).HasColumnType("decimal(12,2)").IsRequired();

        builder.HasIndex(a => a.TecnicoId).HasDatabaseName("ix_area_atendimento_tecnico_id");
        builder.HasIndex(a => new { a.CepInicial, a.CepFinal })
            .HasDatabaseName("ix_area_atendimento_faixa_cep");
    }
}
