using System.Text;
using System.Text.Json.Serialization;
using ChamadosManutencao.Api;
using ChamadosManutencao.Api.Endpoints;
using ChamadosManutencao.Api.Middlewares;
using ChamadosManutencao.Api.Seguranca;
using ChamadosManutencao.Application;
using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Infrastructure;
using ChamadosManutencao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((contexto, configuracao) =>
    configuracao.ReadFrom.Configuration(contexto.Configuration));

// ---------- Configuracao ----------
var opcoesDeJwt = new OpcoesDeJwt();
builder.Configuration.GetSection(OpcoesDeJwt.Secao).Bind(opcoesDeJwt);

if (string.IsNullOrWhiteSpace(opcoesDeJwt.ChaveSecreta) || opcoesDeJwt.ChaveSecreta.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:ChaveSecreta deve ser configurada com ao menos 32 caracteres.");
}

builder.Services.AddSingleton(opcoesDeJwt);

// ---------- Camadas ----------
builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AdicionarAplicacao();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioAtual, UsuarioAtual>();
builder.Services.AddScoped<IGeradorDeToken, GeradorDeTokenJwt>();

builder.Services.ConfigureHttpJsonOptions(opcoes =>
    opcoes.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ---------- Seguranca ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opcoes =>
    {
        opcoes.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = opcoesDeJwt.Emissor,
            ValidAudience = opcoesDeJwt.Audiencia,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(opcoesDeJwt.ChaveSecreta)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Politicas.Cliente, politica => politica.RequireRole(Politicas.Cliente))
    .AddPolicy(Politicas.Tecnico, politica => politica.RequireRole(Politicas.Tecnico))
    .AddPolicy(Politicas.Administrador, politica => politica.RequireRole(Politicas.Administrador))
    .AddPolicy(
        Politicas.ClienteOuAdministrador,
        politica => politica.RequireRole(Politicas.Cliente, Politicas.Administrador))
    .AddPolicy(
        Politicas.TecnicoOuAdministrador,
        politica => politica.RequireRole(Politicas.Tecnico, Politicas.Administrador));

// ---------- Documentacao ----------
builder.Services.AddOpenApi(opcoes => opcoes.AddDocumentTransformer((documento, _, _) =>
{
    documento.Info = new OpenApiInfo
    {
        Title = "Sistema de Abertura de Chamados de Manutencao",
        Version = "v1",
        Description = "API do sistema de chamados de manutencao residencial. "
            + "Endpoints agrupados por caso de uso (UC01 a UC12)."
    };

    return Task.CompletedTask;
}));

var app = builder.Build();

// ---------- Comandos de linha ----------
// dotnet run --project src/ChamadosManutencao.Api -- seed | criar-admin
if (args.Length > 0)
{
    await ComandosDeLinha.ExecutarAsync(app, args);
    return;
}

// ---------- Pipeline ----------
app.UseMiddleware<MiddlewareDeExcecao>();
app.UseMiddleware<MiddlewareDeTempoDeResposta>();
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference(opcoes => opcoes
    .WithTitle("Chamados de Manutencao")
    .WithTheme(ScalarTheme.BluePlanet));

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithTags("Infraestrutura")
    .WithSummary("Health check da API.")
    .AllowAnonymous();

app.MapearEndpoints();

// Em Development as migrations pendentes sao aplicadas no startup; fora dele, nunca.
if (app.Environment.IsDevelopment())
{
    using var escopo = app.Services.CreateScope();
    var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
    await contexto.Database.MigrateAsync();
}

app.Run();

/// <summary>
/// Ponto de extensao usado pelos testes de integracao (WebApplicationFactory).
/// </summary>
public partial class Program;
