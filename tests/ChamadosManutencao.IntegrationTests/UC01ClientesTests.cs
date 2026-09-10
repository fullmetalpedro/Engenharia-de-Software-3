using System.Net;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC01Clientes;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.IntegrationTests.Comum;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// UC01 Gerenciar Cadastro de Clientes.
/// Requisitos: RF0011, RF0012, RF0013, RF0014, RF0015, RF0016, RN0011, RN0012, RNF0021,
/// RNF0022, RNF0023.
/// </summary>
[Trait("Escopo", "UC01")]
public class UC01ClientesTests : TesteDeIntegracao
{
    public UC01ClientesTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    /// <summary>RF0011, RN0011, RNF0023.</summary>
    [Fact]
    public async Task Cadastro_cria_cliente_com_imovel_e_codigo_unico()
    {
        var cliente = await CadastrarClienteAsync();

        cliente.Dados.CodigoCliente.ShouldStartWith("CLI-");
        cliente.Dados.Ativo.ShouldBeTrue();
        cliente.Dados.Imoveis.Count.ShouldBe(1);

        var segundo = await CadastrarClienteAsync();
        segundo.Dados.CodigoCliente.ShouldNotBe(cliente.Dados.CodigoCliente);
    }

    /// <summary>RN0011: o cadastro exige ao menos um imovel.</summary>
    [Fact]
    public async Task Cadastro_sem_imovel_e_recusado()
    {
        var comando = new CadastrarClienteCommand(
            "Sem Imovel",
            Cpf(),
            $"semimovel{Proximo()}@exemplo.com",
            "11940000000",
            SenhaPadrao,
            []);

        var resposta = await Anonimo.PostarAsync("/api/v1/clientes", comando);

        await resposta.DeveTerStatusAsync(HttpStatusCode.BadRequest);
    }

    /// <summary>RNF0021: senha com menos de 8 caracteres, sem maiuscula ou sem especial.</summary>
    [Theory]
    [InlineData("curta1!")]
    [InlineData("semmaiuscula1!")]
    [InlineData("SemEspecial123")]
    public async Task Cadastro_recusa_senha_fora_da_politica(string senha)
    {
        var comando = new CadastrarClienteCommand(
            "Senha Fraca",
            Cpf(),
            $"fraca{Proximo()}@exemplo.com",
            "11940000000",
            senha,
            [ImovelPadrao()]);

        var resposta = await Anonimo.PostarAsync("/api/v1/clientes", comando);

        await resposta.DeveTerStatusAsync(HttpStatusCode.BadRequest);
    }

