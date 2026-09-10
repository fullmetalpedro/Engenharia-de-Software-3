using System.Net;
using ChamadosManutencao.IntegrationTests.Comum;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// Matriz de autorizacao: 401 sem token e 403 com o papel errado, endpoint restrito por
/// endpoint restrito (secao 9 da arquitetura).
/// </summary>
[Trait("Escopo", "Autorizacao")]
public class AutorizacaoTests : TesteDeIntegracao
{
    public AutorizacaoTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    public static TheoryData<string, string> RotasProtegidas() => new()
    {
        { "GET", "/api/v1/clientes" },
        { "GET", "/api/v1/tecnicos" },
        { "GET", "/api/v1/categorias-servico" },
        { "GET", "/api/v1/tipos-servico" },
        { "GET", "/api/v1/chamados" },
        { "GET", "/api/v1/chamados/meus" },
        { "GET", "/api/v1/faturas" },
        { "GET", "/api/v1/faturas/minhas" },
        { "POST", "/api/v1/tecnicos" },
        { "POST", "/api/v1/categorias-servico" },
        { "POST", "/api/v1/chamados" }
    };

    /// <summary>Sem token, todo endpoint protegido devolve 401.</summary>
    [Theory]
    [MemberData(nameof(RotasProtegidas))]
    public async Task Sem_token_o_endpoint_protegido_devolve_401(string metodo, string rota)
    {
        var resposta = await Anonimo.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), rota));

        await resposta.DeveTerStatusAsync(HttpStatusCode.Unauthorized);
    }

    public static TheoryData<string, string> RotasSoDeAdministrador() => new()
    {
        { "GET", "/api/v1/clientes" },
        { "GET", "/api/v1/tecnicos" },
        { "GET", "/api/v1/chamados" },
        { "GET", "/api/v1/faturas" },
        { "POST", "/api/v1/tecnicos" },
        { "POST", "/api/v1/categorias-servico" }
    };

    /// <summary>Com token de cliente, o endpoint exclusivo do administrador devolve 403.</summary>
    [Theory]
    [MemberData(nameof(RotasSoDeAdministrador))]
    public async Task Cliente_nao_alcanca_endpoint_de_administrador(string metodo, string rota)
    {
        var cliente = await CadastrarClienteAsync();

        var resposta = await cliente.Http.SendAsync(
            new HttpRequestMessage(new HttpMethod(metodo), rota));

        await resposta.DeveTerStatusAsync(HttpStatusCode.Forbidden);
    }

    /// <summary>O tecnico nao abre chamado nem consulta os chamados de todos os clientes.</summary>
    [Fact]
    public async Task Tecnico_nao_alcanca_endpoint_de_cliente_nem_de_administrador()
    {
        var categoria = await CriarCategoriaAsync();
        var tecnico = await CriarTecnicoAsync(categoria.Id);

        await (await tecnico.Http.PostarAsync("/api/v1/chamados"))
            .DeveTerStatusAsync(HttpStatusCode.Forbidden);

        await (await tecnico.Http.GetAsync("/api/v1/chamados/meus"))
            .DeveTerStatusAsync(HttpStatusCode.Forbidden);

        await (await tecnico.Http.GetAsync("/api/v1/clientes"))
            .DeveTerStatusAsync(HttpStatusCode.Forbidden);
    }

    /// <summary>O administrador nao executa o atendimento no lugar do tecnico.</summary>
    [Fact]
    public async Task Administrador_nao_inicia_atendimento()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _) = await AgendarAsync(cenario);

        await (await Admin.PostarAsync($"/api/v1/chamados/{chamado.Id}/atendimento"))
            .DeveTerStatusAsync(HttpStatusCode.Forbidden);
    }

    /// <summary>O cliente nao propoe agendamento: a proposta e do tecnico ou do administrador.</summary>
    [Fact]
    public async Task Cliente_nao_propoe_agendamento()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirEAtribuirAsync(cenario);

        var resposta = await cenario.Cliente.Http.PostarAsync(
            $"/api/v1/chamados/{chamado.Id}/agendamentos",
            new Application.UC06Agendamento.AgendarCommand(DateTimeOffset.UtcNow.AddDays(1)));

        await resposta.DeveTerStatusAsync(HttpStatusCode.Forbidden);
    }

    /// <summary>Token invalido nao passa pela validacao de assinatura.</summary>
    [Fact]
    public async Task Token_adulterado_devolve_401()
    {
        var cliente = Ambiente.CreateClient();
        cliente.DefaultRequestHeaders.Add("Authorization", "Bearer token.invalido.aqui");

        var resposta = await cliente.GetAsync("/api/v1/chamados/meus");

        await resposta.DeveTerStatusAsync(HttpStatusCode.Unauthorized);
    }

    /// <summary>Login e cadastro de cliente continuam anonimos.</summary>
    [Fact]
    public async Task Rotas_anonimas_continuam_abertas()
    {
        // O cadastro de cliente responde sem token (o corpo invalido para em 400, nao em 401).
        var cadastro = await Anonimo.PostarAsync("/api/v1/clientes", new { });
        cadastro.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var login = await Anonimo.PostarAsync(
            "/api/v1/auth/login",
            new Application.Autenticacao.LoginCommand("ninguem@exemplo.com", "Senha@123"));
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
