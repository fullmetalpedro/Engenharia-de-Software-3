using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Eventos;
using ChamadosManutencao.Domain.Pessoas;

namespace ChamadosManutencao.Domain.Chamados;

/// <summary>
/// Chamado de manutencao. Raiz do agregado que guarda anexos, historico de status e
/// agendamentos.
/// Requisitos: RF0041, RF0042, RF0045, RF0046, RF0047, RF0048, RF0049, RF0050, RF0085,
/// RN0031, RN0032, RN0033, RN0034, RN0035, RN0041, RNF0041, RNF0042, RNF0043.
/// </summary>
public sealed class Chamado : RaizDeAgregado
{
    /// <summary>RNF0043: no maximo 5 anexos por chamado, contados por origem (decisao D09).</summary>
    public const int MaximoDeAnexos = 5;

    /// <summary>RN0035: prazo de reabertura, em dias corridos apos a conclusao.</summary>
    public const int PrazoDeReaberturaEmDias = 7;

    private readonly List<Anexo> _anexos = [];
    private readonly List<HistoricoStatus> _historicoStatus = [];
    private readonly List<Agendamento> _agendamentos = [];

    private Chamado(
        Guid id,
        long numero,
        Guid clienteId,
        Guid imovelId,
        Guid tipoServicoId,
        string descricaoProblema,
        Urgencia urgencia,
        DateTimeOffset dataHoraAbertura,
        bool chamadoDeGarantia,
        Guid? chamadoOriginalId,
        Guid? garantiaId,
        Guid geradoPorUsuarioId,
        IGeradorId geradorId)
        : base(id)
    {
        Numero = numero;
        ClienteId = clienteId;
        ImovelId = imovelId;
        TipoServicoId = tipoServicoId;
        DescricaoProblema = Garantir.TextoComTamanhoMaximo(
            descricaoProblema, 2000, "descricao do problema", "RF0041");
        Urgencia = urgencia;
        DataHoraAbertura = dataHoraAbertura;
        ChamadoDeGarantia = chamadoDeGarantia;
        ChamadoOriginalId = chamadoOriginalId;
        GarantiaId = garantiaId;
        Status = StatusChamado.Aberto;

        _historicoStatus.Add(new HistoricoStatus(
            geradorId.NovoId(),
            Id,
            statusAnterior: null,
            statusNovo: StatusChamado.Aberto,
            dataHoraAbertura,
            geradoPorUsuarioId,
            chamadoDeGarantia ? "Chamado de garantia aberto pelo cliente." : "Chamado aberto pelo cliente."));

        RegistrarEvento(new StatusDoChamadoAlterado(
            Id, Numero, ClienteId, null, StatusChamado.Aberto, dataHoraAbertura));
    }

    private Chamado()
    {
        DescricaoProblema = null!;
    }

    /// <summary>Numero sequencial unico vindo da sequence seq_numero_chamado (RNF0042).</summary>
    public long Numero { get; private set; }

    public DateTimeOffset DataHoraAbertura { get; private set; }

    public string DescricaoProblema { get; private set; }

    public Urgencia Urgencia { get; private set; }

    public StatusChamado Status { get; private set; }

    /// <summary>RF0085: chamado aberto por acionamento de garantia nao gera fatura (RN0071).</summary>
    public bool ChamadoDeGarantia { get; private set; }

    public Guid ClienteId { get; private set; }

    public Guid ImovelId { get; private set; }

    public Guid TipoServicoId { get; private set; }

    public Guid? TecnicoId { get; private set; }

    /// <summary>
    /// Autorrelacionamento usado tanto pela reabertura (RN0035) quanto pelo acionamento de
    /// garantia (RF0085). O campo <see cref="ChamadoDeGarantia"/> distingue os dois casos.
    /// </summary>
    public Guid? ChamadoOriginalId { get; private set; }

    public Guid? GarantiaId { get; private set; }

    public IReadOnlyCollection<Anexo> Anexos => _anexos.AsReadOnly();

