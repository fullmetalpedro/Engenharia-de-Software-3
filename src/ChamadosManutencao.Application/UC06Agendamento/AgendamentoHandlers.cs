using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Configuration;

namespace ChamadosManutencao.Application.UC06Agendamento;

/// <summary>
/// Proposta de agendamento (RF0051, RF0053). A duracao e opcional: sem ela vale a janela
/// padrao configurada (decisao D08).
/// </summary>
public sealed record AgendarCommand(DateTimeOffset DataHoraProposta, int? DuracaoEmMinutos = null);

public sealed class AgendarValidator : AbstractValidator<AgendarCommand>
{
    public AgendarValidator()
    {
        RuleFor(a => a.DataHoraProposta).NotEmpty();
        RuleFor(a => a.DuracaoEmMinutos)
            .GreaterThan(0)
            .When(a => a.DuracaoEmMinutos is not null);
    }
}

/// <summary>
/// Propoe data e hora para a execucao do atendimento.
/// Requisitos: RF0051, RN0034, RN0041, RNF0041.
/// Caso de uso: UC06.
/// </summary>
public sealed class AgendarAtendimentoHandler
{
    /// <summary>Janela padrao ocupada por um atendimento (decisao D08).</summary>
    public const int DuracaoPadraoEmMinutos = 120;

    private readonly IChamadoRepositorio _chamados;
    private readonly ITecnicoRepositorio _tecnicos;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;
    private readonly int _duracaoConfigurada;

    public AgendarAtendimentoHandler(
        IChamadoRepositorio chamados,
        ITecnicoRepositorio tecnicos,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho,
        IConfiguration configuracao)
    {
        _chamados = chamados;
        _tecnicos = tecnicos;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _duracaoConfigurada = configuracao.GetValue<int?>("Agendamento:DuracaoPadraoEmMinutos")
            ?? DuracaoPadraoEmMinutos;
    }

