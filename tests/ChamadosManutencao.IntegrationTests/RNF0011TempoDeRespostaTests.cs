using System.Diagnostics;
using System.Net;
using ChamadosManutencao.IntegrationTests.Comum;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// RNF0011: toda consulta de usuario responde em no maximo 1 segundo.
/// </summary>
[Trait("Escopo", "RNF0011")]
public class RNF0011TempoDeRespostaTests : TesteDeIntegracao
{
    private const int LimiteEmMilissegundos = 1000;

    public RNF0011TempoDeRespostaTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    public static TheoryData<string> ConsultasDoAdministrador() =>
    [
        "/api/v1/clientes",
        "/api/v1/tecnicos",
        "/api/v1/categorias-servico",
        "/api/v1/tipos-servico",
        "/api/v1/chamados",
        "/api/v1/faturas"
    ];

    [Theory]
    [MemberData(nameof(ConsultasDoAdministrador))]
    public async Task Consulta_responde_dentro_do_limite_de_um_segundo(string rota)
    {
        var cenario = await MontarCenarioBasicoAsync();
        await AbrirChamadoAsync(cenario);

        var cronometro = Stopwatch.StartNew();
        var resposta = await Admin.GetAsync(rota);
        cronometro.Stop();

        await resposta.DeveTerStatusAsync(HttpStatusCode.OK);

        cronometro.ElapsedMilliseconds.ShouldBeLessThanOrEqualTo(LimiteEmMilissegundos);
    }
}
