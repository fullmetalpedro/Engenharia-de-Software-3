using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChamadosManutencao.Infrastructure.Persistence.Configurations;

/// <summary>
/// Concorrencia otimista pela coluna de sistema <c>xmin</c> do PostgreSQL, que o proprio banco
/// incrementa a cada atualizacao da linha. Evita uma coluna de versao no modelo de dominio.
/// </summary>
internal static class ConcorrenciaOtimista
{
    public static EntityTypeBuilder<T> UsarXmin<T>(this EntityTypeBuilder<T> builder)
        where T : class
    {
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        return builder;
    }
}
