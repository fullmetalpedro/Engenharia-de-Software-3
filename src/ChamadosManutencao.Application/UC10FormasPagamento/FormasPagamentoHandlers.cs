using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Financeiro;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Application.UC10FormasPagamento;

/// <summary>
/// Cadastro de forma de pagamento (RF0081). O numero completo do cartao entra aqui, e usado
/// apenas para a tokenizacao e e descartado da memoria em seguida (RNF0061).
/// </summary>
public sealed record CadastrarFormaPagamentoCommand(
    string Tipo,
    string Apelido,
    bool Principal = false,
    string? NumeroCartao = null,
    string? NomeTitular = null,
    string? Validade = null,
    string? CodigoSeguranca = null,
    string? ChavePix = null,
    TipoChavePix? TipoChave = null,
    string? NomeSacado = null,
    string? EmailEnvio = null);

/// <summary>Alteracao do apelido da forma de pagamento (RF0081).</summary>
public sealed record AlterarFormaPagamentoCommand(string Apelido);

/// <summary>
/// Representacao segura da forma de pagamento: nunca inclui numero completo nem token
/// (RNF0061).
/// </summary>
public sealed record FormaPagamentoDto(
    Guid Id,
    string Tipo,
    string Apelido,
    string Descricao,
    bool Principal,
    bool Ativa,
    string? Bandeira,
    string? UltimosQuatroDigitos,
    string? Validade);

public sealed class CadastrarFormaPagamentoValidator : AbstractValidator<CadastrarFormaPagamentoCommand>
{
    public CadastrarFormaPagamentoValidator()
    {
        RuleFor(f => f.Apelido).NotEmpty().MaximumLength(60);

        RuleFor(f => f.Tipo)
            .NotEmpty()
            .Must(tipo => tipo is "CartaoCredito" or "Pix" or "Boleto")
            .WithMessage("O tipo deve ser 'CartaoCredito', 'Pix' ou 'Boleto'.");

        When(f => f.Tipo == "CartaoCredito", () =>
        {
            RuleFor(f => f.NumeroCartao)
                .NotEmpty()
                .Must(numero => (numero ?? string.Empty).Count(char.IsDigit) is >= 13 and <= 19)
                .WithMessage("O numero do cartao deve ter entre 13 e 19 digitos.");
            RuleFor(f => f.NomeTitular).NotEmpty().MaximumLength(200);
            RuleFor(f => f.Validade)
                .NotEmpty()
                .Matches(@"^\d{2}/\d{4}$")
                .WithMessage("A validade deve estar no formato MM/AAAA.");
            RuleFor(f => f.CodigoSeguranca)
                .NotEmpty()
                .Must(codigo => (codigo ?? string.Empty).Length is 3 or 4)
                .WithMessage("O codigo de seguranca deve ter 3 ou 4 digitos.");
        });

        When(f => f.Tipo == "Pix", () =>
        {
            RuleFor(f => f.ChavePix).NotEmpty().MaximumLength(140);
            RuleFor(f => f.TipoChave).NotNull().IsInEnum();
        });

        When(f => f.Tipo == "Boleto", () =>
        {
            RuleFor(f => f.NomeSacado).NotEmpty().MaximumLength(200);
            RuleFor(f => f.EmailEnvio).NotEmpty().EmailAddress();
        });
    }
}

public sealed class AlterarFormaPagamentoValidator : AbstractValidator<AlterarFormaPagamentoCommand>
{
    public AlterarFormaPagamentoValidator() =>
        RuleFor(f => f.Apelido).NotEmpty().MaximumLength(60);
}

