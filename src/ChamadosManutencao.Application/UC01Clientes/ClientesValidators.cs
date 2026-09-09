using ChamadosManutencao.Application.Abstractions;
using FluentValidation;

namespace ChamadosManutencao.Application.UC01Clientes;

/// <summary>
/// RN0011: nome, CPF, telefone, e-mail, senha e ao menos um imovel.
/// RNF0021: senha forte.
/// </summary>
public sealed class CadastrarClienteValidator : AbstractValidator<CadastrarClienteCommand>
{
    public CadastrarClienteValidator(IPoliticaDeSenha politica)
    {
        RuleFor(c => c.NomeCompleto).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Cpf).NotEmpty().Must(TerOnzeDigitos).WithMessage("O CPF deve conter 11 digitos.");
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(c => c.Telefone).NotEmpty().MaximumLength(20);

        RuleFor(c => c.Senha)
            .NotEmpty()
            .Must(senha => politica.EhForte(senha ?? string.Empty, out _))
            .WithMessage("A senha deve ter ao menos 8 caracteres, com maiuscula, minuscula e "
                + "caractere especial.");

        RuleFor(c => c.Imoveis)
            .NotEmpty()
            .WithMessage("O cadastro de cliente exige ao menos um imovel.");

        RuleForEach(c => c.Imoveis).SetValidator(new ImovelValidator());
    }

    private static bool TerOnzeDigitos(string? cpf) =>
        (cpf ?? string.Empty).Count(char.IsDigit) == 11;
}

/// <summary>RN0012: composicao obrigatoria do registro de imovel.</summary>
public sealed class ImovelValidator : AbstractValidator<ImovelCommand>
{
    public ImovelValidator()
    {
        RuleFor(i => i.Apelido).NotEmpty().MaximumLength(60);
        RuleFor(i => i.TipoImovel).IsInEnum();
        RuleFor(i => i.Logradouro).NotEmpty().MaximumLength(200);
        RuleFor(i => i.Numero).NotEmpty().MaximumLength(20);
        RuleFor(i => i.Bairro).NotEmpty().MaximumLength(120);
        RuleFor(i => i.Cep)
            .NotEmpty()
            .Must(cep => (cep ?? string.Empty).Count(char.IsDigit) == 8)
            .WithMessage("O CEP deve conter 8 digitos.");
        RuleFor(i => i.Cidade).NotEmpty().MaximumLength(120);
        RuleFor(i => i.Estado).NotEmpty().Length(2);
        RuleFor(i => i.Complemento).MaximumLength(120);
    }
}

/// <summary>RF0012.</summary>
public sealed class AlterarClienteValidator : AbstractValidator<AlterarClienteCommand>
{
    public AlterarClienteValidator()
    {
        RuleFor(c => c.NomeCompleto).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(c => c.Telefone).NotEmpty().MaximumLength(20);
    }
}
