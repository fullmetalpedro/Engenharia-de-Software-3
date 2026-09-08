using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((contexto, configuracao) =>
    configuracao.ReadFrom.Configuration(contexto.Configuration));

var app = builder.Build();

// Fase 1: apenas o health check. As demais fases acrescentam persistencia,
// seguranca e os endpoints por caso de uso.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

/// <summary>
/// Ponto de extensao usado pelos testes de integracao (WebApplicationFactory).
/// </summary>
public partial class Program;
