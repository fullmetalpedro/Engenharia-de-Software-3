using ChamadosManutencao.Domain.Atendimentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChamadosManutencao.Infrastructure.Persistence.Configurations;

/// <summary>Atendimento: raiz de agregado com concorrencia otimista (RF0054, RF0057).</summary>
public sealed class AtendimentoConfiguration : IEntityTypeConfiguration<Atendimento>
{
    public void Configure(EntityTypeBuilder<Atendimento> builder)
    {
        builder.ToTable("atendimento");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.DataHoraInicio).IsRequired();
        builder.Property(a => a.DataHoraConclusao);
        builder.Property(a => a.RelatoTecnico).HasMaxLength(2000);

        builder.UsarXmin();

        builder.Ignore(a => a.Orcamentos);
        builder.Ignore(a => a.EstaConcluido);
        builder.Ignore(a => a.Eventos);

        // Sem unicidade: a RN0035 devolve o chamado para EM ANALISE e cada passagem gera um
        // atendimento. O diagrama de classes desenha 0..1, mas com o indice unico o chamado
        // reaberto nunca poderia ser reconcluido.
        builder.HasIndex(a => a.ChamadoId).HasDatabaseName("ix_atendimento_chamado_id");
        builder.HasIndex(a => a.TecnicoId).HasDatabaseName("ix_atendimento_tecnico_id");

        builder.HasMany<Orcamento>("_orcamentos")
            .WithOne()
            .HasForeignKey(o => o.AtendimentoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_orcamentos").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Orcamento com concorrencia otimista (RF0055, RN0042, RN0043).</summary>
public sealed class OrcamentoConfiguration : IEntityTypeConfiguration<Orcamento>
{
    public void Configure(EntityTypeBuilder<Orcamento> builder)
    {
        builder.ToTable("orcamento", tabela => tabela.HasCheckConstraint(
            "ck_orcamento_status",
            "status BETWEEN 1 AND 4"));

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.DataHoraRegistro).IsRequired();
        builder.Property(o => o.PrazoAprovacao).IsRequired();
        builder.Property(o => o.Status).HasConversion<int>().IsRequired();
        builder.Property(o => o.DataHoraDecisao);

        builder.UsarXmin();

        builder.Ignore(o => o.Itens);
        builder.Ignore(o => o.Eventos);

        builder.HasIndex(o => o.AtendimentoId).HasDatabaseName("ix_orcamento_atendimento_id");

        // RN0043: o job de expiracao varre por status e prazo.
        builder.HasIndex(o => new { o.Status, o.PrazoAprovacao })
            .HasDatabaseName("ix_orcamento_status_prazo_aprovacao");

        builder.HasMany<ItemOrcamento>("_itens")
            .WithOne()
            .HasForeignKey(i => i.OrcamentoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_itens").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Item do orcamento (RF0055, RN0071).</summary>
public sealed class ItemOrcamentoConfiguration : IEntityTypeConfiguration<ItemOrcamento>
{
    public void Configure(EntityTypeBuilder<ItemOrcamento> builder)
    {
        builder.ToTable("item_orcamento", tabela =>
        {
            tabela.HasCheckConstraint("ck_item_orcamento_tipo", "tipo BETWEEN 1 AND 2");
            tabela.HasCheckConstraint("ck_item_orcamento_quantidade", "quantidade > 0");
            tabela.HasCheckConstraint("ck_item_orcamento_valor", "valor_unitario >= 0");
        });

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Descricao).HasMaxLength(300).IsRequired();
        builder.Property(i => i.Tipo).HasConversion<int>().IsRequired();
        builder.Property(i => i.Quantidade).IsRequired();
        builder.Property(i => i.ValorUnitario).HasColumnType("decimal(12,2)").IsRequired();

        builder.HasIndex(i => i.OrcamentoId).HasDatabaseName("ix_item_orcamento_orcamento_id");
    }
}

/// <summary>Garantia do servico executado (RF0085, RN0072).</summary>
public sealed class GarantiaConfiguration : IEntityTypeConfiguration<Garantia>
{
    public void Configure(EntityTypeBuilder<Garantia> builder)
    {
        builder.ToTable("garantia", tabela => tabela.HasCheckConstraint(
            "ck_garantia_prazo",
            "prazo_dias > 0 AND data_fim > data_inicio"));

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.DataInicio).IsRequired();
        builder.Property(g => g.DataFim).IsRequired();
        builder.Property(g => g.PrazoDias).IsRequired();

        builder.HasIndex(g => g.AtendimentoId).HasDatabaseName("ix_garantia_atendimento_id").IsUnique();

        // RN0072: a vigencia e consultada pela data de fim.
        builder.HasIndex(g => g.DataFim).HasDatabaseName("ix_garantia_data_fim");
    }
}