    public IReadOnlyCollection<HistoricoStatus> HistoricoStatus => _historicoStatus.AsReadOnly();

    public IReadOnlyCollection<Agendamento> Agendamentos => _agendamentos.AsReadOnly();

    /// <summary>RF0041: abertura de chamado comum.</summary>
    public static Chamado Abrir(
        Guid id,
        long numero,
        Guid clienteId,
        Guid imovelId,
        Guid tipoServicoId,
        string descricaoProblema,
        Urgencia urgencia,
        DateTimeOffset dataHoraAbertura,
        IGeradorId geradorId) =>
        new(id,
            numero,
            clienteId,
            imovelId,
            tipoServicoId,
            descricaoProblema,
            urgencia,
            dataHoraAbertura,
            chamadoDeGarantia: false,
            chamadoOriginalId: null,
            garantiaId: null,
            geradoPorUsuarioId: clienteId,
            geradorId);

    /// <summary>RF0085: chamado nascido do acionamento de uma garantia.</summary>
    public static Chamado AbrirParaGarantia(
        Guid id,
        long numero,
        Guid clienteId,
        Guid imovelId,
        Guid tipoServicoId,
        string descricaoProblema,
        DateTimeOffset dataHoraAbertura,
        Guid chamadoOriginalId,
        Guid garantiaId,
        IGeradorId geradorId) =>
        new(id,
            numero,
            clienteId,
            imovelId,
            tipoServicoId,
            descricaoProblema,
            Urgencia.Alta,
            dataHoraAbertura,
            chamadoDeGarantia: true,
            chamadoOriginalId: chamadoOriginalId,
            garantiaId: garantiaId,
            geradoPorUsuarioId: clienteId,
            geradorId);

    /// <summary>
    /// RN0034: unica porta de entrada para mudanca de status. Grava o historico (RF0050) e
    /// publica o evento consumido pelo notificador (RNF0041).
    /// </summary>
    public void AlterarStatus(
        StatusChamado novoStatus,
        Guid usuarioResponsavelId,
        string? observacao,
        DateTimeOffset agora,
        IGeradorId geradorId)
    {
        if (!MaquinaDeEstadosDoChamado.PodeIr(Status, novoStatus))
        {
            throw new TransicaoDeStatusInvalidaException(Status, novoStatus);
        }

        var anterior = Status;
        Status = novoStatus;

        _historicoStatus.Add(new HistoricoStatus(
            geradorId.NovoId(),
            Id,
            anterior,
            novoStatus,
            agora,
            usuarioResponsavelId,
            observacao));

        RegistrarEvento(new StatusDoChamadoAlterado(
            Id, Numero, ClienteId, anterior, novoStatus, agora));
    }

    /// <summary>RN0033: o cliente so cancela enquanto o chamado esta ABERTO ou AGENDADO.</summary>
    public bool PodeSerCancelado() =>
        Status is StatusChamado.Aberto or StatusChamado.Agendado;

    /// <summary>RN0033 e RN0034: cancelamento pelo proprio cliente.</summary>
    public void Cancelar(Guid usuarioResponsavelId, DateTimeOffset agora, IGeradorId geradorId)
    {
        if (!PodeSerCancelado())
        {
            throw new ExcecaoDeDominio(
                "O cliente so pode cancelar o chamado com status ABERTO ou AGENDADO. "
                + "Depois do inicio do atendimento, o cancelamento deve ser solicitado ao administrador.",
                "RN0033");
        }

        AlterarStatus(
            StatusChamado.Cancelado,
            usuarioResponsavelId,
            "Cancelado pelo cliente.",
            agora,
            geradorId);
    }

    /// <summary>
    /// RN0033: cancelamento administrativo, permitido tambem depois do inicio do atendimento.
    /// </summary>
    public void CancelarPeloAdministrador(
        Guid administradorId,
        string motivo,
        DateTimeOffset agora,
        IGeradorId geradorId) =>
        AlterarStatus(
            StatusChamado.Cancelado,
            administradorId,
            Garantir.TextoComTamanhoMaximo(motivo, 500, "motivo do cancelamento", "RF0045"),
            agora,
            geradorId);

