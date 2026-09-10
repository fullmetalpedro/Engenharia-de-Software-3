using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Pessoas;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Application.UC01Clientes;

/// <summary>
/// Cadastra um cliente com seus imoveis.
/// Requisitos: RF0011, RN0011, RN0012, RNF0021, RNF0022, RNF0023.
/// Caso de uso: UC01.
/// </summary>
public sealed class CadastrarClienteHandler
{
    private readonly IClienteRepositorio _clientes;
    private readonly IUsuarioRepositorio _usuarios;
    private readonly IGeradorDeSequencias _sequencias;
    private readonly IServicoDeHashDeSenha _hash;
    private readonly IGeradorId _geradorId;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public CadastrarClienteHandler(
        IClienteRepositorio clientes,
        IUsuarioRepositorio usuarios,
        IGeradorDeSequencias sequencias,
        IServicoDeHashDeSenha hash,
        IGeradorId geradorId,
        IUnitOfWork unidadeDeTrabalho)
    {
        _clientes = clientes;
        _usuarios = usuarios;
        _sequencias = sequencias;
        _hash = hash;
        _geradorId = geradorId;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<ClienteDetalheDto> ExecutarAsync(
        CadastrarClienteCommand comando,
        CancellationToken cancellationToken = default)
    {
        var cpf = new string(comando.Cpf.Where(char.IsDigit).ToArray());
        var email = comando.Email.Trim().ToLowerInvariant();

        if (await _usuarios.ExisteCpfAsync(cpf, cancellationToken: cancellationToken))
        {
            throw new ConflitoException($"Ja existe um usuario cadastrado com o CPF {cpf}.", "RN0011");
        }

        if (await _usuarios.ExisteEmailAsync(email, cancellationToken: cancellationToken))
        {
            throw new ConflitoException($"Ja existe um usuario cadastrado com o e-mail {email}.", "RN0011");
        }

        // RNF0023: codigo unico gerado pelo sistema a partir de sequence.
        var codigo = await _sequencias.ProximoCodigoDeClienteAsync(cancellationToken);
        var clienteId = _geradorId.NovoId();

        var imoveis = comando.Imoveis
            .Select(imovel => new Imovel(
                _geradorId.NovoId(),
                clienteId,
                imovel.Apelido,
                imovel.TipoImovel,
                imovel.Logradouro,
                imovel.Numero,
                imovel.Complemento,
                imovel.Bairro,
                imovel.Cep,
                imovel.Cidade,
                imovel.Estado))
            .ToList();

        var cliente = new Cliente(
            clienteId,
            codigo,
            comando.NomeCompleto,
            cpf,
            email,
            comando.Telefone,
            _hash.GerarHash(comando.Senha),
            imoveis);

        _clientes.Adicionar(cliente);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return cliente.ParaDetalhe();
    }
}

/// <summary>
/// Consulta clientes por filtro combinavel.
/// Requisitos: RF0015, RNF0011.
/// Caso de uso: UC01.
/// </summary>
public sealed class ConsultarClientesHandler
{
    private readonly IContextoDeLeitura _leitura;

    public ConsultarClientesHandler(IContextoDeLeitura leitura) => _leitura = leitura;

    public async Task<IReadOnlyCollection<ClienteResumoDto>> ExecutarAsync(
        FiltroDeClientes filtro,
        CancellationToken cancellationToken = default)
    {
        var consulta = _leitura.Clientes;

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            consulta = consulta.Where(c => EF.Functions.Like(c.NomeCompleto.ToLower(), $"%{filtro.Nome!.ToLower()}%"));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Cpf))
        {
            var cpf = new string(filtro.Cpf.Where(char.IsDigit).ToArray());
            consulta = consulta.Where(c => c.Cpf == cpf);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Email))
        {
            consulta = consulta.Where(c => EF.Functions.Like(c.Email.ToLower(), $"%{filtro.Email!.ToLower()}%"));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Telefone))
        {
            consulta = consulta.Where(c => c.Telefone.Contains(filtro.Telefone));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Codigo))
        {
            consulta = consulta.Where(c => c.CodigoCliente == filtro.Codigo);
        }

        if (filtro.Ativo is not null)
        {
            consulta = consulta.Where(c => c.Ativo == filtro.Ativo);
        }

        // RNF0011: projecao direta para DTO, sem materializar a entidade.
        var itens = await consulta
            .OrderBy(c => c.NomeCompleto)
            .Select(c => new ClienteResumoDto(
                c.Id,
                c.CodigoCliente,
                c.NomeCompleto,
                c.Cpf,
                c.Email,
                c.Telefone,
                c.Ativo))
            .ToListAsync(cancellationToken);

        return itens;
    }
}

