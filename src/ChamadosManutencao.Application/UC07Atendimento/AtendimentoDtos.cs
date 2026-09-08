using ChamadosManutencao.Domain.Enums;
using FluentValidation;

namespace ChamadosManutencao.Application.UC07Atendimento;

/// <summary>Item do orcamento na entrada (RF0055).</summary>
public sealed record ItemOrcamentoCommand(
    string Descricao,
    TipoItem Tipo,
    int Quantidade,
    decimal ValorUnitario);

/// <summary>Registro do orcamento (RF0055).</summary>
public sealed record RegistrarOrcamentoCommand(IReadOnlyCollection<ItemOrcamentoCommand> Itens);

/// <summary>Decisao do cliente sobre o orcamento (RF0056, RN0042).</summary>
public sealed record DecisaoDeOrcamentoCommand(string Decisao, bool SolicitarNovoOrcamento = false);

/// <summary>Conclusao do atendimento (RF0057).</summary>
public sealed record ConcluirAtendimentoCommand(string RelatoTecnico);

public sealed record ItemOrcamentoDto(
    Guid Id,
    string Descricao,
    TipoItem Tipo,
    int Quantidade,
    decimal ValorUnitario,
    decimal Subtotal);

public sealed record OrcamentoDto(
    Guid Id,
    Guid AtendimentoId,
    DateTimeOffset DataHoraRegistro,
    DateTimeOffset PrazoAprovacao,
    StatusOrcamento Status,
    DateTimeOffset? DataHoraDecisao,
    decimal ValorPecas,
    decimal ValorMaoObra,
    decimal ValorTotal,
    IReadOnlyCollection<ItemOrcamentoDto> Itens);

public sealed record AtendimentoDto(
    Guid Id,
    Guid ChamadoId,
    Guid TecnicoId,
    DateTimeOffset DataHoraInicio,
    DateTimeOffset? DataHoraConclusao,
    string? RelatoTecnico,
    Guid? GarantiaId);

public sealed record GarantiaDto(
    Guid Id,
    Guid AtendimentoId,
    DateTimeOffset DataInicio,
    DateTimeOffset DataFim,
    int PrazoDias,
    bool Vigente);

/// <summary>Resultado da conclusao: garantia sempre, fatura apenas quando nao e garantia.</summary>
public sealed record ResultadoDaConclusaoDto(
    AtendimentoDto Atendimento,
    GarantiaDto Garantia,
    Guid? FaturaId,
    long? NumeroDaFatura,
    decimal? ValorDaFatura);

public sealed class RegistrarOrcamentoValidator : AbstractValidator<RegistrarOrcamentoCommand>
{
    public RegistrarOrcamentoValidator()
    {
        RuleFor(o => o.Itens).NotEmpty();
        RuleForEach(o => o.Itens).SetValidator(new ItemOrcamentoValidator());
    }
}

public sealed class ItemOrcamentoValidator : AbstractValidator<ItemOrcamentoCommand>
{
    public ItemOrcamentoValidator()
    {
        RuleFor(i => i.Descricao).NotEmpty().MaximumLength(300);
        RuleFor(i => i.Tipo).IsInEnum();
        RuleFor(i => i.Quantidade).GreaterThan(0);
        RuleFor(i => i.ValorUnitario).GreaterThanOrEqualTo(0);
    }
}

public sealed class DecisaoDeOrcamentoValidator : AbstractValidator<DecisaoDeOrcamentoCommand>
{
    public DecisaoDeOrcamentoValidator() =>
        RuleFor(d => d.Decisao)
            .NotEmpty()
            .Must(decisao => decisao is "Aprovado" or "Recusado")
            .WithMessage("A decisao deve ser 'Aprovado' ou 'Recusado'.");
}

public sealed class ConcluirAtendimentoValidator : AbstractValidator<ConcluirAtendimentoCommand>
{
    public ConcluirAtendimentoValidator() =>
        RuleFor(c => c.RelatoTecnico).NotEmpty().MaximumLength(2000);
}
