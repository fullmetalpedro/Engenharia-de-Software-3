using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace ChamadosManutencao.IntegrationTests.Comum;

/// <summary>Acucar sintatico para as chamadas HTTP dos testes.</summary>
public static class ExtensoesHttp
{
    public static Task<HttpResponseMessage> PostarAsync(
        this HttpClient cliente,
        string rota,
        object? corpo = null) =>
        cliente.PostAsJsonAsync(rota, corpo ?? new { }, AmbienteDeIntegracao.Json);

    public static Task<HttpResponseMessage> ColocarAsync(
        this HttpClient cliente,
        string rota,
        object corpo) =>
        cliente.PutAsJsonAsync(rota, corpo, AmbienteDeIntegracao.Json);

    public static Task<HttpResponseMessage> AlterarAsync(
        this HttpClient cliente,
        string rota,
        object? corpo = null) =>
        cliente.PatchAsJsonAsync(rota, corpo ?? new { }, AmbienteDeIntegracao.Json);

    /// <summary>Le o corpo da resposta exigindo que o status seja de sucesso.</summary>
    public static async Task<T> LerAsync<T>(this HttpResponseMessage resposta)
    {
        var corpo = await resposta.Content.ReadAsStringAsync();

        resposta.IsSuccessStatusCode.ShouldBeTrue(
            $"{(int)resposta.StatusCode} {resposta.StatusCode} em {resposta.RequestMessage?.RequestUri}: {corpo}");

        return JsonSerializer.Deserialize<T>(corpo, AmbienteDeIntegracao.Json)
            ?? throw new InvalidOperationException($"Corpo vazio em {resposta.RequestMessage?.RequestUri}.");
    }

    /// <summary>Confere o status e devolve a resposta, com o corpo na mensagem da falha.</summary>
    public static async Task<HttpResponseMessage> DeveTerStatusAsync(
        this HttpResponseMessage resposta,
        HttpStatusCode esperado)
    {
        var corpo = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.ShouldBe(
            esperado,
            $"{resposta.RequestMessage?.Method} {resposta.RequestMessage?.RequestUri} devolveu: {corpo}");

        return resposta;
    }

    /// <summary>Le o identificador do requisito violado, gravado no ProblemDetails.</summary>
    public static async Task<string?> RequisitoVioladoAsync(this HttpResponseMessage resposta)
    {
        using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        return documento.RootElement.TryGetProperty("requisito", out var requisito)
            ? requisito.GetString()
            : null;
    }

    /// <summary>Le o corpo bruto, util para conferir que um dado sensivel nao vazou.</summary>
    public static Task<string> TextoAsync(this HttpResponseMessage resposta) =>
        resposta.Content.ReadAsStringAsync();
}
