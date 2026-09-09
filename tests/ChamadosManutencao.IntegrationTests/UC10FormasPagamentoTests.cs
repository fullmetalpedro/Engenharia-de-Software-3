using System.Net;
using ChamadosManutencao.Application.UC10FormasPagamento;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Financeiro;
using ChamadosManutencao.IntegrationTests.Comum;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// UC10 Gerenciar Formas de Pagamento.
/// Requisitos: RF0081, RNF0061.
/// </summary>
[Trait("Escopo", "UC10")]
public class UC10FormasPagamentoTests : TesteDeIntegracao
{
    private const string NumeroDeCartao = "4111111111111111";

    public UC10FormasPagamentoTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    /// <summary>RF0081 e RNF0061: guarda apenas bandeira, quatro ultimos digitos e validade.</summary>
    [Fact]
    public async Task Cartao_e_tokenizado_e_o_numero_completo_nao_volta_na_resposta()
    {
        var cliente = await CadastrarClienteAsync();

        var resposta = await cliente.Http.PostarAsync(
            $"/api/v1/clientes/{cliente.Id}/formas-pagamento",
            CartaoPadrao());

        var corpo = await resposta.TextoAsync();
        await resposta.DeveTerStatusAsync(HttpStatusCode.Created);

        corpo.ShouldNotContain(NumeroDeCartao);
        corpo.ShouldNotContain("codigoSeguranca", Case.Insensitive);
        corpo.ShouldNotContain("tokenOperadora", Case.Insensitive);

        var forma = await resposta.LerAsync<FormaPagamentoDto>();
        forma.UltimosQuatroDigitos.ShouldBe("1111");
        forma.Bandeira.ShouldNotBeNullOrWhiteSpace();
        forma.Principal.ShouldBeTrue();
    }

    /// <summary>RNF0061: o numero completo tambem nao pode ficar no banco.</summary>
    [Fact]
    public async Task Numero_completo_do_cartao_nao_e_persistido()
    {
        var cliente = await CadastrarClienteAsync();

        await (await cliente.Http.PostarAsync(
                $"/api/v1/clientes/{cliente.Id}/formas-pagamento",
                CartaoPadrao()))
            .DeveTerStatusAsync(HttpStatusCode.Created);

        var encontrou = await Ambiente.ComContextoAsync(contexto =>
            contexto.FormasDePagamento
                .OfType<CartaoCredito>()
                .AnyAsync(c => c.TokenOperadora.Contains(NumeroDeCartao)
                    || c.UltimosQuatroDigitos == NumeroDeCartao));

        encontrou.ShouldBeFalse();
    }

    /// <summary>RF0081: PIX e boleto tambem sao formas de pagamento validas.</summary>
    [Fact]
    public async Task Cliente_cadastra_pix_e_boleto()
    {
        var cliente = await CadastrarClienteAsync();

        var pix = await (await cliente.Http.PostarAsync(
                $"/api/v1/clientes/{cliente.Id}/formas-pagamento",
                new CadastrarFormaPagamentoCommand(
                    "Pix",
                    "PIX principal",
                    ChavePix: cliente.Email,
                    TipoChave: TipoChavePix.Email)))
            .LerAsync<FormaPagamentoDto>();

        pix.Tipo.ShouldBe("Pix");

        var boleto = await (await cliente.Http.PostarAsync(
                $"/api/v1/clientes/{cliente.Id}/formas-pagamento",
                new CadastrarFormaPagamentoCommand(
                    "Boleto",
                    "Boleto",
                    NomeSacado: cliente.Dados.NomeCompleto,
                    EmailEnvio: cliente.Email)))
            .LerAsync<FormaPagamentoDto>();

        boleto.Tipo.ShouldBe("Boleto");
    }

