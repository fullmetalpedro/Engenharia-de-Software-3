using ChamadosManutencao.Domain.Enums;
using FluentValidation;

namespace ChamadosManutencao.Application.UC04Chamados;

/// <summary>
/// Abertura de chamado (RF0041). O campo IndicacaoDeRisco alimenta a RN0031 e as fotos
/// chegam junto na mesma requisicao multipart, para atender a RN0032 na abertura (decisao D07).
/// </summary>
public sealed record AbrirChamadoCommand(
    Guid ImovelId,
    Guid CategoriaServicoId,
    Guid TipoServicoId,
    string DescricaoProblema,
    bool IndicacaoDeRisco,
    Urgencia Urgencia = Urgencia.Media);

/// <summary>Arquivo recebido no upload, ja lido do formulario multipart.</summary>
public sealed record ArquivoRecebido(
    string NomeArquivo,
    string TipoMime,
    long TamanhoBytes,
    Stream Conteudo);

/// <summary>Cancelamento pelo cliente (RF0045, RN0033).</summary>
public sealed record CancelarChamadoCommand(string? Motivo = null);

/// <summary>Cancelamento administrativo (RF0045).</summary>
public sealed record CancelamentoAdministrativoCommand(string Motivo);

/// <summary>Filtros de RF0043 e RF0044.</summary>
public sealed record FiltroDeChamados(
    StatusChamado? Status = null,
    Urgencia? Urgencia = null,
    Guid? CategoriaId = null,
    Guid? TecnicoId = null,
    Guid? ClienteId = null,
    DateTimeOffset? DataInicio = null,
    DateTimeOffset? DataFim = null,
    long? Numero = null,
    int? Page = null,
    int? PageSize = null);

public sealed record AnexoDto(
    Guid Id,
    string NomeArquivo,
    string TipoMime,
    long TamanhoBytes,
    DateTimeOffset DataHoraUpload,
    OrigemAnexo Origem);

public sealed record HistoricoStatusDto(
    Guid Id,
    StatusChamado? StatusAnterior,
    StatusChamado StatusNovo,
    DateTimeOffset DataHoraAlteracao,
    Guid UsuarioResponsavelId,
    string? Observacao);

public sealed record AgendamentoDto(
    Guid Id,
    DateTimeOffset DataHoraProposta,
    int DuracaoEmMinutos,
    DateTimeOffset? DataHoraConfirmacao,
    OrigemProposta OrigemProposta,
    StatusAgendamento Status);

public sealed record ChamadoResumoDto(
    Guid Id,
    long Numero,
    DateTimeOffset DataHoraAbertura,
    StatusChamado Status,
    Urgencia Urgencia,
    bool ChamadoDeGarantia,
    Guid ClienteId,
    Guid ImovelId,
    Guid TipoServicoId,
    string TipoServico,
    string CategoriaServico,
    Guid? TecnicoId,
    string? Tecnico);

public sealed record ChamadoDetalheDto(
    Guid Id,
    long Numero,
    DateTimeOffset DataHoraAbertura,
    string DescricaoProblema,
    StatusChamado Status,
    Urgencia Urgencia,
    bool ChamadoDeGarantia,
    Guid ClienteId,
    Guid ImovelId,
    Guid TipoServicoId,
    string TipoServico,
    Guid CategoriaServicoId,
    string CategoriaServico,
    Guid? TecnicoId,
    string? Tecnico,
    Guid? ChamadoOriginalId,
    Guid? GarantiaId,
    IReadOnlyCollection<AnexoDto> Anexos,
    IReadOnlyCollection<AgendamentoDto> Agendamentos);

public sealed class AbrirChamadoValidator : AbstractValidator<AbrirChamadoCommand>
{
    public AbrirChamadoValidator()
    {
        RuleFor(c => c.ImovelId).NotEmpty();
        RuleFor(c => c.CategoriaServicoId).NotEmpty();
        RuleFor(c => c.TipoServicoId).NotEmpty();
        RuleFor(c => c.DescricaoProblema).NotEmpty().MaximumLength(2000);
        RuleFor(c => c.Urgencia).IsInEnum();
    }
}

public sealed class CancelamentoAdministrativoValidator
    : AbstractValidator<CancelamentoAdministrativoCommand>
{
    public CancelamentoAdministrativoValidator() =>
        RuleFor(c => c.Motivo).NotEmpty().MaximumLength(500);
}
