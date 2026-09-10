using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Pessoas;

/// <summary>
/// Tecnico responsavel pela execucao dos servicos.
/// Requisitos: RF0021, RF0026, RF0027, RN0021, RN0022, RN0023, RN0041, RNF0031, RNF0032.
/// </summary>
public sealed class Tecnico : Usuario
{
    private readonly List<CategoriaServico> _especialidades = [];
    private readonly List<AreaAtendimento> _areasAtendimento = [];
    private readonly List<Anexo> _documentos = [];

    public Tecnico(
        Guid id,
        string codigoTecnico,
        string nomeCompleto,
        string cpf,
        string email,
        string telefone,
        string senhaHash,
        IEnumerable<CategoriaServico> especialidades)
        : base(id, nomeCompleto, cpf, email, telefone, senhaHash, "RN0021")
    {
        CodigoTecnico = Garantir.TextoComTamanhoMaximo(codigoTecnico, 20, "codigo do tecnico", "RNF0031");

        var lista = especialidades?.ToList() ?? [];

        if (lista.Count == 0)
        {
            throw new ExcecaoDeDominio(
                "O cadastro de tecnico exige ao menos uma especialidade.",
                "RN0021");
        }

        _especialidades.AddRange(lista);
    }

    private Tecnico()
    {
        CodigoTecnico = null!;
    }

    /// <summary>Codigo unico sequencial no formato TEC-000001 (RNF0031).</summary>
    public string CodigoTecnico { get; private set; }

    public override PapelUsuario Papel => PapelUsuario.Tecnico;

    public IReadOnlyCollection<CategoriaServico> Especialidades => _especialidades.AsReadOnly();

    public IReadOnlyCollection<AreaAtendimento> AreasAtendimento => _areasAtendimento.AsReadOnly();

    public IReadOnlyCollection<Anexo> Documentos => _documentos.AsReadOnly();

    /// <summary>RF0026.</summary>
    public void DefinirEspecialidades(IEnumerable<CategoriaServico> especialidades)
    {
        var lista = especialidades?.ToList() ?? [];

        if (lista.Count == 0)
        {
            throw new ExcecaoDeDominio(
                "O tecnico deve manter ao menos uma especialidade.",
                "RN0021");
        }

        _especialidades.Clear();
        _especialidades.AddRange(lista);
    }

    /// <summary>
    /// RF0027: a area de atendimento e opcional. A RN0021 lista como obrigatorios apenas nome,
    /// CPF, telefone, e-mail e ao menos uma especialidade; sem area cadastrada o tecnico
    /// simplesmente nunca passa na RN0023 e nao chega a ser atribuido a chamado nenhum.
    /// </summary>
    public void DefinirAreasAtendimento(IEnumerable<AreaAtendimento> areas)
    {
        var lista = areas?.ToList() ?? [];

        _areasAtendimento.Clear();
        _areasAtendimento.AddRange(lista);
    }

    /// <summary>RNF0032: documento de certificacao ou qualificacao do tecnico.</summary>
    public void AdicionarDocumento(Anexo documento)
    {
        Garantir.NaoNulo(documento, "documento", "RNF0032");

        if (documento.Origem != OrigemAnexo.DocumentoTecnico)
        {
            throw new ExcecaoDeDominio(
                "Somente anexos com origem DocumentoTecnico podem ser vinculados ao tecnico.",
                "RNF0032");
        }

        _documentos.Add(documento);
    }

    /// <summary>RN0022: o tecnico so atende chamado de categoria que seja sua especialidade.</summary>
    public bool AtendeCategoria(Guid categoriaServicoId) =>
        _especialidades.Any(e => e.Id == categoriaServicoId);

    /// <summary>RN0023: o tecnico so atende imovel dentro de alguma de suas areas.</summary>
    public bool AtendeCep(string cep)
    {
        var normalizado = Garantir.Cep(cep, "RN0023");
        return _areasAtendimento.Any(a => a.Contem(normalizado));
    }

    /// <summary>
    /// RN0023: area de atendimento que cobre o CEP informado. Usada para achar a taxa de
    /// deslocamento que compoe a fatura (RN0071, decisao D10).
    /// </summary>
    public AreaAtendimento? AreaQueAtende(string cep)
    {
        var normalizado = Garantir.Cep(cep, "RN0023");
        return _areasAtendimento.FirstOrDefault(a => a.Contem(normalizado));
    }

    /// <summary>
    /// RN0041: o tecnico esta disponivel na janela informada quando nenhum de seus compromissos
    /// se sobrepoe a ela. Os compromissos chegam prontos do repositorio, o que mantem a regra
    /// no dominio e testavel sem banco.
    /// </summary>
    public bool EstaDisponivel(
        DateTimeOffset inicio,
        DateTimeOffset fim,
        IEnumerable<JanelaDeAtendimento> compromissos)
    {
        if (fim <= inicio)
        {
            throw new ExcecaoDeDominio(
                "O fim da janela de atendimento deve ser posterior ao inicio.",
                "RN0041");
        }

        return !compromissos.Any(c => c.Inicio < fim && inicio < c.Fim);
    }
}

/// <summary>
/// Janela de tempo ocupada por um agendamento do tecnico (RN0041, decisao D08).
/// </summary>
public readonly record struct JanelaDeAtendimento(DateTimeOffset Inicio, DateTimeOffset Fim);
