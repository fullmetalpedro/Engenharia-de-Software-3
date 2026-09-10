using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Avaliacoes;
using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Financeiro;
using ChamadosManutencao.Domain.Pessoas;
using ChamadosManutencao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Api;

/// <summary>
/// Bootstrap executado fora do pipeline HTTP:
/// <c>dotnet run --project src/ChamadosManutencao.Api -- criar-admin</c>.
///
/// Os 78 requisitos pressupoem o ator administrador em 11 deles, mas nenhum descreve como
/// cadastra-lo. Este comando cria a primeira linha, sem a qual o sistema nao se levanta.
/// </summary>
internal static class ComandosDeLinha
{
    private static readonly string[] Comandos = ["criar-admin"];

    /// <summary>Diz se o primeiro argumento e um comando desta classe, e nao um argumento do host.</summary>
    public static bool EhComandoConhecido(string argumento) =>
        Comandos.Contains(argumento.Trim().ToLowerInvariant());

    public static async Task ExecutarAsync(WebApplication app, string[] args)
    {
        var comando = args[0].Trim().ToLowerInvariant();

        using var escopo = app.Services.CreateScope();
        var provedor = escopo.ServiceProvider;
        var registrador = provedor.GetRequiredService<ILoggerFactory>().CreateLogger("ComandosDeLinha");
        var contexto = provedor.GetRequiredService<AppDbContext>();

        await contexto.Database.MigrateAsync();

        switch (comando)
        {
            case "criar-admin":
                await CriarAdministradorInicialAsync(provedor, contexto, registrador);
                return;

            default:
                registrador.LogError(
                    "Comando '{Comando}' desconhecido. Comando valido: criar-admin.",
                    comando);
                Environment.ExitCode = 1;
                return;
        }
    }

    // ---------- criar-admin ----------

    /// <summary>
    /// Cria o primeiro administrador a partir das variaveis de ambiente ADMIN_INICIAL_*.
    /// Idempotente: se ja existe algum administrador, nao faz nada.
    /// </summary>
    private static async Task CriarAdministradorInicialAsync(
        IServiceProvider provedor,
        AppDbContext contexto,
        ILogger registrador)
    {
        if (await contexto.Administradores.AnyAsync())
        {
            registrador.LogInformation("Ja existe administrador cadastrado. Nada a fazer.");
            return;
        }

        var email = Environment.GetEnvironmentVariable("ADMIN_INICIAL_EMAIL");
        var senha = Environment.GetEnvironmentVariable("ADMIN_INICIAL_SENHA");
        var cpf = Environment.GetEnvironmentVariable("ADMIN_INICIAL_CPF");
        var nome = Environment.GetEnvironmentVariable("ADMIN_INICIAL_NOME");
        var matricula = Environment.GetEnvironmentVariable("ADMIN_INICIAL_MATRICULA");
        var telefone = Environment.GetEnvironmentVariable("ADMIN_INICIAL_TELEFONE") ?? "1130000000";

        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(senha)
            || string.IsNullOrWhiteSpace(cpf)
            || string.IsNullOrWhiteSpace(nome)
            || string.IsNullOrWhiteSpace(matricula))
        {
            registrador.LogError(
                "Defina ADMIN_INICIAL_EMAIL, ADMIN_INICIAL_SENHA, ADMIN_INICIAL_CPF, "
                + "ADMIN_INICIAL_NOME e ADMIN_INICIAL_MATRICULA antes de rodar 'criar-admin'.");
            Environment.ExitCode = 1;
            return;
        }

        var politica = provedor.GetRequiredService<IPoliticaDeSenha>();

        if (!politica.EhForte(senha, out var motivo))
        {
            registrador.LogError("ADMIN_INICIAL_SENHA rejeitada: {Motivo}", motivo);
            Environment.ExitCode = 1;
            return;
        }

        var hash = provedor.GetRequiredService<IServicoDeHashDeSenha>();
        var geradorId = provedor.GetRequiredService<IGeradorId>();

        var administrador = new Administrador(
            geradorId.NovoId(),
            matricula,
            nome,
            cpf,
            email,
            telefone,
            hash.GerarHash(senha));

        contexto.Administradores.Add(administrador);
        await contexto.SaveChangesAsync();

        registrador.LogInformation(
            "Administrador inicial criado: {Email} (matricula {Matricula}).",
            administrador.Email,
            administrador.Matricula);
    }
}
