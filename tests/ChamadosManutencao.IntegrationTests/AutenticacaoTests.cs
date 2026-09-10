using System.Net;
using ChamadosManutencao.Application.Autenticacao;
using ChamadosManutencao.IntegrationTests.Comum;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// Autenticacao e troca de senha.
/// Requisitos: RNF0021, RNF0022, RF0013, RF0023.
/// </summary>
[Trait("Escopo", "Autenticacao")]
public class AutenticacaoTests : TesteDeIntegracao
{
    public AutenticacaoTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    [Fact]
    public async Task Login_com_credenciais_validas_devolve_token_e_papel()
    {
        var cliente = await CadastrarClienteAsync();

        var resposta = await Anonimo.PostarAsync(
            "/api/v1/auth/login",
            new LoginCommand(cliente.Email, SenhaPadrao));

        var token = await resposta.LerAsync<TokenResponse>();

        token.AccessToken.ShouldNotBeNullOrWhiteSpace();
        token.Papel.ShouldBe("Cliente");
        token.ExpiraEm.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Login_com_senha_errada_devolve_401()
    {
        var cliente = await CadastrarClienteAsync();

        var resposta = await Anonimo.PostarAsync(
            "/api/v1/auth/login",
            new LoginCommand(cliente.Email, "Senha@Errada1"));

        await resposta.DeveTerStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_de_cliente_inativo_e_recusado()
    {
        var cliente = await CadastrarClienteAsync();

        await (await Admin.AlterarAsync($"/api/v1/clientes/{cliente.Id}/inativacao"))
            .DeveTerStatusAsync(HttpStatusCode.OK);

        var resposta = await Anonimo.PostarAsync(
            "/api/v1/auth/login",
            new LoginCommand(cliente.Email, SenhaPadrao));

        await resposta.DeveTerStatusAsync(HttpStatusCode.Unauthorized);
    }

    /// <summary>RNF0021: a politica de senha forte tambem vale para a troca.</summary>
    [Fact]
    public async Task Alterar_senha_recusa_senha_fraca()
    {
        var cliente = await CadastrarClienteAsync();

        var resposta = await cliente.Http.PostarAsync(
            "/api/v1/auth/alterar-senha",
            new AlterarSenhaCommand(SenhaPadrao, "fraca"));

        await resposta.DeveTerStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Alterar_senha_troca_a_credencial_usada_no_login()
    {
        var cliente = await CadastrarClienteAsync();
        const string novaSenha = "OutraSenha@2026";

        await (await cliente.Http.PostarAsync(
                "/api/v1/auth/alterar-senha",
                new AlterarSenhaCommand(SenhaPadrao, novaSenha)))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);

        await (await Anonimo.PostarAsync("/api/v1/auth/login", new LoginCommand(cliente.Email, SenhaPadrao)))
            .DeveTerStatusAsync(HttpStatusCode.Unauthorized);

        await (await Anonimo.PostarAsync("/api/v1/auth/login", new LoginCommand(cliente.Email, novaSenha)))
            .DeveTerStatusAsync(HttpStatusCode.OK);
    }

    /// <summary>RNF0022: o hash da senha nunca sai em resposta.</summary>
    [Fact]
    public async Task Nenhuma_resposta_expoe_a_senha_ou_o_hash()
    {
        var cliente = await CadastrarClienteAsync();

        var corpo = await (await Admin.GetAsync($"/api/v1/clientes/{cliente.Id}")).TextoAsync();

        corpo.ShouldNotContain("senha", Case.Insensitive);
        corpo.ShouldNotContain("$2a$");
    }
}
