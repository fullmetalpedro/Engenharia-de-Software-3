using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Pessoas;
using FluentValidation;

namespace ChamadosManutencao.Application.UC02Tecnicos;

/// <summary>Area de atendimento na entrada (RF0027).</summary>
public sealed record AreaAtendimentoCommand(
    string Bairro,
    string CepInicial,
    string CepFinal,
    decimal TaxaDeslocamento);

/// <summary>Cadastro de tecnico (RF0021, RN0021).</summary>
public sealed record CadastrarTecnicoCommand(
    string NomeCompleto,
    string Cpf,
    string Email,
    string Telefone,
    string Senha,
    IReadOnlyCollection<Guid> EspecialidadeIds,
    IReadOnlyCollection<AreaAtendimentoCommand> AreasAtendimento);

/// <summary>Alteracao dos dados cadastrais (RF0022).</summary>
public sealed record AlterarTecnicoCommand(string NomeCompleto, string Email, string Telefone);

/// <summary>RF0026.</summary>
public sealed record DefinirEspecialidadesCommand(IReadOnlyCollection<Guid> EspecialidadeIds);

/// <summary>RF0027.</summary>
public sealed record DefinirAreasCommand(IReadOnlyCollection<AreaAtendimentoCommand> Areas);

/// <summary>Filtros de RF0025.</summary>
public sealed record FiltroDeTecnicos(
    string? Nome = null,
    string? Cpf = null,
    Guid? EspecialidadeId = null,
    string? Bairro = null,
    string? Cep = null,
    bool? Ativo = null,
    int? Page = null,
    int? PageSize = null);

public sealed record EspecialidadeDto(Guid Id, string Nome);

public sealed record AreaAtendimentoDto(
    Guid Id,
    string Bairro,
    string CepInicial,
    string CepFinal,
    decimal TaxaDeslocamento);

public sealed record DocumentoDoTecnicoDto(
    Guid Id,
    string NomeArquivo,
    string TipoMime,
    long TamanhoBytes,
    DateTimeOffset DataHoraUpload);

public sealed record TecnicoResumoDto(
    Guid Id,
    string CodigoTecnico,
    string NomeCompleto,
    string Cpf,
    string Email,
    string Telefone,
    bool Ativo);

public sealed record TecnicoDetalheDto(
    Guid Id,
    string CodigoTecnico,
    string NomeCompleto,
    string Cpf,
    string Email,
    string Telefone,
    bool Ativo,
    IReadOnlyCollection<EspecialidadeDto> Especialidades,
    IReadOnlyCollection<AreaAtendimentoDto> AreasAtendimento,
    IReadOnlyCollection<DocumentoDoTecnicoDto> Documentos);

/// <summary>Mapeamento manual, sem AutoMapper (RNF0022: SenhaHash nunca sai em DTO).</summary>
public static class TecnicosMapeamento
{
    public static EspecialidadeDto ParaDto(this CategoriaServico categoria) =>
        new(categoria.Id, categoria.Nome);

    public static AreaAtendimentoDto ParaDto(this AreaAtendimento area) =>
        new(area.Id, area.Bairro, area.CepInicial, area.CepFinal, area.TaxaDeslocamento);

    public static TecnicoResumoDto ParaResumo(this Tecnico tecnico) => new(
        tecnico.Id,
        tecnico.CodigoTecnico,
        tecnico.NomeCompleto,
        tecnico.Cpf,
        tecnico.Email,
        tecnico.Telefone,
        tecnico.Ativo);

    public static TecnicoDetalheDto ParaDetalhe(this Tecnico tecnico) => new(
        tecnico.Id,
        tecnico.CodigoTecnico,
        tecnico.NomeCompleto,
        tecnico.Cpf,
        tecnico.Email,
        tecnico.Telefone,
        tecnico.Ativo,
        tecnico.Especialidades.Select(ParaDto).ToList(),
        tecnico.AreasAtendimento.Select(ParaDto).ToList(),
        tecnico.Documentos
            .Select(d => new DocumentoDoTecnicoDto(
                d.Id,
                d.NomeArquivo,
                d.TipoMime,
                d.TamanhoBytes,
                d.DataHoraUpload))
            .ToList());
}

/// <summary>RN0021: nome, CPF, telefone, e-mail e ao menos uma especialidade.</summary>
public sealed class CadastrarTecnicoValidator : AbstractValidator<CadastrarTecnicoCommand>
{
    public CadastrarTecnicoValidator(Abstractions.IPoliticaDeSenha politica)
    {
        RuleFor(t => t.NomeCompleto).NotEmpty().MaximumLength(200);
        RuleFor(t => t.Cpf)
            .NotEmpty()
            .Must(cpf => (cpf ?? string.Empty).Count(char.IsDigit) == 11)
            .WithMessage("O CPF deve conter 11 digitos.");
        RuleFor(t => t.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(t => t.Telefone).NotEmpty().MaximumLength(20);
        RuleFor(t => t.Senha)
            .NotEmpty()
            .Must(senha => politica.EhForte(senha ?? string.Empty, out _))
            .WithMessage("A senha deve ter ao menos 8 caracteres, com maiuscula, minuscula e "
                + "caractere especial.");
        RuleFor(t => t.EspecialidadeIds)
            .NotEmpty()
            .WithMessage("O cadastro de tecnico exige ao menos uma especialidade.");
        RuleFor(t => t.AreasAtendimento)
            .NotEmpty()
            .WithMessage("O cadastro de tecnico exige ao menos uma area de atendimento.");
        RuleForEach(t => t.AreasAtendimento).SetValidator(new AreaAtendimentoValidator());
    }
}

public sealed class AreaAtendimentoValidator : AbstractValidator<AreaAtendimentoCommand>
{
    public AreaAtendimentoValidator()
    {
        RuleFor(a => a.Bairro).NotEmpty().MaximumLength(120);
        RuleFor(a => a.CepInicial)
            .NotEmpty()
            .Must(cep => (cep ?? string.Empty).Count(char.IsDigit) == 8)
            .WithMessage("O CEP inicial deve conter 8 digitos.");
        RuleFor(a => a.CepFinal)
            .NotEmpty()
            .Must(cep => (cep ?? string.Empty).Count(char.IsDigit) == 8)
            .WithMessage("O CEP final deve conter 8 digitos.");
        RuleFor(a => a.TaxaDeslocamento).GreaterThanOrEqualTo(0);
    }
}

public sealed class AlterarTecnicoValidator : AbstractValidator<AlterarTecnicoCommand>
{
    public AlterarTecnicoValidator()
    {
        RuleFor(t => t.NomeCompleto).NotEmpty().MaximumLength(200);
        RuleFor(t => t.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(t => t.Telefone).NotEmpty().MaximumLength(20);
    }
}

public sealed class DefinirEspecialidadesValidator : AbstractValidator<DefinirEspecialidadesCommand>
{
    public DefinirEspecialidadesValidator() =>
        RuleFor(c => c.EspecialidadeIds).NotEmpty();
}

public sealed class DefinirAreasValidator : AbstractValidator<DefinirAreasCommand>
{
    public DefinirAreasValidator()
    {
        RuleFor(c => c.Areas).NotEmpty();
        RuleForEach(c => c.Areas).SetValidator(new AreaAtendimentoValidator());
    }
}
