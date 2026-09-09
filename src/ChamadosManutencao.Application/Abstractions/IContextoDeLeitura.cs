using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Avaliacoes;
using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Financeiro;
using ChamadosManutencao.Domain.Pessoas;

namespace ChamadosManutencao.Application.Abstractions;

/// <summary>
/// Lado de leitura das consultas (RNF0011). Toda propriedade devolve consulta sem rastreamento,
/// para que os handlers projetem direto para DTO sem materializar entidade.
///
/// Decisao D25: a Application referencia o pacote do EF Core (nao o projeto Infrastructure) para
/// que a projecao viva junto do caso de uso. A direcao das dependencias entre projetos continua
/// apontando para dentro.
/// </summary>
public interface IContextoDeLeitura
{
    IQueryable<Usuario> Usuarios { get; }

    IQueryable<Cliente> Clientes { get; }

    IQueryable<Tecnico> Tecnicos { get; }

    IQueryable<Imovel> Imoveis { get; }

    IQueryable<CategoriaServico> CategoriasServico { get; }

    IQueryable<TipoServico> TiposServico { get; }

    IQueryable<AreaAtendimento> AreasAtendimento { get; }

    IQueryable<Chamado> Chamados { get; }

    IQueryable<Anexo> Anexos { get; }

    IQueryable<HistoricoStatus> HistoricosDeStatus { get; }

    IQueryable<Agendamento> Agendamentos { get; }

    IQueryable<Atendimento> Atendimentos { get; }

    IQueryable<Orcamento> Orcamentos { get; }

    IQueryable<ItemOrcamento> ItensDeOrcamento { get; }

    IQueryable<Garantia> Garantias { get; }

    IQueryable<Avaliacao> Avaliacoes { get; }

    IQueryable<RespostaAvaliacao> RespostasDeAvaliacao { get; }

    IQueryable<Fatura> Faturas { get; }

    IQueryable<Pagamento> Pagamentos { get; }

    IQueryable<FormaPagamento> FormasDePagamento { get; }
}
