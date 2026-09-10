using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Financeiro;

/// <summary>
/// Forma de pagamento do cliente. Heranca em tabela unica (TPH) com discriminador
/// tipo_forma_pagamento. Requisitos: RF0081, RNF0061.
/// </summary>
public abstract class FormaPagamento : Entidade
{
    protected FormaPagamento(Guid id, Guid clienteId, string apelido, bool principal)
        : base(id)
    {
        ClienteId = clienteId;
        Apelido = Garantir.TextoComTamanhoMaximo(apelido, 60, "apelido da forma de pagamento", "RF0081");
        Principal = principal;
        Ativa = true;
    }

    protected FormaPagamento()
    {
        Apelido = null!;
    }

    public Guid ClienteId { get; private set; }

    public string Apelido { get; private set; }

    public bool Principal { get; private set; }

    public bool Ativa { get; private set; }

    /// <summary>Descricao segura para exibicao, sem dado sensivel (RNF0061).</summary>
    public abstract string Descricao();

    /// <summary>Valida os dados especificos da forma de pagamento.</summary>
    public abstract bool Validar();

    internal void TornarPrincipal() => Principal = true;

    internal void DeixarDeSerPrincipal() => Principal = false;

    internal void Inativar() => Ativa = false;

    public void AlterarApelido(string apelido) =>
        Apelido = Garantir.TextoComTamanhoMaximo(apelido, 60, "apelido da forma de pagamento", "RF0081");
}

/// <summary>
/// Cartao de credito tokenizado. RNF0061: o numero completo nunca e persistido; ficam apenas
/// bandeira, quatro ultimos digitos, validade e o token devolvido pela operadora.
/// </summary>
public sealed class CartaoCredito : FormaPagamento
{
    public CartaoCredito(
        Guid id,
        Guid clienteId,
        string apelido,
        bool principal,
        string bandeira,
        string ultimosQuatroDigitos,
        string tokenOperadora,
        string validade,
        string nomeTitular)
        : base(id, clienteId, apelido, principal)
    {
        const string requisito = "RNF0061";

        Bandeira = Garantir.TextoComTamanhoMaximo(bandeira, 30, "bandeira do cartao", requisito);
        UltimosQuatroDigitos = ValidarUltimosQuatro(ultimosQuatroDigitos);
        TokenOperadora = Garantir.TextoComTamanhoMaximo(tokenOperadora, 200, "token da operadora", requisito);
        Validade = ValidarValidade(validade);
        NomeTitular = Garantir.TextoComTamanhoMaximo(nomeTitular, 200, "nome do titular", requisito);
    }

    private CartaoCredito()
    {
        Bandeira = null!;
        UltimosQuatroDigitos = null!;
        TokenOperadora = null!;
        Validade = null!;
        NomeTitular = null!;
    }

    public string Bandeira { get; private set; }

    public string UltimosQuatroDigitos { get; private set; }

    /// <summary>Token devolvido pela operadora. Substitui o numero do cartao (RNF0061).</summary>
    public string TokenOperadora { get; private set; }

    /// <summary>Validade no formato MM/AAAA.</summary>
    public string Validade { get; private set; }

    public string NomeTitular { get; private set; }

    public override string Descricao() => $"{Bandeira} **** {UltimosQuatroDigitos}";

    // O formato da validade ja e conferido no construtor; aqui so resta o token da operadora,
    // que e o que de fato permite cobrar (RNF0061). Nenhum requisito pede recusa por cartao
    // vencido, entao a data nao entra nesta verificacao.
    public override bool Validar() => !string.IsNullOrWhiteSpace(TokenOperadora);

    private static string ValidarUltimosQuatro(string valor)
    {
        var digitos = new string((valor ?? string.Empty).Where(char.IsDigit).ToArray());

        if (digitos.Length != 4)
        {
            throw new ExcecaoDeDominio(
                "Devem ser informados exatamente os quatro ultimos digitos do cartao.",
                "RNF0061");
        }

        return digitos;
    }

    private static string ValidarValidade(string valor)
    {
        var validade = Garantir.TextoPreenchido(valor, "validade do cartao", "RNF0061");
        var partes = validade.Split('/');

        if (partes.Length != 2
            || !int.TryParse(partes[0], out var mes)
            || !int.TryParse(partes[1], out var ano)
            || mes is < 1 or > 12
            || ano < 2000
            || ano > 2100)
        {
            throw new ExcecaoDeDominio(
                "A validade do cartao deve estar no formato MM/AAAA.",
                "RNF0061");
        }

        return $"{mes:00}/{ano:0000}";
    }
}

/// <summary>Chave PIX cadastrada como forma de pagamento (RF0081).</summary>
public sealed class Pix : FormaPagamento
{
    public Pix(
        Guid id,
        Guid clienteId,
        string apelido,
        bool principal,
        string chavePix,
        TipoChavePix tipoChave)
        : base(id, clienteId, apelido, principal)
    {
        ChavePix = Garantir.TextoComTamanhoMaximo(chavePix, 140, "chave PIX", "RF0081");
        TipoChave = tipoChave;

        if (!Validar())
        {
            throw new ExcecaoDeDominio(
                $"A chave PIX informada nao e valida para o tipo {tipoChave}.",
                "RF0081");
        }
    }

    private Pix()
    {
        ChavePix = null!;
    }

    public string ChavePix { get; private set; }

    public TipoChavePix TipoChave { get; private set; }

    public override string Descricao() => $"PIX ({TipoChave})";

    public override bool Validar() => TipoChave switch
    {
        TipoChavePix.Cpf => ChavePix.Count(char.IsDigit) == 11,
        TipoChavePix.Email => ChavePix.Contains('@') && ChavePix.Contains('.'),
        TipoChavePix.Telefone => ChavePix.Count(char.IsDigit) >= 10,
        TipoChavePix.Aleatoria => ChavePix.Length >= 32,
        _ => false
    };
}

/// <summary>Boleto bancario como forma de pagamento (RF0081).</summary>
public sealed class Boleto : FormaPagamento
{
    public Boleto(
        Guid id,
        Guid clienteId,
        string apelido,
        bool principal,
        string nomeSacado,
        string emailEnvio)
        : base(id, clienteId, apelido, principal)
    {
        NomeSacado = Garantir.TextoComTamanhoMaximo(nomeSacado, 200, "nome do sacado", "RF0081");
        EmailEnvio = Garantir.Email(emailEnvio, "RF0081");
    }

    private Boleto()
    {
        NomeSacado = null!;
        EmailEnvio = null!;
    }

    public string NomeSacado { get; private set; }

    public string EmailEnvio { get; private set; }

    public override string Descricao() => $"Boleto para {EmailEnvio}";

    public override bool Validar() =>
        !string.IsNullOrWhiteSpace(NomeSacado) && EmailEnvio.Contains('@');
}