    /// <summary>RF0081: exatamente uma forma principal por vez.</summary>
    [Fact]
    public async Task Definir_principal_desmarca_a_anterior()
    {
        var cliente = await CadastrarClienteAsync();

        var primeira = await (await cliente.Http.PostarAsync(
                $"/api/v1/clientes/{cliente.Id}/formas-pagamento",
                CartaoPadrao()))
            .LerAsync<FormaPagamentoDto>();

        var segunda = await (await cliente.Http.PostarAsync(
                $"/api/v1/clientes/{cliente.Id}/formas-pagamento",
                new CadastrarFormaPagamentoCommand(
                    "Pix",
                    "PIX",
                    ChavePix: cliente.Email,
                    TipoChave: TipoChavePix.Email)))
            .LerAsync<FormaPagamentoDto>();

        await (await cliente.Http.AlterarAsync(
                $"/api/v1/clientes/{cliente.Id}/formas-pagamento/{segunda.Id}/principal"))
            .DeveTerStatusAsync(HttpStatusCode.OK);

        var formas = await ListarAsync(cliente);

        formas.Single(f => f.Id == segunda.Id).Principal.ShouldBeTrue();
        formas.Single(f => f.Id == primeira.Id).Principal.ShouldBeFalse();
    }

    /// <summary>RF0081: alteracao do apelido.</summary>
    [Fact]
    public async Task Alteracao_troca_o_apelido_da_forma()
    {
        var cliente = await CadastrarClienteAsync();

        var forma = await (await cliente.Http.PostarAsync(
                $"/api/v1/clientes/{cliente.Id}/formas-pagamento",
                CartaoPadrao()))
            .LerAsync<FormaPagamentoDto>();

        var alterada = await (await cliente.Http.ColocarAsync(
                $"/api/v1/clientes/{cliente.Id}/formas-pagamento/{forma.Id}",
                new AlterarFormaPagamentoCommand("Cartao da empresa")))
            .LerAsync<FormaPagamentoDto>();

        alterada.Apelido.ShouldBe("Cartao da empresa");
    }

    /// <summary>RF0081: a exclusao e logica, para preservar o historico de pagamentos.</summary>
    [Fact]
    public async Task Exclusao_inativa_a_forma_sem_apagar_o_registro()
    {
        var cliente = await CadastrarClienteAsync();

        var forma = await (await cliente.Http.PostarAsync(
                $"/api/v1/clientes/{cliente.Id}/formas-pagamento",
                CartaoPadrao()))
            .LerAsync<FormaPagamentoDto>();

        await (await cliente.Http.DeleteAsync(
                $"/api/v1/clientes/{cliente.Id}/formas-pagamento/{forma.Id}"))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);

        var continuaNoBanco = await Ambiente.ComContextoAsync(contexto =>
            contexto.FormasDePagamento.AnyAsync(f => f.Id == forma.Id && !f.Ativa));

        continuaNoBanco.ShouldBeTrue();
    }

    [Fact]
    public async Task Cliente_nao_mexe_na_forma_de_pagamento_de_outro_cliente()
    {
        var dono = await CadastrarClienteAsync();
        var intruso = await CadastrarClienteAsync();

        var resposta = await intruso.Http.PostarAsync(
            $"/api/v1/clientes/{dono.Id}/formas-pagamento",
            CartaoPadrao());

        resposta.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Tipo_de_forma_desconhecido_e_recusado()
    {
        var cliente = await CadastrarClienteAsync();

        var resposta = await cliente.Http.PostarAsync(
            $"/api/v1/clientes/{cliente.Id}/formas-pagamento",
            new CadastrarFormaPagamentoCommand("Cheque", "Cheque"));

        await resposta.DeveTerStatusAsync(HttpStatusCode.BadRequest);
    }

    private async Task<IReadOnlyCollection<FormaPagamentoDto>> ListarAsync(CenarioDeCliente cliente) =>
        await (await cliente.Http.GetAsync($"/api/v1/clientes/{cliente.Id}/formas-pagamento"))
            .LerAsync<IReadOnlyCollection<FormaPagamentoDto>>();

    private static CadastrarFormaPagamentoCommand CartaoPadrao() => new(
        "CartaoCredito",
        "Cartao principal",
        Principal: true,
        NumeroCartao: NumeroDeCartao,
        NomeTitular: "CLIENTE DE TESTE",
        Validade: "12/2030",
        CodigoSeguranca: "123");
}
