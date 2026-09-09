using System.Text.Json;
using System.Text.Json.Serialization;
using ChamadosManutencao.Infrastructure.Jobs;
using ChamadosManutencao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace ChamadosManutencao.IntegrationTests.Comum;

/// <summary>
/// Ambiente compartilhado por toda a suite: um PostgreSQL descartavel em container e a API
/// hospedada em memoria contra ele. O container sobe uma unica vez; cada classe de teste
/// recebe a base limpa (secao 9 da arquitetura).
/// </summary>
public sealed class AmbienteDeIntegracao : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("chamados_testes")
        .WithUsername("chamados")
        .WithPassword("chamados_testes")
        .Build();

    /// <summary>Opcoes de serializacao identicas as da API (enum como texto).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var escopo = Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        await contexto.Database.MigrateAsync();
    }

    // Implementacao explicita: WebApplicationFactory ja expoe DisposeAsync devolvendo
    // ValueTask, e a assinatura do IAsyncLifetime do xUnit v2 devolve Task.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Devolve a base ao estado logo apos as migrations: apaga o conteudo de todas as tabelas
    /// e reinicia as sequences, para que numero de chamado e codigo de cliente sejam
    /// previsiveis dentro de cada classe de teste.
    /// </summary>
    public async Task LimparBaseAsync()
    {
        using var escopo = Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        await contexto.Database.ExecuteSqlRawAsync(
            """
            DO $$
            DECLARE
                tabelas text;
            BEGIN
                SELECT string_agg(format('%I.%I', schemaname, tablename), ', ')
                INTO tabelas
                FROM pg_tables
                WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory';

                IF tabelas IS NOT NULL THEN
                    EXECUTE 'TRUNCATE TABLE ' || tabelas || ' RESTART IDENTITY CASCADE';
                END IF;
            END $$;
            """);

        foreach (var sequence in new[]
                 {
                     AppDbContext.SequenceNumeroChamado,
                     AppDbContext.SequenceNumeroFatura,
                     AppDbContext.SequenceCodigoCliente,
                     AppDbContext.SequenceCodigoTecnico
                 })
        {
            // O nome vem de constante do proprio AppDbContext, nao de entrada externa.
#pragma warning disable EF1002
            await contexto.Database.ExecuteSqlRawAsync($"ALTER SEQUENCE \"{sequence}\" RESTART WITH 1");
#pragma warning restore EF1002
        }
    }

    /// <summary>
    /// Dispara uma execucao do job, sem esperar o intervalo de 15 minutos. Os jobs ficam
    /// desligados no ambiente de teste (Jobs:Habilitados = false).
    /// </summary>
    public async Task<int> ExecutarJobAsync<TJob>()
        where TJob : JobPeriodico
    {
        using var escopo = Services.CreateScope();

        var job = ActivatorUtilities.CreateInstance<TJob>(escopo.ServiceProvider);

        return await job.ProcessarAsync(escopo.ServiceProvider, CancellationToken.None);
    }

    /// <summary>RN0043: expira orcamento vencido e cancela o chamado.</summary>
    public Task<int> ExecutarJobDeExpiracaoDeOrcamentoAsync() =>
        ExecutarJobAsync<ExpiracaoOrcamentoJob>();

    /// <summary>Executa um bloco com o AppDbContext, para montar cenario ou conferir efeito.</summary>
    public async Task ComContextoAsync(Func<AppDbContext, Task> acao)
    {
        using var escopo = Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        await acao(contexto);
    }

    /// <summary>Versao com retorno de <see cref="ComContextoAsync(Func{AppDbContext, Task})"/>.</summary>
    public async Task<T> ComContextoAsync<T>(Func<AppDbContext, Task<T>> acao)
    {
        using var escopo = Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        return await acao(contexto);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:ChaveSecreta", "chave-de-teste-com-pelo-menos-32-bytes-de-tamanho");
        builder.UseSetting("Jwt:Emissor", "ChamadosManutencao");
        builder.UseSetting("Jwt:Audiencia", "ChamadosManutencao.Api");
        builder.UseSetting("Jwt:MinutosDeValidade", "60");

        // Os jobs mexem em prazo e status por conta propria e tornariam o teste nao
        // deterministico. O comportamento deles e verificado chamando o job diretamente.
        builder.UseSetting("Jobs:Habilitados", "false");
    }
}

/// <summary>Coleção que compartilha o mesmo container entre todas as classes de teste.</summary>
[CollectionDefinition(Nome)]
public sealed class ColecaoDeIntegracao : ICollectionFixture<AmbienteDeIntegracao>
{
    public const string Nome = "integracao";
}
