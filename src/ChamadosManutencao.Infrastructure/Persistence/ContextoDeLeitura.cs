using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Avaliacoes;
using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Financeiro;
using ChamadosManutencao.Domain.Pessoas;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Infrastructure.Persistence;

/// <summary>
/// RNF0011: toda consulta de leitura sai daqui sem rastreamento. Os handlers projetam direto
/// para DTO, o que evita materializar o agregado inteiro.
/// </summary>
public sealed class ContextoDeLeitura : IContextoDeLeitura
{
    private readonly AppDbContext _contexto;

    public ContextoDeLeitura(AppDbContext contexto) => _contexto = contexto;

    public IQueryable<Usuario> Usuarios => _contexto.Usuarios.AsNoTracking();

    public IQueryable<Cliente> Clientes => _contexto.Clientes.AsNoTracking();

    public IQueryable<Tecnico> Tecnicos => _contexto.Tecnicos.AsNoTracking();

    public IQueryable<Imovel> Imoveis => _contexto.Imoveis.AsNoTracking();

    public IQueryable<CategoriaServico> CategoriasServico => _contexto.CategoriasServico.AsNoTracking();

    public IQueryable<TipoServico> TiposServico => _contexto.TiposServico.AsNoTracking();

    public IQueryable<AreaAtendimento> AreasAtendimento => _contexto.AreasAtendimento.AsNoTracking();

    public IQueryable<Chamado> Chamados => _contexto.Chamados.AsNoTracking();

    public IQueryable<Anexo> Anexos => _contexto.Anexos.AsNoTracking();

    public IQueryable<HistoricoStatus> HistoricosDeStatus => _contexto.HistoricosDeStatus.AsNoTracking();

    public IQueryable<Agendamento> Agendamentos => _contexto.Agendamentos.AsNoTracking();

    public IQueryable<Atendimento> Atendimentos => _contexto.Atendimentos.AsNoTracking();

    public IQueryable<Orcamento> Orcamentos => _contexto.Orcamentos.AsNoTracking();

    public IQueryable<ItemOrcamento> ItensDeOrcamento => _contexto.ItensDeOrcamento.AsNoTracking();

    public IQueryable<Garantia> Garantias => _contexto.Garantias.AsNoTracking();

    public IQueryable<Avaliacao> Avaliacoes => _contexto.Avaliacoes.AsNoTracking();

    public IQueryable<RespostaAvaliacao> RespostasDeAvaliacao =>
        _contexto.RespostasDeAvaliacao.AsNoTracking();

    public IQueryable<Fatura> Faturas => _contexto.Faturas.AsNoTracking();

    public IQueryable<Pagamento> Pagamentos => _contexto.Pagamentos.AsNoTracking();

    public IQueryable<FormaPagamento> FormasDePagamento => _contexto.FormasDePagamento.AsNoTracking();
}