    public async Task<AgendamentoDto> ExecutarAsync(
        Guid chamadoId,
        AgendarCommand comando,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        var chamado = await _chamados.ObterCompletoAsync(chamadoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", chamadoId);

        var ehAdministrador = Autorizacao.EhAdministrador(_usuarioAtual);

        if (!ehAdministrador && !chamado.EstaAtribuidoAo(usuarioId))
        {
            throw new AcessoNegadoException("Somente o tecnico atribuido ou o administrador pode agendar.");
        }

        if (chamado.TecnicoId is null)
        {
            throw new ConflitoException(
                "O chamado precisa de um tecnico atribuido antes do agendamento.",
                "RF0051");
        }

        var duracao = comando.DuracaoEmMinutos ?? _duracaoConfigurada;

        var tecnico = await _tecnicos.ObterCompletoAsync(chamado.TecnicoId.Value, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Tecnico", chamado.TecnicoId.Value);

        var compromissos = await _tecnicos.ObterCompromissosAsync(
            tecnico.Id,
            ignorarChamadoId: chamadoId,
            cancellationToken);

        var fim = comando.DataHoraProposta.AddMinutes(duracao);

        // RN0041: nada de agendar em horario ja ocupado do tecnico.
        if (!tecnico.EstaDisponivel(comando.DataHoraProposta, fim, compromissos))
        {
            throw new ConflitoException(
                "O tecnico ja possui atendimento agendado nesse horario.",
                "RN0041");
        }

        var agendamento = new Agendamento(
            _geradorId.NovoId(),
            chamadoId,
            comando.DataHoraProposta,
            duracao,
            ehAdministrador ? OrigemProposta.Administrador : OrigemProposta.Tecnico);

        chamado.AdicionarAgendamento(agendamento, usuarioId, _relogio.Agora, _geradorId);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return new AgendamentoDto(
            agendamento.Id,
            agendamento.DataHoraProposta,
            agendamento.DuracaoEmMinutos,
            agendamento.DataHoraConfirmacao,
            agendamento.OrigemProposta,
            agendamento.Status);
    }
}

/// <summary>
/// Confirma o agendamento proposto.
/// Requisitos: RF0052, RN0034, RNF0041.
/// Caso de uso: UC06.
/// </summary>
public sealed class ConfirmarAgendamentoHandler
{
    private readonly IChamadoRepositorio _chamados;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public ConfirmarAgendamentoHandler(
        IChamadoRepositorio chamados,
        IUsuarioAtual usuarioAtual,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _chamados = chamados;
        _usuarioAtual = usuarioAtual;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<AgendamentoDto> ExecutarAsync(
        Guid agendamentoId,
        CancellationToken cancellationToken = default)
    {
        var chamado = await _chamados.ObterPorAgendamentoAsync(agendamentoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Agendamento", agendamentoId);

        Autorizacao.ExigirDono(_usuarioAtual, chamado.ClienteId);

        var agendamento = chamado.Agendamentos.Single(a => a.Id == agendamentoId);

        agendamento.Confirmar(_relogio.Agora);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return new AgendamentoDto(
            agendamento.Id,
            agendamento.DataHoraProposta,
            agendamento.DuracaoEmMinutos,
            agendamento.DataHoraConfirmacao,
            agendamento.OrigemProposta,
            agendamento.Status);
    }
}

/// <summary>
/// Solicita o reagendamento de um atendimento ja agendado.
/// Requisitos: RF0053, RN0041, RNF0041.
/// Caso de uso: UC06.
/// </summary>
public sealed class ReagendarAtendimentoHandler
{
    private readonly IChamadoRepositorio _chamados;
    private readonly ITecnicoRepositorio _tecnicos;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;
    private readonly int _duracaoConfigurada;

    public ReagendarAtendimentoHandler(
        IChamadoRepositorio chamados,
        ITecnicoRepositorio tecnicos,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho,
        IConfiguration configuracao)
    {
        _chamados = chamados;
        _tecnicos = tecnicos;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _duracaoConfigurada = configuracao.GetValue<int?>("Agendamento:DuracaoPadraoEmMinutos")
            ?? AgendarAtendimentoHandler.DuracaoPadraoEmMinutos;
    }

    public async Task<AgendamentoDto> ExecutarAsync(
        Guid agendamentoId,
        AgendarCommand comando,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        var chamado = await _chamados.ObterPorAgendamentoAsync(agendamentoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Agendamento", agendamentoId);

        var ehCliente = usuarioId == chamado.ClienteId;
        var ehTecnico = chamado.EstaAtribuidoAo(usuarioId);

        if (!ehCliente && !ehTecnico && !Autorizacao.EhAdministrador(_usuarioAtual))
        {
            throw new AcessoNegadoException(
                "Somente o cliente dono ou o tecnico atribuido pode reagendar.");
        }

        var duracao = comando.DuracaoEmMinutos ?? _duracaoConfigurada;

        var tecnico = await _tecnicos.ObterCompletoAsync(chamado.TecnicoId!.Value, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Tecnico", chamado.TecnicoId.Value);

        var compromissos = await _tecnicos.ObterCompromissosAsync(
            tecnico.Id,
            ignorarChamadoId: chamado.Id,
            cancellationToken);

        var fim = comando.DataHoraProposta.AddMinutes(duracao);

        if (!tecnico.EstaDisponivel(comando.DataHoraProposta, fim, compromissos))
        {
            throw new ConflitoException(
                "O tecnico ja possui atendimento agendado nesse horario.",
                "RN0041");
        }

        var novoAgendamento = new Agendamento(
            _geradorId.NovoId(),
            chamado.Id,
            comando.DataHoraProposta,
            duracao,
            ehCliente ? OrigemProposta.Cliente : OrigemProposta.Tecnico);

        // AdicionarAgendamento marca a proposta anterior como reagendada.
        chamado.AdicionarAgendamento(novoAgendamento, usuarioId, _relogio.Agora, _geradorId);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return new AgendamentoDto(
            novoAgendamento.Id,
            novoAgendamento.DataHoraProposta,
            novoAgendamento.DuracaoEmMinutos,
            novoAgendamento.DataHoraConfirmacao,
            novoAgendamento.OrigemProposta,
            novoAgendamento.Status);
    }
}
