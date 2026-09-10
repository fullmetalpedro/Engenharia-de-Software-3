using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Pessoas;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Application.UC02Tecnicos;

/// <summary>
/// Cadastra um tecnico com especialidades e areas de atendimento.
/// Requisitos: RF0021, RF0026, RF0027, RN0021, RN0023, RNF0021, RNF0022, RNF0031.
/// Caso de uso: UC02.
/// </summary>
public sealed class CadastrarTecnicoHandler
{
    private readonly ITecnicoRepositorio _tecnicos;
    private readonly IUsuarioRepositorio _usuarios;
    private readonly ICatalogoRepositorio _catalogo;
    private readonly IGeradorDeSequencias _sequencias;
    private readonly IServicoDeHashDeSenha _hash;
    private readonly IGeradorId _geradorId;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public CadastrarTecnicoHandler(
        ITecnicoRepositorio tecnicos,
        IUsuarioRepositorio usuarios,
        ICatalogoRepositorio catalogo,
        IGeradorDeSequencias sequencias,
        IServicoDeHashDeSenha hash,
        IGeradorId geradorId,
        IUnitOfWork unidadeDeTrabalho)
    {
        _tecnicos = tecnicos;
        _usuarios = usuarios;
        _catalogo = catalogo;
        _sequencias = sequencias;
        _hash = hash;
        _geradorId = geradorId;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<TecnicoDetalheDto> ExecutarAsync(
        CadastrarTecnicoCommand comando,
        CancellationToken cancellationToken = default)
    {
        var cpf = new string(comando.Cpf.Where(char.IsDigit).ToArray());
        var email = comando.Email.Trim().ToLowerInvariant();

        if (await _usuarios.ExisteCpfAsync(cpf, cancellationToken: cancellationToken))
        {
            throw new ConflitoException($"Ja existe um usuario cadastrado com o CPF {cpf}.", "RN0021");
        }

        if (await _usuarios.ExisteEmailAsync(email, cancellationToken: cancellationToken))
        {
            throw new ConflitoException($"Ja existe um usuario cadastrado com o e-mail {email}.", "RN0021");
        }

        var especialidades = await ObterEspecialidadesAsync(comando.EspecialidadeIds, cancellationToken);

        var codigo = await _sequencias.ProximoCodigoDeTecnicoAsync(cancellationToken);
        var tecnicoId = _geradorId.NovoId();

        var tecnico = new Tecnico(
            tecnicoId,
            codigo,
            comando.NomeCompleto,
            cpf,
            email,
            comando.Telefone,
            _hash.GerarHash(comando.Senha),
            especialidades);

        tecnico.DefinirAreasAtendimento(comando.AreasAtendimento
            .Select(area => new AreaAtendimento(
                _geradorId.NovoId(),
                tecnicoId,
                area.Bairro,
                area.CepInicial,
                area.CepFinal,
                area.TaxaDeslocamento))
            .ToList());

        _tecnicos.Adicionar(tecnico);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return tecnico.ParaDetalhe();
    }

    private async Task<List<CategoriaServico>> ObterEspecialidadesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var categorias = await _catalogo.ObterCategoriasPorIdsAsync(ids, cancellationToken);

        var faltantes = ids.Except(categorias.Select(c => c.Id)).ToList();

        if (faltantes.Count > 0)
        {
            throw new RecursoNaoEncontradoException(
                "Categoria de servico",
                string.Join(", ", faltantes));
        }

        return categorias.ToList();
    }
}

/// <summary>
/// Consulta tecnicos por filtro, incluindo especialidade e area de atendimento.
/// Requisitos: RF0025, RNF0011.
/// Caso de uso: UC02.
/// </summary>
public sealed class ConsultarTecnicosHandler
{
    private readonly IContextoDeLeitura _leitura;

    public ConsultarTecnicosHandler(IContextoDeLeitura leitura) => _leitura = leitura;

