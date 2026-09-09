using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Financeiro;
using ChamadosManutencao.Domain.Pessoas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChamadosManutencao.Infrastructure.Persistence.Configurations;

/// <summary>
/// Usuario em tabela unica (TPH) com discriminador tipo_usuario.
/// Requisitos: RN0011, RN0021, RNF0011 (indices), RNF0022.
/// </summary>
public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuario");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.HasDiscriminator<string>("tipo_usuario")
            .HasValue<Cliente>("Cliente")
            .HasValue<Tecnico>("Tecnico")
            .HasValue<Administrador>("Administrador");

        builder.Property(u => u.NomeCompleto).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Cpf).HasMaxLength(11).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Telefone).HasMaxLength(20).IsRequired();
        builder.Property(u => u.SenhaHash).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Ativo).IsRequired();

        // Papel e derivado do tipo concreto, nao ha coluna correspondente.
        builder.Ignore(u => u.Papel);
        builder.Ignore(u => u.Eventos);

        // RNF0011: CPF e e-mail unicos por tipo de usuario (TPH exige indice filtrado).
        builder.HasIndex(u => u.Cpf)
            .HasDatabaseName("ix_usuario_cpf_cliente")
            .IsUnique()
            .HasFilter("tipo_usuario = 'Cliente'");

        builder.HasIndex(u => u.Email)
            .HasDatabaseName("ix_usuario_email_cliente")
            .IsUnique()
            .HasFilter("tipo_usuario = 'Cliente'");

        builder.HasIndex(u => u.Cpf)
            .HasDatabaseName("ix_usuario_cpf_tecnico")
            .IsUnique()
            .HasFilter("tipo_usuario = 'Tecnico'");

        builder.HasIndex(u => u.Email)
            .HasDatabaseName("ix_usuario_email_tecnico")
            .IsUnique()
            .HasFilter("tipo_usuario = 'Tecnico'");
    }
}

/// <summary>Cliente: codigo unico (RNF0023) e composicao com imoveis e formas de pagamento.</summary>
public sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.Property(c => c.CodigoCliente).HasMaxLength(20).IsRequired();

        builder.HasIndex(c => c.CodigoCliente)
            .HasDatabaseName("ix_usuario_codigo_cliente")
            .IsUnique()
            .HasFilter("tipo_usuario = 'Cliente'");

        builder.Ignore(c => c.Imoveis);
        builder.Ignore(c => c.FormasPagamento);

        // Composicao: excluir o cliente exclui os imoveis (RN0012).
        builder.HasMany<Imovel>("_imoveis")
            .WithOne()
            .HasForeignKey(i => i.ClienteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_imoveis").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany<FormaPagamento>("_formasPagamento")
            .WithOne()
            .HasForeignKey(f => f.ClienteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_formasPagamento").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Tecnico: codigo unico (RNF0031), especialidades, areas e documentos.</summary>
public sealed class TecnicoConfiguration : IEntityTypeConfiguration<Tecnico>
{
    public void Configure(EntityTypeBuilder<Tecnico> builder)
    {
        builder.Property(t => t.CodigoTecnico).HasMaxLength(20).IsRequired();

        builder.HasIndex(t => t.CodigoTecnico)
            .HasDatabaseName("ix_usuario_codigo_tecnico")
            .IsUnique()
            .HasFilter("tipo_usuario = 'Tecnico'");

        builder.Ignore(t => t.Especialidades);
        builder.Ignore(t => t.AreasAtendimento);
        builder.Ignore(t => t.Documentos);

        // RF0026: especialidades sao categorias de servico (muitos para muitos).
        builder.HasMany<CategoriaServico>("_especialidades")
            .WithMany()
            .UsingEntity(juncao => juncao.ToTable("tecnico_especialidade"));

        builder.Navigation("_especialidades").UsePropertyAccessMode(PropertyAccessMode.Field);

        // RF0027: areas de atendimento pertencem ao tecnico.
        builder.HasMany<AreaAtendimento>("_areasAtendimento")
            .WithOne()
            .HasForeignKey(a => a.TecnicoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_areasAtendimento").UsePropertyAccessMode(PropertyAccessMode.Field);

        // RNF0032: documentos de certificacao do tecnico.
        builder.HasMany<Anexo>("_documentos")
            .WithOne()
            .HasForeignKey(a => a.TecnicoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_documentos").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Administrador: matricula.</summary>
public sealed class AdministradorConfiguration : IEntityTypeConfiguration<Administrador>
{
    public void Configure(EntityTypeBuilder<Administrador> builder)
    {
        builder.Property(a => a.Matricula).HasMaxLength(20).IsRequired();

        builder.HasIndex(a => a.Matricula)
            .HasDatabaseName("ix_usuario_matricula")
            .IsUnique()
            .HasFilter("tipo_usuario = 'Administrador'");
    }
}

/// <summary>Imovel do cliente (RN0012).</summary>
public sealed class ImovelConfiguration : IEntityTypeConfiguration<Imovel>
{
    public void Configure(EntityTypeBuilder<Imovel> builder)
    {
        builder.ToTable("imovel", tabela => tabela.HasCheckConstraint(
            "ck_imovel_tipo",
            "tipo_imovel BETWEEN 1 AND 3"));

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Apelido).HasMaxLength(60).IsRequired();
        builder.Property(i => i.TipoImovel).HasConversion<int>().IsRequired();
        builder.Property(i => i.Logradouro).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Numero).HasMaxLength(20).IsRequired();
        builder.Property(i => i.Complemento).HasMaxLength(120);
        builder.Property(i => i.Bairro).HasMaxLength(120).IsRequired();
        builder.Property(i => i.Cep).HasMaxLength(8).IsRequired();
        builder.Property(i => i.Cidade).HasMaxLength(120).IsRequired();
        builder.Property(i => i.Estado).HasMaxLength(2).IsRequired();

        builder.HasIndex(i => i.ClienteId).HasDatabaseName("ix_imovel_cliente_id");
        builder.HasIndex(i => i.Cep).HasDatabaseName("ix_imovel_cep");
    }
}