/// <summary>
/// Gerencia as formas de pagamento do cliente.
/// Requisitos: RF0081, RNF0061.
/// Caso de uso: UC10.
/// </summary>
public sealed class FormasDePagamentoHandler
{
    private readonly IClienteRepositorio _clientes;
    private readonly IContextoDeLeitura _leitura;
    private readonly IGatewayPagamento _gateway;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public FormasDePagamentoHandler(
        IClienteRepositorio clientes,
        IContextoDeLeitura leitura,
        IGatewayPagamento gateway,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IUnitOfWork unidadeDeTrabalho)
    {
        _clientes = clientes;
        _leitura = leitura;
        _gateway = gateway;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<IReadOnlyCollection<FormaPagamentoDto>> ListarAsync(
        Guid clienteId,
        CancellationToken cancellationToken = default)
    {
        Autorizacao.ExigirDonoOuAdministrador(_usuarioAtual, clienteId);

        var formas = await _leitura.FormasDePagamento
            .Where(f => f.ClienteId == clienteId && f.Ativa)
            .ToListAsync(cancellationToken);

        return formas.Select(Mapear).ToList();
    }

    public async Task<FormaPagamentoDto> CadastrarAsync(
        Guid clienteId,
        CadastrarFormaPagamentoCommand comando,
        CancellationToken cancellationToken = default)
    {
        Autorizacao.ExigirDono(_usuarioAtual, clienteId);

        var cliente = await _clientes.ObterComFormasPagamentoAsync(clienteId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", clienteId);

        FormaPagamento forma;

        switch (comando.Tipo)
        {
            case "CartaoCredito":
                {
                    // RNF0061: o numero completo vai para o gateway, volta como token e nao e
                    // persistido nem registrado em log.
                    var token = await _gateway.TokenizarCartaoAsync(
                        new DadosDeCartao(
                            comando.NumeroCartao!,
                            comando.NomeTitular!,
                            comando.Validade!,
                            comando.CodigoSeguranca!),
                        cancellationToken);

                    forma = new CartaoCredito(
                        _geradorId.NovoId(),
                        clienteId,
                        comando.Apelido,
                        comando.Principal,
                        token.Bandeira,
                        token.UltimosQuatroDigitos,
                        token.Token,
                        token.Validade,
                        comando.NomeTitular!);
                    break;
                }

            case "Pix":
                forma = new Pix(
                    _geradorId.NovoId(),
                    clienteId,
                    comando.Apelido,
                    comando.Principal,
                    comando.ChavePix!,
                    comando.TipoChave!.Value);
                break;

            case "Boleto":
                forma = new Boleto(
                    _geradorId.NovoId(),
                    clienteId,
                    comando.Apelido,
                    comando.Principal,
                    comando.NomeSacado!,
                    comando.EmailEnvio!);
                break;

            default:
                throw new ValidacaoException(nameof(comando.Tipo), "Tipo de forma de pagamento invalido.");
        }

        cliente.AdicionarFormaPagamento(forma);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return Mapear(forma);
    }

    public async Task<FormaPagamentoDto> AlterarAsync(
        Guid clienteId,
        Guid formaId,
        AlterarFormaPagamentoCommand comando,
        CancellationToken cancellationToken = default)
    {
        Autorizacao.ExigirDono(_usuarioAtual, clienteId);

        var cliente = await _clientes.ObterComFormasPagamentoAsync(clienteId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", clienteId);

        var forma = cliente.FormasPagamento.SingleOrDefault(f => f.Id == formaId)
            ?? throw new RecursoNaoEncontradoException("Forma de pagamento", formaId);

        forma.AlterarApelido(comando.Apelido);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return Mapear(forma);
    }

    public async Task RemoverAsync(
        Guid clienteId,
        Guid formaId,
        CancellationToken cancellationToken = default)
    {
        Autorizacao.ExigirDono(_usuarioAtual, clienteId);

        var cliente = await _clientes.ObterComFormasPagamentoAsync(clienteId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", clienteId);

        cliente.RemoverFormaPagamento(formaId);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
    }

    public async Task<FormaPagamentoDto> DefinirPrincipalAsync(
        Guid clienteId,
        Guid formaId,
        CancellationToken cancellationToken = default)
    {
        Autorizacao.ExigirDono(_usuarioAtual, clienteId);

        var cliente = await _clientes.ObterComFormasPagamentoAsync(clienteId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", clienteId);

        cliente.DefinirFormaPagamentoPrincipal(formaId);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return Mapear(cliente.FormasPagamento.Single(f => f.Id == formaId));
    }

    /// <summary>RNF0061: a resposta devolve bandeira, quatro ultimos digitos e validade, nunca o token.</summary>
    internal static FormaPagamentoDto Mapear(FormaPagamento forma) => forma switch
    {
        CartaoCredito cartao => new FormaPagamentoDto(
            cartao.Id,
            "CartaoCredito",
            cartao.Apelido,
            cartao.Descricao(),
            cartao.Principal,
            cartao.Ativa,
            cartao.Bandeira,
            cartao.UltimosQuatroDigitos,
            cartao.Validade),

        _ => new FormaPagamentoDto(
            forma.Id,
            forma is Pix ? "Pix" : "Boleto",
            forma.Apelido,
            forma.Descricao(),
            forma.Principal,
            forma.Ativa,
            null,
            null,
            null)
    };
}
