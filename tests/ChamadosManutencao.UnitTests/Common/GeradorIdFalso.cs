using ChamadosManutencao.Domain.Common;

namespace ChamadosManutencao.UnitTests.Common;

/// <summary>
/// Gerador de identificadores deterministico. Existe porque o dominio recebe a geracao de Id
/// por interface (secao 18 da especificacao), o que torna os testes previsiveis.
/// </summary>
public sealed class GeradorIdFalso : IGeradorId
{
    private int _proximo;

    public Guid NovoId()
    {
        _proximo++;
        return new Guid($"00000000-0000-0000-0000-{_proximo:000000000000}");
    }
}
