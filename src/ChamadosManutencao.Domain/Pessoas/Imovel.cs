using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Pessoas;

/// <summary>
/// Imovel do cliente. Composicao: excluir o cliente exclui os imoveis.
/// Requisitos: RF0016, RN0012.
/// </summary>
public sealed class Imovel : Entidade
{
    public Imovel(
        Guid id,
        Guid clienteId,
        string apelido,
        TipoImovel tipoImovel,
        string logradouro,
        string numero,
        string? complemento,
        string bairro,
        string cep,
        string cidade,
        string estado)
        : base(id)
    {
        const string requisito = "RN0012";

        ClienteId = clienteId;
        Apelido = Garantir.TextoComTamanhoMaximo(apelido, 60, "apelido do imovel", requisito);
        TipoImovel = tipoImovel;
        Logradouro = Garantir.TextoComTamanhoMaximo(logradouro, 200, "logradouro", requisito);
        Numero = Garantir.TextoComTamanhoMaximo(numero, 20, "numero", requisito);
        Complemento = string.IsNullOrWhiteSpace(complemento) ? null : complemento.Trim();
        Bairro = Garantir.TextoComTamanhoMaximo(bairro, 120, "bairro", requisito);
        Cep = Garantir.Cep(cep, requisito);
        Cidade = Garantir.TextoComTamanhoMaximo(cidade, 120, "cidade", requisito);
        Estado = ValidarEstado(estado);
    }

    private Imovel()
    {
        Apelido = null!;
        Logradouro = null!;
        Numero = null!;
        Bairro = null!;
        Cep = null!;
        Cidade = null!;
        Estado = null!;
    }

    public Guid ClienteId { get; private set; }

    public string Apelido { get; private set; }

    public TipoImovel TipoImovel { get; private set; }

    public string Logradouro { get; private set; }

    public string Numero { get; private set; }

    public string? Complemento { get; private set; }

    public string Bairro { get; private set; }

    public string Cep { get; private set; }

    public string Cidade { get; private set; }

    public string Estado { get; private set; }

    public void Alterar(
        string apelido,
        TipoImovel tipoImovel,
        string logradouro,
        string numero,
        string? complemento,
        string bairro,
        string cep,
        string cidade,
        string estado)
    {
        const string requisito = "RN0012";

        Apelido = Garantir.TextoComTamanhoMaximo(apelido, 60, "apelido do imovel", requisito);
        TipoImovel = tipoImovel;
        Logradouro = Garantir.TextoComTamanhoMaximo(logradouro, 200, "logradouro", requisito);
        Numero = Garantir.TextoComTamanhoMaximo(numero, 20, "numero", requisito);
        Complemento = string.IsNullOrWhiteSpace(complemento) ? null : complemento.Trim();
        Bairro = Garantir.TextoComTamanhoMaximo(bairro, 120, "bairro", requisito);
        Cep = Garantir.Cep(cep, requisito);
        Cidade = Garantir.TextoComTamanhoMaximo(cidade, 120, "cidade", requisito);
        Estado = ValidarEstado(estado);
    }

    private static string ValidarEstado(string estado)
    {
        var sigla = Garantir.TextoPreenchido(estado, "estado", "RN0012").ToUpperInvariant();

        if (sigla.Length != 2)
        {
            throw new ExcecaoDeDominio("O estado deve ser informado com a sigla de 2 letras.", "RN0012");
        }

        return sigla;
    }
}
