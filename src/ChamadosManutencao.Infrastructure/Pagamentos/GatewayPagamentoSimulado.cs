using System.Security.Cryptography;
using System.Text;
using ChamadosManutencao.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace ChamadosManutencao.Infrastructure.Pagamentos;

/// <summary>
/// Gateway de pagamento simulado. Nao ha integracao real no escopo do projeto.
///
/// RNF0061: o numero completo do cartao entra apenas como argumento, e usado para derivar o
/// token e os quatro ultimos digitos, e nunca e persistido nem registrado em log.
///
/// Para permitir o teste do caminho negativo, valores terminados em .99 sao recusados.
/// </summary>
public sealed class GatewayPagamentoSimulado : IGatewayPagamento
{
    private readonly ILogger<GatewayPagamentoSimulado> _log;

    public GatewayPagamentoSimulado(ILogger<GatewayPagamentoSimulado> log) => _log = log;

    public Task<TokenDeCartao> TokenizarCartaoAsync(
        DadosDeCartao dados,
        CancellationToken cancellationToken = default)
    {
        var digitos = new string(dados.NumeroCompleto.Where(char.IsDigit).ToArray());

        if (digitos.Length is < 13 or > 19)
        {
            throw new ArgumentException("Numero de cartao invalido.", nameof(dados));
        }

        // Token deterministico: o mesmo cartao sempre gera o mesmo token, o que evita
        // duplicidade no cadastro sem guardar o numero.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{digitos}|{dados.Validade}"));
        var token = $"tok_{Convert.ToHexString(hash)[..32].ToLowerInvariant()}";

        var resultado = new TokenDeCartao(
            token,
            IdentificarBandeira(digitos),
            digitos[^4..],
            dados.Validade);

        _log.LogInformation(
            "Cartao tokenizado. Bandeira {Bandeira}, final {Final}.",
            resultado.Bandeira,
            resultado.UltimosQuatroDigitos);

        return Task.FromResult(resultado);
    }

    public Task<RetornoDeAutorizacao> AutorizarTransacaoAsync(
        string token,
        decimal valor,
        CancellationToken cancellationToken = default)
    {
        var centavos = Math.Abs(decimal.Truncate((valor - decimal.Truncate(valor)) * 100));
        var recusada = centavos == 99m;

        var retorno = recusada
            ? new RetornoDeAutorizacao(
                false,
                $"TRX-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                "51",
                "Transacao recusada pela operadora: saldo insuficiente.")
            : new RetornoDeAutorizacao(
                true,
                $"TRX-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                "00",
                "Transacao aprovada.");

        _log.LogInformation(
            "Autorizacao simulada para o token {Token}: {Codigo} - {Mensagem}.",
            token[..Math.Min(12, token.Length)],
            retorno.CodigoDeRetorno,
            retorno.Mensagem);

        return Task.FromResult(retorno);
    }

    private static string IdentificarBandeira(string digitos) => digitos[0] switch
    {
        '4' => "Visa",
        '5' => "Mastercard",
        '3' => "American Express",
        '6' => "Elo",
        _ => "Outra"
    };
}