    /// <summary>
    /// RN0035: reabertura em ate 7 dias corridos apos a conclusao. O chamado volta para
    /// EM ANALISE e mantem o vinculo com o chamado original.
    /// </summary>
    public void SolicitarReabertura(
        Guid usuarioResponsavelId,
        DateTimeOffset dataHoraConclusao,
        DateTimeOffset agora,
        IGeradorId geradorId)
    {
        if (Status != StatusChamado.Concluido)
        {
            throw new ExcecaoDeDominio(
                "Somente chamado com status CONCLUIDO pode ser reaberto.",
                "RN0035");
        }

        if (agora > dataHoraConclusao.AddDays(PrazoDeReaberturaEmDias))
        {
            throw new ExcecaoDeDominio(
                $"O prazo de {PrazoDeReaberturaEmDias} dias corridos para reabertura ja expirou.",
                "RN0035");
        }

        AlterarStatus(
            StatusChamado.EmAnalise,
            usuarioResponsavelId,
            "Reabertura solicitada pelo cliente: o problema persistiu.",
            agora,
            geradorId);
    }

    /// <summary>
    /// RN0022 e RN0023: o tecnico precisa ter a categoria entre suas especialidades e cobrir o
    /// CEP do imovel. RF0047 e RF0048 usam o mesmo caminho.
    /// </summary>
    public void AtribuirTecnico(
        Tecnico tecnico,
        Guid categoriaServicoId,
        string cepDoImovel,
        Guid administradorId,
        DateTimeOffset agora,
        IGeradorId geradorId)
    {
        Garantir.NaoNulo(tecnico, "tecnico", "RF0047");

        if (Status is StatusChamado.Cancelado or StatusChamado.Concluido)
        {
            throw new ExcecaoDeDominio(
                $"Nao e possivel atribuir tecnico a um chamado com status {Status}.",
                "RF0047");
        }

        if (!tecnico.Ativo)
        {
            throw new ExcecaoDeDominio(
                "Tecnico inativo nao pode receber chamados.",
                "RF0023");
        }

        if (!tecnico.AtendeCategoria(categoriaServicoId))
        {
            throw new ExcecaoDeDominio(
                "O tecnico nao possui a categoria do chamado entre suas especialidades.",
                "RN0022");
        }

        if (!tecnico.AtendeCep(cepDoImovel))
        {
            throw new ExcecaoDeDominio(
                "O imovel do chamado esta fora da area de atendimento do tecnico.",
                "RN0023");
        }

        var eraReatribuicao = TecnicoId is not null;
        TecnicoId = tecnico.Id;

        if (Status == StatusChamado.Aberto)
        {
            AlterarStatus(
                StatusChamado.EmAnalise,
                administradorId,
                eraReatribuicao
                    ? $"Chamado reatribuido ao tecnico {tecnico.CodigoTecnico}."
                    : $"Chamado atribuido ao tecnico {tecnico.CodigoTecnico}.",
                agora,
                geradorId);
        }
        else
        {
            _historicoStatus.Add(new HistoricoStatus(
                geradorId.NovoId(),
                Id,
                Status,
                Status,
                agora,
                administradorId,
                eraReatribuicao
                    ? $"Chamado reatribuido ao tecnico {tecnico.CodigoTecnico}."
                    : $"Chamado atribuido ao tecnico {tecnico.CodigoTecnico}."));
        }
    }

    /// <summary>RF0085: o chamado de garantia nasce sem tecnico quando o original nao serve mais.</summary>
    public void DeixarSemTecnico() => TecnicoId = null;

    /// <summary>RF0046: classificacao manual da urgencia pelo administrador.</summary>
    public void ClassificarUrgencia(Urgencia urgencia)
    {
        if (Status is StatusChamado.Cancelado or StatusChamado.Concluido)
        {
            throw new ExcecaoDeDominio(
                $"Nao e possivel classificar a urgencia de um chamado com status {Status}.",
                "RF0046");
        }

        Urgencia = urgencia;
    }

