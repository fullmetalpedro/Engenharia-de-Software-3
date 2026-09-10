using ChamadosManutencao.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChamadosManutencao.Infrastructure.Persistence.Configurations;

/// <summary>Fatura do atendimento concluido (RF0082, RF0083, RN0071).</summary>
public sealed class FaturaConfiguration : IEntityTypeConfiguration<Fatura>
{
    public void Configure(EntityTypeBuilder<Fatura> builder)
    {
        builder.ToTable("fatura", tabela =>
        {
            tabela.HasCheckConstraint("ck_fatura_status", "status BETWEEN 1 AND 4");
            tabela.HasCheckConstraint(
                "ck_fatura_valores",
                "valor_pecas >= 0 AND valor_mao_obra >= 0 AND taxa_deslocamento >= 0 AND valor_total >= 0");
        });

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Numero).IsRequired();
        builder.HasIndex(f => f.Numero).HasDatabaseName("ix_fatura_numero").IsUnique();

        builder.Property(f => f.DataEmissao).IsRequired();
        builder.Property(f => f.DataVencimento).IsRequired();
        builder.Property(f => f.ValorPecas).HasColumnType("decimal(12,2)").IsRequired();
        builder.Property(f => f.ValorMaoObra).HasColumnType("decimal(12,2)").IsRequired();
        builder.Property(f => f.TaxaDeslocamento).HasColumnType("decimal(12,2)").IsRequired();
        builder.Property(f => f.ValorTotal).HasColumnType("decimal(12,2)").IsRequired();
        builder.Property(f => f.Status).HasConversion<int>().IsRequired();

        builder.UsarXmin();

        builder.Ignore(f => f.Pagamentos);
        builder.Ignore(f => f.Eventos);

        // RNF0011: sustenta os filtros de status e periodo da RF0083.
        builder.HasIndex(f => new { f.Status, f.DataVencimento })
            .HasDatabaseName("ix_fatura_status_data_vencimento");
        builder.HasIndex(f => f.ClienteId).HasDatabaseName("ix_fatura_cliente_id");
        builder.HasIndex(f => f.AtendimentoId).HasDatabaseName("ix_fatura_atendimento_id").IsUnique();
        builder.HasIndex(f => f.ChamadoId).HasDatabaseName("ix_fatura_chamado_id");

        builder.HasMany<Pagamento>("_pagamentos")
            .WithOne()
            .HasForeignKey(p => p.FaturaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_pagamentos").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Pagamento de fatura (RF0084).</summary>
public sealed class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("pagamento", tabela =>
        {
            tabela.HasCheckConstraint("ck_pagamento_status", "status BETWEEN 1 AND 4");
            tabela.HasCheckConstraint("ck_pagamento_valor", "valor_pago >= 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.DataHoraPagamento).IsRequired();
        builder.Property(p => p.ValorPago).HasColumnType("decimal(12,2)").IsRequired();
        builder.Property(p => p.IdentificadorTransacao).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Status).HasConversion<int>().IsRequired();

        builder.HasIndex(p => p.FaturaId).HasDatabaseName("ix_pagamento_fatura_id");
        builder.HasIndex(p => p.IdentificadorTransacao)
            .HasDatabaseName("ix_pagamento_identificador_transacao");

        builder.HasOne<FormaPagamento>()
            .WithMany()
            .HasForeignKey(p => p.FormaPagamentoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Forma de pagamento em tabela unica (TPH) com discriminador tipo_forma_pagamento.
/// RNF0061: o numero completo do cartao nao existe no modelo, so o token.
/// </summary>
public sealed class FormaPagamentoConfiguration : IEntityTypeConfiguration<FormaPagamento>
{
    public void Configure(EntityTypeBuilder<FormaPagamento> builder)
    {
        builder.ToTable("forma_pagamento");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.HasDiscriminator<string>("tipo_forma_pagamento")
            .HasValue<CartaoCredito>("CartaoCredito")
            .HasValue<Pix>("Pix")
            .HasValue<Boleto>("Boleto");

        builder.Property(f => f.Apelido).HasMaxLength(60).IsRequired();
        builder.Property(f => f.Principal).IsRequired();
        builder.Property(f => f.Ativa).IsRequired();

        builder.HasIndex(f => f.ClienteId).HasDatabaseName("ix_forma_pagamento_cliente_id");
    }
}

/// <summary>Cartao tokenizado (RNF0061).</summary>
public sealed class CartaoCreditoConfiguration : IEntityTypeConfiguration<CartaoCredito>
{
    public void Configure(EntityTypeBuilder<CartaoCredito> builder)
    {
        builder.Property(c => c.Bandeira).HasMaxLength(30);
        builder.Property(c => c.UltimosQuatroDigitos).HasMaxLength(4);
        builder.Property(c => c.TokenOperadora).HasMaxLength(200);
        builder.Property(c => c.Validade).HasMaxLength(7);
        builder.Property(c => c.NomeTitular).HasMaxLength(200);
    }
}

/// <summary>Chave PIX (RF0081).</summary>
public sealed class PixConfiguration : IEntityTypeConfiguration<Pix>
{
    public void Configure(EntityTypeBuilder<Pix> builder)
    {
        builder.Property(p => p.ChavePix).HasMaxLength(140);
        builder.Property(p => p.TipoChave).HasConversion<int>();
    }
}

/// <summary>Boleto (RF0081).</summary>
public sealed class BoletoConfiguration : IEntityTypeConfiguration<Boleto>
{
    public void Configure(EntityTypeBuilder<Boleto> builder)
    {
        builder.Property(b => b.NomeSacado).HasMaxLength(200);
        builder.Property(b => b.EmailEnvio).HasMaxLength(200);
    }
}