    public async Task<IReadOnlyCollection<TecnicoResumoDto>> ExecutarAsync(
        FiltroDeTecnicos filtro,
        CancellationToken cancellationToken = default)
    {
        var consulta = _leitura.Tecnicos;

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            consulta = consulta.Where(t => EF.Functions.Like(t.NomeCompleto.ToLower(), $"%{filtro.Nome!.ToLower()}%"));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Cpf))
        {
            var cpf = new string(filtro.Cpf.Where(char.IsDigit).ToArray());
            consulta = consulta.Where(t => t.Cpf == cpf);
        }

        if (filtro.EspecialidadeId is not null)
        {
            consulta = consulta.Where(t => EF.Property<ICollection<CategoriaServico>>(t, "_especialidades")
                .Any(e => e.Id == filtro.EspecialidadeId));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Bairro))
        {
            consulta = consulta.Where(t => _leitura.AreasAtendimento
                .Any(a => a.TecnicoId == t.Id && EF.Functions.Like(a.Bairro.ToLower(), $"%{filtro.Bairro!.ToLower()}%")));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Cep))
        {
            var cep = new string(filtro.Cep.Where(char.IsDigit).ToArray());
            consulta = consulta.Where(t => _leitura.AreasAtendimento
                .Any(a => a.TecnicoId == t.Id
                    && string.Compare(a.CepInicial, cep) <= 0
                    && string.Compare(a.CepFinal, cep) >= 0));
        }

        if (filtro.Ativo is not null)
        {
            consulta = consulta.Where(t => t.Ativo == filtro.Ativo);
        }

        var itens = await consulta
            .OrderBy(t => t.NomeCompleto)
            .Select(t => new TecnicoResumoDto(
                t.Id,
                t.CodigoTecnico,
                t.NomeCompleto,
                t.Cpf,
                t.Email,
                t.Telefone,
                t.Ativo))
            .ToListAsync(cancellationToken);

        return itens;
    }
}

/// <summary>
/// Consulta um tecnico pelo identificador, com especialidades, areas e documentos.
/// Requisitos: RF0025.
/// Caso de uso: UC02.
/// </summary>
public sealed class ObterTecnicoHandler
{
    private readonly ITecnicoRepositorio _tecnicos;

    public ObterTecnicoHandler(ITecnicoRepositorio tecnicos) => _tecnicos = tecnicos;