    /// <summary>RF0042 e RF0057, com o limite da RNF0043 aplicado por origem (decisao D09).</summary>
    public void AdicionarAnexo(Anexo anexo)
    {
        Garantir.NaoNulo(anexo, "anexo", "RF0042");

        if (anexo.ChamadoId != Id)
        {
            throw new ExcecaoDeDominio("O anexo pertence a outro chamado.", "RF0042");
        }

        // RNF0043: o limite e por chamado, somando a abertura e a conclusao.
        if (_anexos.Count >= MaximoDeAnexos)
        {
            throw new ExcecaoDeDominio(
                $"Limite de {MaximoDeAnexos} arquivos por chamado atingido.",
                "RNF0043");
        }

        _anexos.Add(anexo);
    }

    /// <summary>RN0032: a categoria que exige foto precisa de ao menos uma imagem na abertura.</summary>
    public bool PossuiFotoDeAbertura() =>
        _anexos.Any(a => a.Origem == OrigemAnexo.ChamadoAbertura && a.EhFoto());

    /// <summary>RF0051: registra a proposta de agendamento e leva o chamado para AGENDADO.</summary>
    public void AdicionarAgendamento(
        Agendamento agendamento,
        Guid usuarioResponsavelId,
        DateTimeOffset agora,
        IGeradorId geradorId)
    {
        Garantir.NaoNulo(agendamento, "agendamento", "RF0051");

        if (agendamento.ChamadoId != Id)
        {
            throw new ExcecaoDeDominio("O agendamento pertence a outro chamado.", "RF0051");
        }

        if (TecnicoId is null)
        {
            throw new ExcecaoDeDominio(
                "O chamado precisa de um tecnico atribuido antes do agendamento.",
                "RF0051");
        }

        foreach (var anterior in _agendamentos.Where(a => a.OcupaAgenda()))
        {
            anterior.MarcarComoReagendado();
        }

        _agendamentos.Add(agendamento);

        if (Status == StatusChamado.EmAnalise)
        {
            AlterarStatus(
                StatusChamado.Agendado,
                usuarioResponsavelId,
                $"Atendimento proposto para {agendamento.DataHoraProposta:dd/MM/yyyy HH:mm}.",
                agora,
                geradorId);
        }
        else if (Status == StatusChamado.Agendado)
        {
            AlterarStatus(
                StatusChamado.Agendado,
                usuarioResponsavelId,
                $"Atendimento reagendado para {agendamento.DataHoraProposta:dd/MM/yyyy HH:mm}.",
                agora,
                geradorId);
        }
        else
        {
            throw new ExcecaoDeDominio(
                $"Nao e possivel agendar um chamado com status {Status}.",
                "RN0034");
        }
    }

    /// <summary>RF0054: o inicio do atendimento leva o chamado para EM ATENDIMENTO.</summary>
    public void RegistrarInicioDeAtendimento(
        Guid tecnicoId,
        DateTimeOffset agora,
        IGeradorId geradorId)
    {
        if (TecnicoId != tecnicoId)
        {
            throw new ExcecaoDeDominio(
                "Somente o tecnico atribuido pode iniciar o atendimento.",
                "RF0054");
        }

        AlterarStatus(
            StatusChamado.EmAtendimento,
            tecnicoId,
            "Atendimento iniciado no local do imovel.",
            agora,
            geradorId);
    }

    /// <summary>Agendamento em vigor, se houver (RF0052, RF0053).</summary>
    public Agendamento? AgendamentoVigente() =>
        _agendamentos.LastOrDefault(a => a.OcupaAgenda());

    public bool PertenceAoCliente(Guid clienteId) => ClienteId == clienteId;

    public bool EstaAtribuidoAo(Guid tecnicoId) => TecnicoId == tecnicoId;

    /// <summary>RF0085: vincula a garantia acionada que originou este chamado.</summary>
    public void VincularGarantia(Guid garantiaId) => GarantiaId = garantiaId;
}
