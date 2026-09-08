namespace ChamadosManutencao.Domain.Common;

/// <summary>
/// Fonte de identificadores. Existe porque a secao 18 da especificacao proibe
/// Guid.NewGuid() espalhado pelo dominio: sem esta interface os testes nao conseguiriam
/// prever os identificadores gerados. A implementacao padrao usa Guid.CreateVersion7().
/// </summary>
public interface IGeradorId
{
    Guid NovoId();
}
