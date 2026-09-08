using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChamadosManutencao.Infrastructure.Persistence;

/// <summary>
/// Fabrica usada apenas pelas ferramentas do EF Core (dotnet ef migrations / database update).
/// Em tempo de execucao o contexto vem da injecao de dependencia configurada na Api.
/// </summary>
public sealed class FabricaDeContextoEmTempoDeDesign : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cadeiaDeConexao = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=chamados;Username=chamados;Password=chamados_dev_senha";

        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(cadeiaDeConexao, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(opcoes);
    }
}
