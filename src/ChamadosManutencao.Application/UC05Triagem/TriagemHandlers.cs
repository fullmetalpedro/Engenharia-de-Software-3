using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Application.UC05Triagem;

/// <summary>RF0046.</summary>
public sealed record ClassificarUrgenciaCommand(Urgencia Urgencia);

/// <summary>RF0047 e RF0048.</summary>
public sealed record AtribuirTecnicoCommand(Guid TecnicoId);

/// <summary>RF0049.</summary>
public sealed record AlterarStatusCommand(StatusChamado Status, string? Observacao = null);

public sealed class ClassificarUrgenciaValidator : AbstractValidator<ClassificarUrgenciaCommand>
{
    public ClassificarUrgenciaValidator() => RuleFor(c => c.Urgencia).IsInEnum();
}

public sealed class AtribuirTecnicoValidator : AbstractValidator<AtribuirTecnicoCommand>
{
    public AtribuirTecnicoValidator() => RuleFor(c => c.TecnicoId).NotEmpty();
}

public sealed class AlterarStatusValidator : AbstractValidator<AlterarStatusCommand>
{
    public AlterarStatusValidator()
    {
        RuleFor(c => c.Status).IsInEnum();
        RuleFor(c => c.Observacao).MaximumLength(500);
    }
}

/// <summary>
/// Classifica a urgencia do chamado.
/// Requisitos: RF0046. Decisao D20: o administrador pode reclassificar a urgencia definida
/// automaticamente pela RN0031, e a alteracao fica no log de transacao (RNF0012).
/// Caso de uso: UC05.
/// </summary>
public sealed class ClassificarUrgenciaHandler
{
    private readonly IChamadoRepositorio _chamados;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public ClassificarUrgenciaHandler(IChamadoRepositorio chamados, IUnitOfWork unidadeDeTrabalho)
    {
        _chamados = chamados;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task ExecutarAsync(
        Guid chamadoId,
        ClassificarUrgenciaCommand comando,
        CancellationToken cancellationToken = default)
    {
        var chamado = await _chamados.ObterPorIdAsync(chamadoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", chamadoId);

        chamado.ClassificarUrgencia(comando.Urgencia);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
    }
}

/// <summary>
/// Atribui ou reatribui o tecnico responsavel pelo chamado.
/// Requisitos: RF0047, RF0048, RN0022, RN0023, RN0034, RNF0041.
/// Caso de uso: UC05.
/// </summary>
public sealed class AtribuirTecnicoHandler
{
    private readonly IChamadoRepositorio _chamados;
    private readonly ITecnicoRepositorio _tecnicos;
    private readonly IContextoDeLeitura _leitura;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public AtribuirTecnicoHandler(
        IChamadoRepositorio chamados,
        ITecnicoRepositorio tecnicos,
        IContextoDeLeitura leitura,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _chamados = chamados;
        _tecnicos = tecnicos;
        _leitura = leitura;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task ExecutarAsync(
        Guid chamadoId,
        AtribuirTecnicoCommand comando,
        CancellationToken cancellationToken = default)
    {
        var administradorId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        var chamado = await _chamados.ObterCompletoAsync(chamadoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", chamadoId);

        var tecnico = await _tecnicos.ObterCompletoAsync(comando.TecnicoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Tecnico", comando.TecnicoId);

        var categoriaId = await _leitura.TiposServico
            .Where(t => t.Id == chamado.TipoServicoId)
            .Select(t => t.CategoriaServicoId)
            .SingleAsync(cancellationToken);

        var cep = await _leitura.Imoveis
            .Where(i => i.Id == chamado.ImovelId)
            .Select(i => i.Cep)
            .SingleAsync(cancellationToken);

        // RN0022 e RN0023 sao verificadas dentro do dominio.
        chamado.AtribuirTecnico(tecnico, categoriaId, cep, administradorId, _relogio.Agora, _geradorId);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
    }
}

/// <summary>
/// Altera o status do chamado conforme o andamento do atendimento.
/// Requisitos: RF0049, RN0034, RNF0041.
/// Caso de uso: UC05.
/// </summary>
public sealed class AlterarStatusDoChamadoHandler
{
    private readonly IChamadoRepositorio _chamados;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public AlterarStatusDoChamadoHandler(
        IChamadoRepositorio chamados,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _chamados = chamados;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task ExecutarAsync(
        Guid chamadoId,
        AlterarStatusCommand comando,
        CancellationToken cancellationToken = default)
    {
        var administradorId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        var chamado = await _chamados.ObterCompletoAsync(chamadoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", chamadoId);

        chamado.AlterarStatus(
            comando.Status,
            administradorId,
            comando.Observacao,
            _relogio.Agora,
            _geradorId);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
    }
}

/// <summary>
/// Cancela o chamado por decisao administrativa, inclusive depois do inicio do atendimento.
/// Requisitos: RF0045, RN0033 (decisao D06), RN0034.
/// Caso de uso: UC05.
/// </summary>
public sealed class CancelamentoAdministrativoHandler
{
    private readonly IChamadoRepositorio _chamados;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public CancelamentoAdministrativoHandler(
        IChamadoRepositorio chamados,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _chamados = chamados;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task ExecutarAsync(
        Guid chamadoId,
        CancelamentoAdministrativoCommand comando,
        CancellationToken cancellationToken = default)
    {
        var administradorId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        var chamado = await _chamados.ObterCompletoAsync(chamadoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", chamadoId);

        chamado.CancelarPeloAdministrador(administradorId, comando.Motivo, _relogio.Agora, _geradorId);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
    }
}