    /// <summary>RN0012: CEP invalido reprova a composicao do imovel.</summary>
    [Fact]
    public async Task Cadastro_recusa_imovel_com_cep_invalido()
    {
        var comando = new CadastrarClienteCommand(
            "Cep Invalido",
            Cpf(),
            $"cep{Proximo()}@exemplo.com",
            "11940000000",
            SenhaPadrao,
            [ImovelPadrao() with { Cep = "123" }]);

        var resposta = await Anonimo.PostarAsync("/api/v1/clientes", comando);

        await resposta.DeveTerStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cadastro_recusa_email_ja_usado()
    {
        var cliente = await CadastrarClienteAsync();

        var comando = new CadastrarClienteCommand(
            "Repetido",
            Cpf(),
            cliente.Email,
            "11940000000",
            SenhaPadrao,
            [ImovelPadrao()]);

        var resposta = await Anonimo.PostarAsync("/api/v1/clientes", comando);

        resposta.StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    /// <summary>RF0015: filtros combinaveis ou isolados.</summary>
    [Fact]
    public async Task Consulta_filtra_por_nome_email_e_situacao()
    {
        var cliente = await CadastrarClienteAsync();
        await CadastrarClienteAsync();

        var porEmail = await (await Admin.GetAsync($"/api/v1/clientes?email={cliente.Email}"))
            .LerAsync<IReadOnlyCollection<ClienteResumoDto>>();

        porEmail.Count.ShouldBe(1);
        porEmail.Single().Id.ShouldBe(cliente.Id);

        var porNome = await (await Admin.GetAsync("/api/v1/clientes?nome=Cliente"))
            .LerAsync<IReadOnlyCollection<ClienteResumoDto>>();

        porNome.Count.ShouldBe(2);

        var combinado = await (await Admin.GetAsync(
                $"/api/v1/clientes?nome=Cliente&codigo={cliente.Dados.CodigoCliente}&ativo=true"))
            .LerAsync<IReadOnlyCollection<ClienteResumoDto>>();

        combinado.Count.ShouldBe(1);
    }

    /// <summary>RF0012.</summary>
    [Fact]
    public async Task Alteracao_atualiza_nome_email_e_telefone()
    {
        var cliente = await CadastrarClienteAsync();

        var alterado = await (await cliente.Http.ColocarAsync(
                $"/api/v1/clientes/{cliente.Id}",
                new AlterarClienteCommand("Nome Alterado", "alterado@exemplo.com", "11955555555")))
            .LerAsync<ClienteDetalheDto>();

        alterado.NomeCompleto.ShouldBe("Nome Alterado");
        alterado.Email.ShouldBe("alterado@exemplo.com");
        alterado.Telefone.ShouldBe("11955555555");
    }

    /// <summary>RF0013 e RF0014.</summary>
    [Fact]
    public async Task Inativacao_e_reativacao_alternam_a_situacao()
    {
        var cliente = await CadastrarClienteAsync();

        var inativo = await (await Admin.AlterarAsync($"/api/v1/clientes/{cliente.Id}/inativacao"))
            .LerAsync<ClienteDetalheDto>();
        inativo.Ativo.ShouldBeFalse();

        var ativo = await (await Admin.AlterarAsync($"/api/v1/clientes/{cliente.Id}/ativacao"))
            .LerAsync<ClienteDetalheDto>();
        ativo.Ativo.ShouldBeTrue();
    }

    /// <summary>RF0016.</summary>
    [Fact]
    public async Task Cliente_adiciona_altera_e_lista_imoveis()
    {
        var cliente = await CadastrarClienteAsync();

        var novo = await (await cliente.Http.PostarAsync(
                $"/api/v1/clientes/{cliente.Id}/imoveis",
                ImovelPadrao() with { Apelido = "Casa da praia", TipoImovel = TipoImovel.Casa }))
            .LerAsync<ImovelDto>();

        novo.Apelido.ShouldBe("Casa da praia");

        var alterado = await (await cliente.Http.ColocarAsync(
                $"/api/v1/clientes/{cliente.Id}/imoveis/{novo.Id}",
                ImovelPadrao() with { Apelido = "Casa da serra", TipoImovel = TipoImovel.Casa }))
            .LerAsync<ImovelDto>();

        alterado.Apelido.ShouldBe("Casa da serra");

        var lista = await (await cliente.Http.GetAsync($"/api/v1/clientes/{cliente.Id}/imoveis"))
            .LerAsync<IReadOnlyCollection<ImovelDto>>();

        lista.Count.ShouldBe(2);
    }

    /// <summary>RN0011: remover o ultimo imovel deixaria o cliente sem imovel.</summary>
    [Fact]
    public async Task Remocao_do_unico_imovel_e_bloqueada()
    {
        var cliente = await CadastrarClienteAsync();

        var resposta = await cliente.Http.DeleteAsync(
            $"/api/v1/clientes/{cliente.Id}/imoveis/{cliente.ImovelId}");

        await resposta.DeveTerStatusAsync(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Remocao_funciona_quando_sobra_outro_imovel()
    {
        var cliente = await CadastrarClienteAsync();

        await (await cliente.Http.PostarAsync(
                $"/api/v1/clientes/{cliente.Id}/imoveis",
                ImovelPadrao() with { Apelido = "Segundo imovel" }))
            .DeveTerStatusAsync(HttpStatusCode.Created);

        await (await cliente.Http.DeleteAsync($"/api/v1/clientes/{cliente.Id}/imoveis/{cliente.ImovelId}"))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Consulta_de_cliente_inexistente_devolve_404()
    {
        var resposta = await Admin.GetAsync($"/api/v1/clientes/{Guid.NewGuid()}");

        await resposta.DeveTerStatusAsync(HttpStatusCode.NotFound);
    }

    private static ImovelCommand ImovelPadrao() => new(
        "Imovel principal",
        TipoImovel.Apartamento,
        "Rua das Flores",
        "100",
        null,
        "Centro",
        "01043000",
        "Sao Paulo",
        "SP");
}