    public async Task<TecnicoDetalheDto> ExecutarAsync(
        Guid tecnicoId,
        CancellationToken cancellationToken = default)
    {
        var tecnico = await _tecnicos.ObterCompletoAsync(tecnicoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Tecnico", tecnicoId);

        return tecnico.ParaDetalhe();
    }
}

/// <summary>
/// Altera dados, situacao, especialidades e areas do tecnico.
/// Requisitos: RF0022, RF0023, RF0024, RF0026, RF0027, RN0021, RN0023.
/// Caso de uso: UC02.
/// </summary>
public sealed class ManterTecnicoHandler
{
    private readonly ITecnicoRepositorio _tecnicos;
    private readonly IUsuarioRepositorio _usuarios;
    private readonly ICatalogoRepositorio _catalogo;
    private readonly IGeradorId _geradorId;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public ManterTecnicoHandler(
        ITecnicoRepositorio tecnicos,
        IUsuarioRepositorio usuarios,
        ICatalogoRepositorio catalogo,
        IGeradorId geradorId,
        IUnitOfWork unidadeDeTrabalho)
    {
        _tecnicos = tecnicos;
        _usuarios = usuarios;
        _catalogo = catalogo;
        _geradorId = geradorId;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<TecnicoResumoDto> AlterarAsync(
        Guid tecnicoId,
        AlterarTecnicoCommand comando,
        CancellationToken cancellationToken = default)
    {
        var tecnico = await _tecnicos.ObterPorIdAsync(tecnicoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Tecnico", tecnicoId);

        var email = comando.Email.Trim().ToLowerInvariant();

        if (await _usuarios.ExisteEmailAsync(email, tecnicoId, cancellationToken))
        {
            throw new ConflitoException($"Ja existe um usuario cadastrado com o e-mail {email}.");
        }

        tecnico.AlterarDados(comando.NomeCompleto, email, comando.Telefone);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return tecnico.ParaResumo();
    }

    public async Task<TecnicoResumoDto> AlterarSituacaoAsync(
        Guid tecnicoId,
        bool ativar,
        CancellationToken cancellationToken = default)
    {
        var tecnico = await _tecnicos.ObterPorIdAsync(tecnicoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Tecnico", tecnicoId);

        if (ativar)
        {
            tecnico.Ativar();
        }
        else
        {
            tecnico.Inativar();
        }

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return tecnico.ParaResumo();
    }

    public async Task<TecnicoDetalheDto> DefinirEspecialidadesAsync(
        Guid tecnicoId,
        DefinirEspecialidadesCommand comando,
        CancellationToken cancellationToken = default)
    {
        var tecnico = await _tecnicos.ObterCompletoAsync(tecnicoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Tecnico", tecnicoId);

        var categorias = await _catalogo.ObterCategoriasPorIdsAsync(
            comando.EspecialidadeIds,
            cancellationToken);

        var faltantes = comando.EspecialidadeIds.Except(categorias.Select(c => c.Id)).ToList();

        if (faltantes.Count > 0)
        {
            throw new RecursoNaoEncontradoException(
                "Categoria de servico",
                string.Join(", ", faltantes));
        }

        tecnico.DefinirEspecialidades(categorias);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return tecnico.ParaDetalhe();
    }

    public async Task<TecnicoDetalheDto> DefinirAreasAsync(
        Guid tecnicoId,
        DefinirAreasCommand comando,
        CancellationToken cancellationToken = default)
    {
        var tecnico = await _tecnicos.ObterCompletoAsync(tecnicoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Tecnico", tecnicoId);

        tecnico.DefinirAreasAtendimento(comando.Areas
            .Select(area => new AreaAtendimento(
                _geradorId.NovoId(),
                tecnicoId,
                area.Bairro,
                area.CepInicial,
                area.CepFinal,
                area.TaxaDeslocamento))
            .ToList());

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return tecnico.ParaDetalhe();
    }
}

/// <summary>
/// Anexa documento de certificacao ou qualificacao do tecnico.
/// Requisitos: RNF0032.
/// Caso de uso: UC02.
/// </summary>
public sealed class AnexarDocumentoDoTecnicoHandler
{
    private readonly ITecnicoRepositorio _tecnicos;
    private readonly IArmazenamentoArquivos _armazenamento;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public AnexarDocumentoDoTecnicoHandler(
        ITecnicoRepositorio tecnicos,
        IArmazenamentoArquivos armazenamento,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _tecnicos = tecnicos;
        _armazenamento = armazenamento;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<DocumentoDoTecnicoDto> ExecutarAsync(
        Guid tecnicoId,
        string nomeArquivo,
        string tipoMime,
        long tamanhoBytes,
        Stream conteudo,
        CancellationToken cancellationToken = default)
    {
        var tecnico = await _tecnicos.ObterCompletoAsync(tecnicoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Tecnico", tecnicoId);

        var anexoId = _geradorId.NovoId();
        var caminhoRelativo = $"tecnicos/{tecnicoId}/{anexoId}{Path.GetExtension(nomeArquivo)}";

        // O construtor valida tipo MIME (RNF0032) e tamanho antes da gravacao em disco.
        var documento = Anexo.ParaTecnico(
            anexoId,
            tecnicoId,
            nomeArquivo,
            tipoMime,
            tamanhoBytes,
            _relogio.Agora,
            caminhoRelativo);

        await _armazenamento.GravarAsync(caminhoRelativo, conteudo, cancellationToken);

        tecnico.AdicionarDocumento(documento);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return new DocumentoDoTecnicoDto(
            documento.Id,
            documento.NomeArquivo,
            documento.TipoMime,
            documento.TamanhoBytes,
            documento.DataHoraUpload);
    }
}
