using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Domain.Avaliacoes;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Application.UC08Avaliacoes;

/// <summary>Avaliacao do atendimento pelo cliente (RF0061).</summary>
public sealed record RegistrarAvaliacaoCommand(int Nota, string? Comentario = null);

/// <summary>Resposta publica do administrador (RF0063).</summary>
public sealed record ResponderAvaliacaoCommand(string Texto);

public sealed record RespostaAvaliacaoDto(
    Guid Id,
    string Texto,
    DateTimeOffset DataHoraResposta,
    Guid AdministradorId);

public sealed record AvaliacaoDto(
    Guid Id,
    Guid ChamadoId,
    long NumeroDoChamado,
    Guid ClienteId,
    Guid? TecnicoId,
    int Nota,
    string? Comentario,
    DateTimeOffset DataHoraRegistro,
    RespostaAvaliacaoDto? Resposta);

public sealed class RegistrarAvaliacaoValidator : AbstractValidator<RegistrarAvaliacaoCommand>
{
    public RegistrarAvaliacaoValidator()
    {
        RuleFor(a => a.Nota).InclusiveBetween(1, 5);
        RuleFor(a => a.Comentario).MaximumLength(2000);
    }
}

public sealed class ResponderAvaliacaoValidator : AbstractValidator<ResponderAvaliacaoCommand>
{
    public ResponderAvaliacaoValidator() =>
        RuleFor(r => r.Texto).NotEmpty().MaximumLength(2000);
}

/// <summary>
/// Registra a avaliacao do atendimento concluido.
/// Requisitos: RF0061, RN0051, RN0052.
/// Caso de uso: UC08.
/// </summary>
public sealed class RegistrarAvaliacaoHandler
{
    private readonly IAvaliacaoRepositorio _avaliacoes;
    private readonly IChamadoRepositorio _chamados;
    private readonly IContextoDeLeitura _leitura;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public RegistrarAvaliacaoHandler(
        IAvaliacaoRepositorio avaliacoes,
        IChamadoRepositorio chamados,
        IContextoDeLeitura leitura,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _avaliacoes = avaliacoes;
        _chamados = chamados;
        _leitura = leitura;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<AvaliacaoDto> ExecutarAsync(
        Guid chamadoId,
        RegistrarAvaliacaoCommand comando,
        CancellationToken cancellationToken = default)
    {
        var chamado = await _chamados.ObterPorIdAsync(chamadoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", chamadoId);

        Autorizacao.ExigirDono(_usuarioAtual, chamado.ClienteId);

        if (chamado.Status != StatusChamado.Concluido)
        {
            throw new ConflitoException(
                "Somente chamado concluido pode ser avaliado.",
                "RN0051");
        }

        var jaAvaliado = await _avaliacoes.ObterPorChamadoAsync(chamadoId, cancellationToken);

        if (jaAvaliado is not null)
        {
            throw new ConflitoException("Este chamado ja foi avaliado.", "RF0061");
        }

        var conclusao = await _leitura.Atendimentos
            .Where(a => a.ChamadoId == chamadoId && a.DataHoraConclusao != null)
            .Select(a => a.DataHoraConclusao!.Value)
            .SingleOrDefaultAsync(cancellationToken);

        if (conclusao == default)
        {
            throw new ConflitoException("Nao ha atendimento concluido para este chamado.", "RN0051");
        }

        // RN0052: 15 dias corridos apos a conclusao (decisao D13: a janela e derivada da data).
        if (!Avaliacao.DentroDoPrazo(conclusao, _relogio.Agora))
        {
            throw new ConflitoException(
                $"O prazo de {Avaliacao.PrazoParaAvaliarEmDias} dias corridos para avaliar ja expirou.",
                "RN0052");
        }

        var avaliacao = new Avaliacao(
            _geradorId.NovoId(),
            chamadoId,
            chamado.ClienteId,
            chamado.TecnicoId,
            comando.Nota,
            comando.Comentario,
            _relogio.Agora);

        _avaliacoes.Adicionar(avaliacao);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return new AvaliacaoDto(
            avaliacao.Id,
            avaliacao.ChamadoId,
            chamado.Numero,
            avaliacao.ClienteId,
            avaliacao.TecnicoId,
            avaliacao.Nota,
            avaliacao.Comentario,
            avaliacao.DataHoraRegistro,
            Resposta: null);
    }
}

/// <summary>
/// Consulta as avaliacoes recebidas por um tecnico.
/// Requisitos: RF0062, RNF0011.
/// Caso de uso: UC08.
/// </summary>
public sealed class ConsultarAvaliacoesDoTecnicoHandler
{
    private readonly IContextoDeLeitura _leitura;

    public ConsultarAvaliacoesDoTecnicoHandler(IContextoDeLeitura leitura) => _leitura = leitura;

    public async Task<IReadOnlyCollection<AvaliacaoDto>> ExecutarAsync(
        Guid tecnicoId,
        CancellationToken cancellationToken = default)
    {

        var consulta = _leitura.Avaliacoes.Where(a => a.TecnicoId == tecnicoId);

        var itens = await consulta
            .OrderByDescending(a => a.DataHoraRegistro)
            .Select(a => new AvaliacaoDto(
                a.Id,
                a.ChamadoId,
                _leitura.Chamados.Where(c => c.Id == a.ChamadoId).Select(c => c.Numero).First(),
                a.ClienteId,
                a.TecnicoId,
                a.Nota,
                a.Comentario,
                a.DataHoraRegistro,
                _leitura.RespostasDeAvaliacao
                    .Where(r => r.AvaliacaoId == a.Id)
                    .Select(r => new RespostaAvaliacaoDto(
                        r.Id,
                        r.Texto,
                        r.DataHoraResposta,
                        r.AdministradorId))
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return itens;
    }
}

/// <summary>
/// Responde publicamente a uma avaliacao.
/// Requisitos: RF0063.
/// Caso de uso: UC08.
/// </summary>
public sealed class ResponderAvaliacaoHandler
{
    private readonly IAvaliacaoRepositorio _avaliacoes;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public ResponderAvaliacaoHandler(
        IAvaliacaoRepositorio avaliacoes,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _avaliacoes = avaliacoes;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<RespostaAvaliacaoDto> ExecutarAsync(
        Guid avaliacaoId,
        ResponderAvaliacaoCommand comando,
        CancellationToken cancellationToken = default)
    {
        var administradorId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        var avaliacao = await _avaliacoes.ObterPorIdAsync(avaliacaoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Avaliacao", avaliacaoId);

        var resposta = avaliacao.Responder(
            _geradorId.NovoId(),
            administradorId,
            comando.Texto,
            _relogio.Agora);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return new RespostaAvaliacaoDto(
            resposta.Id,
            resposta.Texto,
            resposta.DataHoraResposta,
            resposta.AdministradorId);
    }
}
