using System.Diagnostics.CodeAnalysis;
using ChamadosManutencao.Domain.Common;

namespace ChamadosManutencao.Domain.Catalogo;

/// <summary>
/// Faixa de CEP atendida por um tecnico, com a taxa de deslocamento aplicada na fatura.
/// Requisitos: RF0027, RN0023, RN0071.
/// </summary>
public sealed class AreaAtendimento : Entidade
{
    public AreaAtendimento(
        Guid id,
        Guid tecnicoId,
        string bairro,
        string cepInicial,
        string cepFinal,
        decimal taxaDeslocamento)
        : base(id)
    {
        TecnicoId = tecnicoId;

        Alterar(bairro, cepInicial, cepFinal, taxaDeslocamento);
    }

    private AreaAtendimento()
    {
        Bairro = null!;
        CepInicial = null!;
        CepFinal = null!;
    }

    public Guid TecnicoId { get; private set; }

    public string Bairro { get; private set; }

    public string CepInicial { get; private set; }

    public string CepFinal { get; private set; }

    public decimal TaxaDeslocamento { get; private set; }

    /// <summary>RN0023: o CEP informado esta dentro da faixa desta area.</summary>
    public bool Contem(string cep)
    {
        var normalizado = Garantir.Cep(cep, "RN0023");

        return string.CompareOrdinal(normalizado, CepInicial) >= 0
            && string.CompareOrdinal(normalizado, CepFinal) <= 0;
    }

    [MemberNotNull(nameof(Bairro), nameof(CepInicial), nameof(CepFinal))]
    public void Alterar(string bairro, string cepInicial, string cepFinal, decimal taxaDeslocamento)
    {
        const string requisito = "RF0027";

        Bairro = Garantir.TextoComTamanhoMaximo(bairro, 120, "bairro da area de atendimento", requisito);
        CepInicial = Garantir.Cep(cepInicial, requisito);
        CepFinal = Garantir.Cep(cepFinal, requisito);
        TaxaDeslocamento = Garantir.ValorNaoNegativo(taxaDeslocamento, "taxa de deslocamento", "RN0071");

        if (string.CompareOrdinal(CepInicial, CepFinal) > 0)
        {
            throw new ExcecaoDeDominio(
                "O CEP inicial da area de atendimento nao pode ser maior que o CEP final.",
                requisito);
        }
    }
}
