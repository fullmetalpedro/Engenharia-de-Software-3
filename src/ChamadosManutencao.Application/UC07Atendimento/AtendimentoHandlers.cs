using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Application.UC07Atendimento;

/// <summary>
/// Registra o inicio do atendimento no local do imovel.
/// Requisitos: RF0054, RN0034, RNF0041.
/// Caso de uso: UC07.
/// </summary>
public sealed class IniciarAtendimentoHandler
{
    private readonly IChamadoRepositorio _chamados;
    private readonly IAtendimentoRepositorio _atendimentos;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public IniciarAtendimentoHandler(
        IChamadoRepositorio chamados,
        IAtendimentoRepositorio atendimentos,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _chamados = chamados;
        _atendimentos = atendimentos;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<AtendimentoDto> ExecutarAsync(
        Guid chamadoId,
        CancellationToken cancellationToken = default)
    {
        var tecnicoId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        var chamado = await _chamados.ObterCompletoAsync(chamadoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", chamadoId);

        if (!chamado.EstaAtribuidoAo(tecnicoId))
        {
            throw new AcessoNegadoException("Somente o tecnico atribuido pode iniciar o atendimento.");
        }

        var existente = await _atendimentos.ObterPorChamadoAsync(chamadoId, cancellationToken);

        if (existente is not null)
        {
            throw new ConflitoException("Este chamado ja possui atendimento registrado.", "RF0054");
        }

        var atendimento = new Atendimento(_geradorId.NovoId(), chamadoId, tecnicoId, _relogio.Agora);

        chamado.RegistrarInicioDeAtendimento(tecnicoId, _relogio.Agora, _geradorId);

        _atendimentos.Adicionar(atendimento);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return Mapear(atendimento);
    }

    internal static AtendimentoDto Mapear(Atendimento atendimento) => new(
        atendimento.Id,
        atendimento.ChamadoId,
        atendimento.TecnicoId,
        atendimento.DataHoraInicio,
        atendimento.DataHoraConclusao,
        atendimento.RelatoTecnico,
        atendimento.GarantiaId);
}

/// <summary>
/// Registra o orcamento de pecas e mao de obra.
/// Requisitos: RF0055, RN0043.
/// Caso de uso: UC07.
/// </summary>
public sealed class RegistrarOrcamentoHandler
{
    private readonly IAtendimentoRepositorio _atendimentos;
    private readonly IChamadoRepositorio _chamados;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public RegistrarOrcamentoHandler(
        IAtendimentoRepositorio atendimentos,
        IChamadoRepositorio chamados,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _atendimentos = atendimentos;
        _chamados = chamados;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<OrcamentoDto> ExecutarAsync(
        Guid atendimentoId,
        RegistrarOrcamentoCommand comando,
        CancellationToken cancellationToken = default)
    {
        var tecnicoId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        var atendimento = await _atendimentos.ObterCompletoAsync(atendimentoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Atendimento", atendimentoId);

        if (atendimento.TecnicoId != tecnicoId)
        {
            throw new AcessoNegadoException("Somente o tecnico atribuido pode registrar o orcamento.");
        }

        var orcamentoId = _geradorId.NovoId();

        var itens = comando.Itens
            .Select(item => new ItemOrcamento(
                _geradorId.NovoId(),
                orcamentoId,
                item.Descricao,
                item.Tipo,
                item.Quantidade,
                item.ValorUnitario))
            .ToList();

        // RN0043: o prazo de 48 horas nasce do proprio orcamento.
        var orcamento = new Orcamento(orcamentoId, atendimentoId, _relogio.Agora, itens);

        atendimento.RegistrarOrcamento(orcamento);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return ConsultarOrcamentoHandler.Mapear(orcamento);
    }
}

/// <summary>
/// Consulta o orcamento vigente do atendimento.
/// Requisitos: RF0055.
/// Caso de uso: UC07.
/// </summary>
public sealed class ConsultarOrcamentoHandler
{
    private readonly IAtendimentoRepositorio _atendimentos;
    private readonly IContextoDeLeitura _leitura;
    private readonly IUsuarioAtual _usuarioAtual;

    public ConsultarOrcamentoHandler(
        IAtendimentoRepositorio atendimentos,
        IContextoDeLeitura leitura,
        IUsuarioAtual usuarioAtual)
    {
        _atendimentos = atendimentos;
        _leitura = leitura;
        _usuarioAtual = usuarioAtual;
    }

    public async Task<IReadOnlyCollection<OrcamentoDto>> ExecutarAsync(
        Guid atendimentoId,
        CancellationToken cancellationToken = default)
    {
        var atendimento = await _atendimentos.ObterCompletoAsync(atendimentoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Atendimento", atendimentoId);

        var clienteId = await _leitura.Chamados
            .Where(c => c.Id == atendimento.ChamadoId)
            .Select(c => c.ClienteId)
            .SingleAsync(cancellationToken);

        var usuarioId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        if (!Autorizacao.EhAdministrador(_usuarioAtual)
            && usuarioId != clienteId
            && usuarioId != atendimento.TecnicoId)
        {
            throw new AcessoNegadoException("Este atendimento pertence a outro usuario.");
        }

        return atendimento.Orcamentos.Select(Mapear).ToList();
    }

    internal static OrcamentoDto Mapear(Orcamento orcamento) => new(
        orcamento.Id,
        orcamento.AtendimentoId,
        orcamento.DataHoraRegistro,
        orcamento.PrazoAprovacao,
        orcamento.Status,
        orcamento.DataHoraDecisao,
        orcamento.CalcularValorPecas(),
        orcamento.CalcularValorMaoObra(),
        orcamento.ValorTotal(),
        orcamento.Itens
            .Select(item => new ItemOrcamentoDto(
                item.Id,
                item.Descricao,
                item.Tipo,
                item.Quantidade,
                item.ValorUnitario,
                item.CalcularSubtotal()))
            .ToList());
}

/// <summary>
/// Registra a decisao do cliente sobre o orcamento.
/// Requisitos: RF0056, RN0042, RN0043, RN0034.
/// Caso de uso: UC07.
/// </summary>
public sealed class DecidirOrcamentoHandler
{
    private readonly IAtendimentoRepositorio _atendimentos;
    private readonly IChamadoRepositorio _chamados;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public DecidirOrcamentoHandler(
        IAtendimentoRepositorio atendimentos,
        IChamadoRepositorio chamados,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _atendimentos = atendimentos;
        _chamados = chamados;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<OrcamentoDto> ExecutarAsync(
        Guid orcamentoId,
        DecisaoDeOrcamentoCommand comando,
        CancellationToken cancellationToken = default)
    {
        var atendimento = await _atendimentos.ObterPorOrcamentoAsync(orcamentoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Orcamento", orcamentoId);

        var chamado = await _chamados.ObterCompletoAsync(atendimento.ChamadoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", atendimento.ChamadoId);

        Autorizacao.ExigirDono(_usuarioAtual, chamado.ClienteId);

        var orcamento = atendimento.Orcamentos.Single(o => o.Id == orcamentoId);

        if (comando.Decisao == "Aprovado")
        {
            orcamento.Aprovar(_relogio.Agora);
        }
        else
        {
            orcamento.Recusar(_relogio.Agora);

            // RN0042: a recusa cancela o chamado, salvo pedido de novo orcamento.
            if (!comando.SolicitarNovoOrcamento)
            {
                chamado.AlterarStatus(
                    StatusChamado.Cancelado,
                    chamado.ClienteId,
                    "Orcamento recusado pelo cliente.",
                    _relogio.Agora,
                    _geradorId);
            }
        }

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return ConsultarOrcamentoHandler.Mapear(orcamento);
    }
}

/// <summary>
/// Registra a conclusao do atendimento, criando a garantia e, quando cabe, a fatura.
/// Requisitos: RF0057, RF0082, RN0034, RN0043, RN0051, RN0071, RNF0043.
/// Caso de uso: UC07.
/// </summary>
public sealed class ConcluirAtendimentoHandler
{
    private readonly IAtendimentoRepositorio _atendimentos;
    private readonly IChamadoRepositorio _chamados;
    private readonly IFaturaRepositorio _faturas;
    private readonly ITecnicoRepositorio _tecnicos;
    private readonly IContextoDeLeitura _leitura;
    private readonly IGeradorDeSequencias _sequencias;
    private readonly IArmazenamentoArquivos _armazenamento;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public ConcluirAtendimentoHandler(
        IAtendimentoRepositorio atendimentos,
        IChamadoRepositorio chamados,
        IFaturaRepositorio faturas,
        ITecnicoRepositorio tecnicos,
        IContextoDeLeitura leitura,
        IGeradorDeSequencias sequencias,
        IArmazenamentoArquivos armazenamento,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _atendimentos = atendimentos;
        _chamados = chamados;
        _faturas = faturas;
        _tecnicos = tecnicos;
        _leitura = leitura;
        _sequencias = sequencias;
        _armazenamento = armazenamento;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<ResultadoDaConclusaoDto> ExecutarAsync(
        Guid atendimentoId,
        ConcluirAtendimentoCommand comando,
        IReadOnlyCollection<ArquivoRecebido> fotosDaConclusao,
        CancellationToken cancellationToken = default)
    {
        var tecnicoId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        var atendimento = await _atendimentos.ObterCompletoAsync(atendimentoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Atendimento", atendimentoId);

        if (atendimento.TecnicoId != tecnicoId)
        {
            throw new AcessoNegadoException("Somente o tecnico atribuido pode concluir o atendimento.");
        }

        var chamado = await _chamados.ObterCompletoAsync(atendimento.ChamadoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", atendimento.ChamadoId);

        // RF0057: fotos do servico finalizado, com a cota propria da decisao D09.
        foreach (var arquivo in fotosDaConclusao)
        {
            var anexoId = _geradorId.NovoId();
            var caminho = $"chamados/{chamado.Id}/{anexoId}{Path.GetExtension(arquivo.NomeArquivo)}";

            var anexo = Anexo.ParaChamado(
                anexoId,
                chamado.Id,
                arquivo.NomeArquivo,
                arquivo.TipoMime,
                arquivo.TamanhoBytes,
                _relogio.Agora,
                caminho,
                OrigemAnexo.ConclusaoAtendimento);

            chamado.AdicionarAnexo(anexo);

            await _armazenamento.GravarAsync(caminho, arquivo.Conteudo, cancellationToken);
        }

        // A conclusao publica AtendimentoConcluido (RN0051) e devolve a garantia de 90 dias.
        var garantia = atendimento.Concluir(
            comando.RelatoTecnico,
            chamado.ClienteId,
            chamado.ChamadoDeGarantia,
            _relogio.Agora,
            _geradorId);

        _atendimentos.AdicionarGarantia(garantia);

        chamado.AlterarStatus(
            StatusChamado.Concluido,
            tecnicoId,
            "Atendimento concluido pelo tecnico.",
            _relogio.Agora,
            _geradorId);

        Guid? faturaId = null;
        long? numeroDaFatura = null;
        decimal? valorDaFatura = null;

        // RN0071: chamado de garantia nao gera fatura.
        if (!chamado.ChamadoDeGarantia)
        {
            var fatura = await GerarFaturaAsync(atendimento, chamado.Id, chamado.ClienteId, chamado.ImovelId, tecnicoId, cancellationToken);

            faturaId = fatura.Id;
            numeroDaFatura = fatura.Numero;
            valorDaFatura = fatura.ValorTotal;
        }

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return new ResultadoDaConclusaoDto(
            IniciarAtendimentoHandler.Mapear(atendimento),
            new GarantiaDto(
                garantia.Id,
                garantia.AtendimentoId,
                garantia.DataInicio,
                garantia.DataFim,
                garantia.PrazoDias,
                garantia.EstaVigente(_relogio.Agora)),
            faturaId,
            numeroDaFatura,
            valorDaFatura);
    }

    /// <summary>
    /// RN0071: pecas + mao de obra do orcamento aprovado + taxa de deslocamento da area do
    /// tecnico que cobre o CEP do imovel (decisao D10). Sem orcamento aprovado, a fatura sai
    /// apenas com a taxa de deslocamento (decisao D11).
    /// </summary>
    private async Task<Fatura> GerarFaturaAsync(
        Atendimento atendimento,
        Guid chamadoId,
        Guid clienteId,
        Guid imovelId,
        Guid tecnicoId,
        CancellationToken cancellationToken)
    {
        var aprovado = atendimento.OrcamentoAprovado();

        var valorPecas = aprovado?.CalcularValorPecas() ?? 0m;
        var valorMaoObra = aprovado?.CalcularValorMaoObra() ?? 0m;

        var cep = await _leitura.Imoveis
            .Where(i => i.Id == imovelId)
            .Select(i => i.Cep)
            .SingleAsync(cancellationToken);

        var tecnico = await _tecnicos.ObterCompletoAsync(tecnicoId, cancellationToken);
        var taxa = tecnico?.AreaQueAtende(cep)?.TaxaDeslocamento ?? 0m;

        var numero = await _sequencias.ProximoNumeroDeFaturaAsync(cancellationToken);

        var fatura = new Fatura(
            _geradorId.NovoId(),
            numero,
            atendimento.Id,
            clienteId,
            chamadoId,
            _relogio.Agora,
            valorPecas,
            valorMaoObra,
            taxa);

        _faturas.Adicionar(fatura);

        return fatura;
    }
}
