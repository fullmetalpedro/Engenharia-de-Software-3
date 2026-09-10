using ChamadosManutencao.Domain.Chamados;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChamadosManutencao.Infrastructure.Persistence.Configurations;

/// <summary>
/// Chamado: raiz de agregado com concorrencia otimista por xmin.
/// Requisitos: RF0041, RF0050, RN0034, RNF0011, RNF0042.
/// </summary>
public sealed class ChamadoConfiguration : IEntityTypeConfiguration<Chamado>
{
    public void Configure(EntityTypeBuilder<Chamado> builder)
    {
        builder.ToTable("chamado", tabela =>
        {
            tabela.HasCheckConstraint("ck_chamado_status", "status BETWEEN 1 AND 6");
            tabela.HasCheckConstraint("ck_chamado_urgencia", "urgencia BETWEEN 1 AND 3");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        // RNF0042: numero sequencial unico vindo da sequence.
        builder.Property(c => c.Numero).IsRequired();
        builder.HasIndex(c => c.Numero).HasDatabaseName("ix_chamado_numero").IsUnique();

        builder.Property(c => c.DataHoraAbertura).IsRequired();
        builder.Property(c => c.DescricaoProblema).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.Urgencia).HasConversion<int>().IsRequired();
        builder.Property(c => c.Status).HasConversion<int>().IsRequired();
        builder.Property(c => c.ChamadoDeGarantia).IsRequired();

        builder.UsarXmin();

        builder.Ignore(c => c.Anexos);
        builder.Ignore(c => c.HistoricoStatus);
        builder.Ignore(c => c.Agendamentos);
        builder.Ignore(c => c.Eventos);

        // RNF0011: indices das consultas de RF0043 e RF0044.
        builder.HasIndex(c => new { c.Status, c.DataHoraAbertura })
            .HasDatabaseName("ix_chamado_status_data_hora_abertura");
        builder.HasIndex(c => c.ClienteId).HasDatabaseName("ix_chamado_cliente_id");
        builder.HasIndex(c => c.TecnicoId).HasDatabaseName("ix_chamado_tecnico_id");
        builder.HasIndex(c => c.TipoServicoId).HasDatabaseName("ix_chamado_tipo_servico_id");

        builder.HasMany<Anexo>("_anexos")
            .WithOne()
            .HasForeignKey(a => a.ChamadoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_anexos").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany<HistoricoStatus>("_historicoStatus")
            .WithOne()
            .HasForeignKey(h => h.ChamadoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_historicoStatus").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany<Agendamento>("_agendamentos")
            .WithOne()
            .HasForeignKey(a => a.ChamadoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_agendamentos").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// Anexo: pertence a um chamado, a um atendimento ou a um tecnico, nunca a mais de um
/// (decisao D21). Requisitos: RF0042, RF0057, RNF0032, RNF0043.
/// </summary>
public sealed class AnexoConfiguration : IEntityTypeConfiguration<Anexo>
{
    public void Configure(EntityTypeBuilder<Anexo> builder)
    {
        builder.ToTable("anexo", tabela =>
        {
            tabela.HasCheckConstraint(
                "ck_anexo_dono_unico",
                "num_nonnulls(chamado_id, atendimento_id, tecnico_id) = 1");

            // RNF0043: 10 MB por arquivo.
            tabela.HasCheckConstraint(
                "ck_anexo_tamanho",
                "tamanho_bytes > 0 AND tamanho_bytes <= 10485760");

            tabela.HasCheckConstraint("ck_anexo_origem", "origem BETWEEN 1 AND 3");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.NomeArquivo).HasMaxLength(260).IsRequired();
        builder.Property(a => a.TipoMime).HasMaxLength(120).IsRequired();
        builder.Property(a => a.TamanhoBytes).IsRequired();
        builder.Property(a => a.DataHoraUpload).IsRequired();
        builder.Property(a => a.Caminho).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Origem).HasConversion<int>().IsRequired();

        builder.HasIndex(a => a.ChamadoId).HasDatabaseName("ix_anexo_chamado_id");
        builder.HasIndex(a => a.AtendimentoId).HasDatabaseName("ix_anexo_atendimento_id");
        builder.HasIndex(a => a.TecnicoId).HasDatabaseName("ix_anexo_tecnico_id");
    }
}

/// <summary>Historico de status do chamado (RF0050).</summary>
public sealed class HistoricoStatusConfiguration : IEntityTypeConfiguration<HistoricoStatus>
{
    public void Configure(EntityTypeBuilder<HistoricoStatus> builder)
    {
        builder.ToTable("historico_status", tabela => tabela.HasCheckConstraint(
            "ck_historico_status_valores",
            "status_novo BETWEEN 1 AND 6 AND (status_anterior IS NULL OR status_anterior BETWEEN 1 AND 6)"));

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.Property(h => h.StatusAnterior).HasConversion<int?>();
        builder.Property(h => h.StatusNovo).HasConversion<int>().IsRequired();
        builder.Property(h => h.DataHoraAlteracao).IsRequired();
        builder.Property(h => h.UsuarioResponsavelId).IsRequired();
        builder.Property(h => h.Observacao).HasMaxLength(500);

        // RNF0011: consulta do historico ordenada por data.
        builder.HasIndex(h => new { h.ChamadoId, h.DataHoraAlteracao })
            .HasDatabaseName("ix_historico_status_chamado_id_data_hora_alteracao");
    }
}

/// <summary>Agendamento do atendimento (RF0051, RN0041).</summary>
public sealed class AgendamentoConfiguration : IEntityTypeConfiguration<Agendamento>
{
    public void Configure(EntityTypeBuilder<Agendamento> builder)
    {
        builder.ToTable("agendamento", tabela =>
        {
            tabela.HasCheckConstraint("ck_agendamento_status", "status BETWEEN 1 AND 4");
            tabela.HasCheckConstraint("ck_agendamento_origem", "origem_proposta BETWEEN 1 AND 3");
            tabela.HasCheckConstraint("ck_agendamento_duracao", "duracao_em_minutos > 0");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.DataHoraProposta).IsRequired();
        builder.Property(a => a.DuracaoEmMinutos).IsRequired();
        builder.Property(a => a.DataHoraConfirmacao);
        builder.Property(a => a.OrigemProposta).HasConversion<int>().IsRequired();
        builder.Property(a => a.Status).HasConversion<int>().IsRequired();

        builder.Ignore(a => a.FimPrevisto);

        builder.HasIndex(a => a.ChamadoId).HasDatabaseName("ix_agendamento_chamado_id");
        builder.HasIndex(a => a.DataHoraProposta).HasDatabaseName("ix_agendamento_data_hora_proposta");
    }
}
