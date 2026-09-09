using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Infrastructure.Armazenamento;
using ChamadosManutencao.Infrastructure.Auditoria;
using ChamadosManutencao.Infrastructure.Jobs;
using ChamadosManutencao.Infrastructure.Notificacao;
using ChamadosManutencao.Infrastructure.Pagamentos;
using ChamadosManutencao.Infrastructure.Persistence;
using ChamadosManutencao.Infrastructure.Persistence.Repositories;
using ChamadosManutencao.Infrastructure.Servicos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChamadosManutencao.Infrastructure;

/// <summary>
/// Composicao dos servicos de infraestrutura. E o unico ponto em que a Api conhece a
/// Infrastructure.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AdicionarInfraestrutura(
        this IServiceCollection servicos,
        IConfiguration configuracao)
    {
        var cadeiaDeConexao = configuracao.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "A cadeia de conexao 'Postgres' nao foi configurada.");

        servicos.AddSingleton<IGeradorId, GeradorIdV7>();
        servicos.AddSingleton<IRelogio, RelogioDoSistema>();
        servicos.AddSingleton<IServicoDeHashDeSenha, ServicoDeHashBCrypt>();
        servicos.AddSingleton<IPoliticaDeSenha, PoliticaDeSenhaForte>();

        servicos.AddScoped<InterceptorDeAuditoria>();

        servicos.AddDbContext<AppDbContext>((provedor, opcoes) =>
        {
            opcoes.UseNpgsql(cadeiaDeConexao, npgsql =>
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(provedor.GetRequiredService<InterceptorDeAuditoria>());
        });

        servicos.AddScoped<IContextoDeLeitura, ContextoDeLeitura>();
        servicos.AddScoped<IUnitOfWork, UnidadeDeTrabalho>();
        servicos.AddScoped<IGeradorDeSequencias, GeradorDeSequencias>();

        servicos.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
        servicos.AddScoped<IClienteRepositorio, ClienteRepositorio>();
        servicos.AddScoped<ITecnicoRepositorio, TecnicoRepositorio>();
        servicos.AddScoped<ICatalogoRepositorio, CatalogoRepositorio>();
        servicos.AddScoped<IChamadoRepositorio, ChamadoRepositorio>();
        servicos.AddScoped<IAtendimentoRepositorio, AtendimentoRepositorio>();
        servicos.AddScoped<IAvaliacaoRepositorio, AvaliacaoRepositorio>();
        servicos.AddScoped<IFaturaRepositorio, FaturaRepositorio>();

        servicos.AddScoped<INotificador, NotificadorPorLog>();
        servicos.AddScoped<IDespachanteDeEventos, DespachanteDeEventos>();
        servicos.AddScoped<IGatewayPagamento, GatewayPagamentoSimulado>();
        servicos.AddSingleton<IArmazenamentoArquivos, ArmazenamentoEmVolume>();

        // O job fica desligado nos testes de integracao, que controlam o tempo por conta
        // propria; em qualquer outro ambiente o padrao e ligado.
        if (configuracao.GetSection("Jobs:Habilitados").Value?.ToLowerInvariant() != "false")
        {
            servicos.AddHostedService<ExpiracaoOrcamentoJob>();
        }

        return servicos;
    }
}
