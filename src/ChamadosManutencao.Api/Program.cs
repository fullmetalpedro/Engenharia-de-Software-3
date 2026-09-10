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
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Ajustes da maquina do desenvolvedor (por exemplo, outra porta do PostgreSQL quando a 5432
// ja esta ocupada). O arquivo nao e versionado; ver .gitignore e o README.
//
// A fonte entra logo depois dos appsettings e antes das variaveis de ambiente, para que o
// ambiente continue tendo a ultima palavra (e o compose e o CI sobreponham o arquivo local).
var ultimoJson = builder.Configuration.Sources
    .Select((fonte, indice) => (fonte, indice))
    .Where(par => par.fonte is Microsoft.Extensions.Configuration.Json.JsonConfigurationSource)
    .Select(par => par.indice)
    .DefaultIfEmpty(-1)
    .Max();

builder.Configuration.Sources.Insert(
    ultimoJson + 1,
    new Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
    {
        Path = "appsettings.Local.json",
        Optional = true,
        ReloadOnChange = true,
        FileProvider = builder.Environment.ContentRootFileProvider
    });

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

// A chave do appsettings esta no repositorio: quem a alcanca emite token de administrador.
// Ela serve para levantar o ambiente local e nada alem disso.
const string ChaveDeDesenvolvimento = "chave-de-desenvolvimento-com-pelo-menos-32-bytes-troque-isto";

if (!builder.Environment.IsDevelopment() && opcoesDeJwt.ChaveSecreta == ChaveDeDesenvolvimento)
{
    throw new InvalidOperationException(
        "Jwt:ChaveSecreta ainda e a chave de desenvolvimento versionada. Defina uma propria "
        + $"pelo ambiente antes de subir em {builder.Environment.EnvironmentName}.");
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
        politica => politica.RequireRole(Politicas.Tecnico, Politicas.Administrador))
    .AddPolicy(
        Politicas.ClienteOuTecnico,
        politica => politica.RequireRole(Politicas.Cliente, Politicas.Tecnico));

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
// dotnet run --project src/ChamadosManutencao.Api -- criar-admin
//
// Nada de await no corpo do Program: WebApplicationFactory (usada pelos testes de
// integracao) nao consegue capturar o host quando o ponto de entrada e assincrono.
//
// A comparacao e por nome de comando conhecido, nao por args.Length: o host de teste passa
// os proprios argumentos de linha de comando e derrubaria a API antes do app.Run().
if (args.Length > 0 && ComandosDeLinha.EhComandoConhecido(args[0]))
{
    ComandosDeLinha.ExecutarAsync(app, args).GetAwaiter().GetResult();
    return;
}

// ---------- Pipeline ----------
app.UseMiddleware<MiddlewareDeExcecao>();
app.UseMiddleware<MiddlewareDeTempoDeResposta>();
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

// A documentacao e a interface do sistema enquanto nao ha tela, mas ela desenha o mapa
// completo das rotas e dos schemas para quem chega sem token: fora de producao apenas.
if (!app.Environment.IsProduction())
{
    app.MapOpenApi();

    app.UseSwaggerUI(opcoes =>
    {
        opcoes.SwaggerEndpoint("/openapi/v1.json", "Chamados de Manutencao v1");
        opcoes.DocumentTitle = "Chamados de Manutencao";
    });
}

app.MapearEndpoints();

// Em Development as migrations pendentes sao aplicadas no startup; fora dele, nunca.
if (app.Environment.IsDevelopment())
{
    using var escopo = app.Services.CreateScope();
    var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
    contexto.Database.Migrate();
}

app.Run();

/// <summary>
/// Ponto de extensao usado pelos testes de integracao (WebApplicationFactory).
/// </summary>
public partial class Program;
