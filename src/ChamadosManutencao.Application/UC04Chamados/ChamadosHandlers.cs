using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Application.UC04Chamados;

/// <summary>
/// Abre um chamado de manutencao.
/// Requisitos: RF0041, RF0042, RN0031, RN0032, RN0034, RNF0041, RNF0042, RNF0043.
/// Caso de uso: UC04.
/// </summary>
public sealed class AbrirChamadoHandler
{
    private readonly IChamadoRepositorio _chamados;
    private readonly IClienteRepositorio _clientes;
    private readonly ICatalogoRepositorio _catalogo;
    private readonly IGeradorDeSequencias _sequencias;
    private readonly IArmazenamentoArquivos _armazenamento;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public AbrirChamadoHandler(
        IChamadoRepositorio chamados,
        IClienteRepositorio clientes,
        ICatalogoRepositorio catalogo,
        IGeradorDeSequencias sequencias,
        IArmazenamentoArquivos armazenamento,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _chamados = chamados;
        _clientes = clientes;
        _catalogo = catalogo;
        _sequencias = sequencias;
        _armazenamento = armazenamento;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<ChamadoResumoDto> ExecutarAsync(
        AbrirChamadoCommand comando,
        IReadOnlyCollection<ArquivoRecebido> anexos,
        CancellationToken cancellationToken = default)
    {
        var clienteId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        var cliente = await _clientes.ObterComImoveisAsync(clienteId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", clienteId);

        if (!cliente.Ativo)
        {
            throw new ConflitoException(
                "Cliente inativo nao pode abrir chamados.",
                "RF0013");
        }

        if (!cliente.PossuiImovel(comando.ImovelId))
        {
            throw new RecursoNaoEncontradoException("Imovel", comando.ImovelId);
        }

        var tipoServico = await _catalogo.ObterTipoServicoAsync(comando.TipoServicoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Tipo de servico", comando.TipoServicoId);

        // Decisao D19: os dois identificadores sao aceitos, e a coerencia entre eles e validada.
        if (tipoServico.CategoriaServicoId != comando.CategoriaServicoId)
        {
            throw new ValidacaoException(
                nameof(comando.TipoServicoId),
                "O tipo de servico informado nao pertence a categoria informada.");
        }

        if (!tipoServico.Ativo)
        {
            throw new ConflitoException("O tipo de servico informado esta inativo.");
        }

        var categoria = await _catalogo.ObterCategoriaAsync(comando.CategoriaServicoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Categoria de servico", comando.CategoriaServicoId);

        // RN0032: categoria que exige foto bloqueia a abertura sem ao menos uma imagem.
        if (categoria.ExigeFoto && !anexos.Any(a => a.TipoMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflitoException(
                $"A categoria '{categoria.Nome}' exige ao menos uma foto do equipamento com problema.",
                "RN0032");
        }

        if (anexos.Count > Chamado.MaximoDeAnexos)
        {
            throw new ConflitoException(
                $"Limite de {Chamado.MaximoDeAnexos} arquivos por chamado.",
                "RNF0043");
        }

        // RN0031: categoria de risco com indicacao de risco recebe urgencia ALTA;
        // fora disso o chamado nasce MEDIA e so o administrador reclassifica (RF0046).
        var urgencia = PoliticaUrgencia.Definir(categoria, comando.IndicacaoDeRisco);

        // RNF0042: numero sequencial unico vindo da sequence.
        var numero = await _sequencias.ProximoNumeroDeChamadoAsync(cancellationToken);
        var chamadoId = _geradorId.NovoId();

        var chamado = Chamado.Abrir(
            chamadoId,
            numero,
            clienteId,
            comando.ImovelId,
            comando.TipoServicoId,
            comando.DescricaoProblema,
            urgencia,
            _relogio.Agora,
            _geradorId);

        foreach (var arquivo in anexos)
        {
            var anexoId = _geradorId.NovoId();
            var caminho = $"chamados/{chamadoId}/{anexoId}{Path.GetExtension(arquivo.NomeArquivo)}";

            var anexo = Anexo.ParaChamado(
                anexoId,
                chamadoId,
                arquivo.NomeArquivo,
                arquivo.TipoMime,
                arquivo.TamanhoBytes,
                _relogio.Agora,
                caminho,
                OrigemAnexo.ChamadoAbertura);

            chamado.AdicionarAnexo(anexo);

            await _armazenamento.GravarAsync(caminho, arquivo.Conteudo, cancellationToken);
        }

        _chamados.Adicionar(chamado);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return new ChamadoResumoDto(
            chamado.Id,
            chamado.Numero,
            chamado.DataHoraAbertura,
            chamado.Status,
            chamado.Urgencia,
            chamado.ChamadoDeGarantia,
            chamado.ClienteId,
            chamado.ImovelId,
            chamado.TipoServicoId,
            tipoServico.Nome,
            categoria.Nome,
            chamado.TecnicoId,
            Tecnico: null);
    }
}

/// <summary>
/// Anexa midia ao chamado ja aberto.
/// Requisitos: RF0042, RNF0043.
/// Caso de uso: UC04.
/// </summary>
public sealed class AnexarMidiaAoChamadoHandler
{
    private readonly IChamadoRepositorio _chamados;
    private readonly IArmazenamentoArquivos _armazenamento;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public AnexarMidiaAoChamadoHandler(
        IChamadoRepositorio chamados,
        IArmazenamentoArquivos armazenamento,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _chamados = chamados;
        _armazenamento = armazenamento;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<AnexoDto> ExecutarAsync(
        Guid chamadoId,
        ArquivoRecebido arquivo,
        CancellationToken cancellationToken = default)
    {
        var chamado = await _chamados.ObterCompletoAsync(chamadoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", chamadoId);

        Autorizacao.ExigirDono(_usuarioAtual, chamado.ClienteId);

        var anexoId = _geradorId.NovoId();
        var caminho = $"chamados/{chamadoId}/{anexoId}{Path.GetExtension(arquivo.NomeArquivo)}";

        var anexo = Anexo.ParaChamado(
            anexoId,
            chamadoId,
            arquivo.NomeArquivo,
            arquivo.TipoMime,
            arquivo.TamanhoBytes,
            _relogio.Agora,
            caminho,
            OrigemAnexo.ChamadoAbertura);

        // O limite da RNF0043 e verificado antes da gravacao em disco.
        chamado.AdicionarAnexo(anexo);

        await _armazenamento.GravarAsync(caminho, arquivo.Conteudo, cancellationToken);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return new AnexoDto(
            anexo.Id,
            anexo.NomeArquivo,
            anexo.TipoMime,
            anexo.TamanhoBytes,
            anexo.DataHoraUpload,
            anexo.Origem);
    }
}

/// <summary>
/// Cancela o chamado a pedido do cliente.
/// Requisitos: RF0045, RN0033, RN0034, RNF0041.
/// Caso de uso: UC04.
/// </summary>
public sealed class CancelarChamadoHandler
{
    private readonly IChamadoRepositorio _chamados;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public CancelarChamadoHandler(
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

    public async Task ExecutarAsync(Guid chamadoId, CancellationToken cancellationToken = default)
    {
        var chamado = await _chamados.ObterCompletoAsync(chamadoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", chamadoId);

        Autorizacao.ExigirDono(_usuarioAtual, chamado.ClienteId);

        chamado.Cancelar(chamado.ClienteId, _relogio.Agora, _geradorId);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
    }
}

/// <summary>
/// Solicita a reabertura de um chamado concluido.
/// Requisitos: RN0034, RN0035, RNF0041.
/// Caso de uso: UC04.
/// </summary>
public sealed class ReabrirChamadoHandler
{
    private readonly IChamadoRepositorio _chamados;
    private readonly IContextoDeLeitura _leitura;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public ReabrirChamadoHandler(
        IChamadoRepositorio chamados,
        IContextoDeLeitura leitura,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _chamados = chamados;
        _leitura = leitura;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task ExecutarAsync(Guid chamadoId, CancellationToken cancellationToken = default)
    {
        var chamado = await _chamados.ObterCompletoAsync(chamadoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", chamadoId);

        Autorizacao.ExigirDono(_usuarioAtual, chamado.ClienteId);

        // RN0035 conta os 7 dias a partir da conclusao. Um chamado reaberto e reconcluido tem
        // mais de um atendimento encerrado, entao vale o mais recente.
        var conclusao = await _leitura.Atendimentos
            .Where(a => a.ChamadoId == chamadoId && a.DataHoraConclusao != null)
            .OrderByDescending(a => a.DataHoraConclusao)
            .Select(a => (DateTimeOffset?)a.DataHoraConclusao!.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (conclusao is null)
        {
            throw new ConflitoException(
                "Nao ha atendimento concluido para este chamado.",
                "RN0035");
        }

        chamado.SolicitarReabertura(chamado.ClienteId, conclusao.Value, _relogio.Agora, _geradorId);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
    }
}
