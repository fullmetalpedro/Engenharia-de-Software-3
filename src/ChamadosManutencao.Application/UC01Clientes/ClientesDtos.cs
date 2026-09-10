using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Pessoas;

namespace ChamadosManutencao.Application.UC01Clientes;

/// <summary>Imovel na entrada de cadastro e alteracao (RN0012).</summary>
public sealed record ImovelCommand(
    string Apelido,
    TipoImovel TipoImovel,
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string Cep,
    string Cidade,
    string Estado);

/// <summary>Cadastro de cliente (RF0011, RN0011).</summary>
public sealed record CadastrarClienteCommand(
    string NomeCompleto,
    string Cpf,
    string Email,
    string Telefone,
    string Senha,
    IReadOnlyCollection<ImovelCommand> Imoveis);

/// <summary>Alteracao dos dados cadastrais (RF0012).</summary>
public sealed record AlterarClienteCommand(string NomeCompleto, string Email, string Telefone);

/// <summary>Filtros de RF0015, combinaveis ou isolados.</summary>
public sealed record FiltroDeClientes(
    string? Nome = null,
    string? Cpf = null,
    string? Email = null,
    string? Telefone = null,
    string? Codigo = null,
    bool? Ativo = null);

public sealed record ImovelDto(
    Guid Id,
    string Apelido,
    TipoImovel TipoImovel,
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string Cep,
    string Cidade,
    string Estado);

public sealed record ClienteResumoDto(
    Guid Id,
    string CodigoCliente,
    string NomeCompleto,
    string Cpf,
    string Email,
    string Telefone,
    bool Ativo);

public sealed record ClienteDetalheDto(
    Guid Id,
    string CodigoCliente,
    string NomeCompleto,
    string Cpf,
    string Email,
    string Telefone,
    bool Ativo,
    IReadOnlyCollection<ImovelDto> Imoveis);

/// <summary>
/// Mapeamento escrito a mao (sem AutoMapper) para manter a conversao auditavel.
/// A propriedade SenhaHash nunca aparece em DTO (RNF0022).
/// </summary>
public static class ClientesMapeamento
{
    public static ImovelDto ParaDto(this Imovel imovel) => new(
        imovel.Id,
        imovel.Apelido,
        imovel.TipoImovel,
        imovel.Logradouro,
        imovel.Numero,
        imovel.Complemento,
        imovel.Bairro,
        imovel.Cep,
        imovel.Cidade,
        imovel.Estado);

    public static ClienteResumoDto ParaResumo(this Cliente cliente) => new(
        cliente.Id,
        cliente.CodigoCliente,
        cliente.NomeCompleto,
        cliente.Cpf,
        cliente.Email,
        cliente.Telefone,
        cliente.Ativo);

    public static ClienteDetalheDto ParaDetalhe(this Cliente cliente) => new(
        cliente.Id,
        cliente.CodigoCliente,
        cliente.NomeCompleto,
        cliente.Cpf,
        cliente.Email,
        cliente.Telefone,
        cliente.Ativo,
        cliente.Imoveis.Select(ParaDto).ToList());
}