/// <summary>
/// Consulta um cliente pelo identificador.
/// Requisitos: RF0015.
/// Caso de uso: UC01.
/// </summary>
public sealed class ObterClienteHandler
{
    private readonly IContextoDeLeitura _leitura;
    private readonly IUsuarioAtual _usuarioAtual;

    public ObterClienteHandler(IContextoDeLeitura leitura, IUsuarioAtual usuarioAtual)
    {
        _leitura = leitura;
        _usuarioAtual = usuarioAtual;
    }

    public async Task<ClienteDetalheDto> ExecutarAsync(
        Guid clienteId,
        CancellationToken cancellationToken = default)
    {
        Autorizacao.ExigirDonoOuAdministrador(_usuarioAtual, clienteId);

        var cliente = await _leitura.Clientes
            .Where(c => c.Id == clienteId)
            .Select(c => new ClienteDetalheDto(
                c.Id,
                c.CodigoCliente,
                c.NomeCompleto,
                c.Cpf,
                c.Email,
                c.Telefone,
                c.Ativo,
                _leitura.Imoveis
                    .Where(i => i.ClienteId == c.Id)
                    .Select(i => new ImovelDto(
                        i.Id,
                        i.Apelido,
                        i.TipoImovel,
                        i.Logradouro,
                        i.Numero,
                        i.Complemento,
                        i.Bairro,
                        i.Cep,
                        i.Cidade,
                        i.Estado))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        return cliente ?? throw new RecursoNaoEncontradoException("Cliente", clienteId);
    }
}

/// <summary>
/// Altera os dados cadastrais do cliente.
/// Requisitos: RF0012.
/// Caso de uso: UC01.
/// </summary>
public sealed class AlterarClienteHandler
{
    private readonly IClienteRepositorio _clientes;
    private readonly IUsuarioRepositorio _usuarios;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public AlterarClienteHandler(
        IClienteRepositorio clientes,
        IUsuarioRepositorio usuarios,
        IUsuarioAtual usuarioAtual,
        IUnitOfWork unidadeDeTrabalho)
    {
        _clientes = clientes;
        _usuarios = usuarios;
        _usuarioAtual = usuarioAtual;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<ClienteResumoDto> ExecutarAsync(
        Guid clienteId,
        AlterarClienteCommand comando,
        CancellationToken cancellationToken = default)
    {
        Autorizacao.ExigirDonoOuAdministrador(_usuarioAtual, clienteId);

        var cliente = await _clientes.ObterPorIdAsync(clienteId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", clienteId);

        var email = comando.Email.Trim().ToLowerInvariant();

        if (await _usuarios.ExisteEmailAsync(email, clienteId, cancellationToken))
        {
            throw new ConflitoException($"Ja existe um usuario cadastrado com o e-mail {email}.");
        }

        cliente.AlterarDados(comando.NomeCompleto, email, comando.Telefone);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return cliente.ParaResumo();
    }
}

/// <summary>
/// Ativa ou inativa o cadastro do cliente.
/// Requisitos: RF0013, RF0014.
/// Caso de uso: UC01.
/// </summary>
public sealed class AlterarSituacaoDoClienteHandler
{
    private readonly IClienteRepositorio _clientes;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public AlterarSituacaoDoClienteHandler(IClienteRepositorio clientes, IUnitOfWork unidadeDeTrabalho)
    {
        _clientes = clientes;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<ClienteResumoDto> ExecutarAsync(
        Guid clienteId,
        bool ativar,
        CancellationToken cancellationToken = default)
    {
        var cliente = await _clientes.ObterPorIdAsync(clienteId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", clienteId);

        if (ativar)
        {
            cliente.Ativar();
        }
        else
        {
            cliente.Inativar();
        }

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return cliente.ParaResumo();
    }
}

/// <summary>
/// Gerencia os imoveis do cliente.
/// Requisitos: RF0016, RN0011, RN0012.
/// Caso de uso: UC01.
/// </summary>
public sealed class ImoveisDoClienteHandler
{
    private readonly IClienteRepositorio _clientes;
    private readonly IContextoDeLeitura _leitura;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public ImoveisDoClienteHandler(
        IClienteRepositorio clientes,
        IContextoDeLeitura leitura,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IUnitOfWork unidadeDeTrabalho)
    {
        _clientes = clientes;
        _leitura = leitura;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<IReadOnlyCollection<ImovelDto>> ListarAsync(
        Guid clienteId,
        CancellationToken cancellationToken = default)
    {
        Autorizacao.ExigirDonoOuAdministrador(_usuarioAtual, clienteId);

        return await _leitura.Imoveis
            .Where(i => i.ClienteId == clienteId)
            .OrderBy(i => i.Apelido)
            .Select(i => new ImovelDto(
                i.Id,
                i.Apelido,
                i.TipoImovel,
                i.Logradouro,
                i.Numero,
                i.Complemento,
                i.Bairro,
                i.Cep,
                i.Cidade,
                i.Estado))
            .ToListAsync(cancellationToken);
    }

    public async Task<ImovelDto> AdicionarAsync(
        Guid clienteId,
        ImovelCommand comando,
        CancellationToken cancellationToken = default)
    {
        Autorizacao.ExigirDono(_usuarioAtual, clienteId);

        var cliente = await _clientes.ObterComImoveisAsync(clienteId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", clienteId);

        var imovel = new Imovel(
            _geradorId.NovoId(),
            clienteId,
            comando.Apelido,
            comando.TipoImovel,
            comando.Logradouro,
            comando.Numero,
            comando.Complemento,
            comando.Bairro,
            comando.Cep,
            comando.Cidade,
            comando.Estado);

        cliente.AdicionarImovel(imovel);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return imovel.ParaDto();
    }

    public async Task<ImovelDto> AlterarAsync(
        Guid clienteId,
        Guid imovelId,
        ImovelCommand comando,
        CancellationToken cancellationToken = default)
    {
        Autorizacao.ExigirDono(_usuarioAtual, clienteId);

        var cliente = await _clientes.ObterComImoveisAsync(clienteId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", clienteId);

        var imovel = cliente.Imoveis.SingleOrDefault(i => i.Id == imovelId)
            ?? throw new RecursoNaoEncontradoException("Imovel", imovelId);

        imovel.Alterar(
            comando.Apelido,
            comando.TipoImovel,
            comando.Logradouro,
            comando.Numero,
            comando.Complemento,
            comando.Bairro,
            comando.Cep,
            comando.Cidade,
            comando.Estado);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return imovel.ParaDto();
    }

    /// <summary>RN0011 impede remover o ultimo imovel do cliente.</summary>
    public async Task RemoverAsync(
        Guid clienteId,
        Guid imovelId,
        CancellationToken cancellationToken = default)
    {
        Autorizacao.ExigirDono(_usuarioAtual, clienteId);

        var cliente = await _clientes.ObterComImoveisAsync(clienteId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", clienteId);

        var possuiChamado = await _leitura.Chamados
            .AnyAsync(c => c.ImovelId == imovelId, cancellationToken);

        if (possuiChamado)
        {
            throw new ConflitoException(
                "O imovel possui chamados vinculados e nao pode ser removido.");
        }

        cliente.RemoverImovel(imovelId);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
    }
}
