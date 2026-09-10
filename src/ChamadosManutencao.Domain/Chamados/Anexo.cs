using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Chamados;

/// <summary>
/// Arquivo anexado a um chamado (midia do problema), a um atendimento (fotos do servico
/// finalizado) ou ao cadastro de um tecnico (certificacoes).
/// Requisitos: RF0042, RF0057, RNF0032, RNF0043.
/// </summary>
public sealed class Anexo : Entidade
{
    /// <summary>Limite de 10 MB por arquivo (RNF0043).</summary>
    public const long TamanhoMaximoEmBytes = 10L * 1024 * 1024;

    /// <summary>Tipos aceitos para documentos do tecnico (RNF0032).</summary>
    public static readonly string[] TiposMimeDeDocumento =
    [
        "application/pdf",
        "image/png",
        "image/jpeg"
    ];

    /// <summary>Tipos aceitos para midias do chamado (RF0042: fotos ou videos).</summary>
    public static readonly string[] TiposMimeDeMidia =
    [
        "image/png",
        "image/jpeg",
        "image/webp",
        "video/mp4",
        "video/quicktime"
    ];

    private Anexo(
        Guid id,
        string nomeArquivo,
        string tipoMime,
        long tamanhoBytes,
        DateTimeOffset dataHoraUpload,
        string caminho,
        OrigemAnexo origem,
        Guid? chamadoId,
        Guid? atendimentoId,
        Guid? tecnicoId)
        : base(id)
    {
        const string requisito = "RNF0043";

        NomeArquivo = Garantir.TextoComTamanhoMaximo(nomeArquivo, 260, "nome do arquivo", requisito);
        TipoMime = Garantir.TextoComTamanhoMaximo(tipoMime, 120, "tipo MIME", requisito);
        Caminho = Garantir.TextoComTamanhoMaximo(caminho, 500, "caminho do arquivo", requisito);
        DataHoraUpload = dataHoraUpload;
        Origem = origem;
        ChamadoId = chamadoId;
        AtendimentoId = atendimentoId;
        TecnicoId = tecnicoId;

        if (tamanhoBytes <= 0)
        {
            throw new ExcecaoDeDominio("O arquivo anexado esta vazio.", requisito);
        }

        if (tamanhoBytes > TamanhoMaximoEmBytes)
        {
            throw new ExcecaoDeDominio(
                "Cada arquivo anexado deve ter no maximo 10 MB.",
                requisito);
        }

        TamanhoBytes = tamanhoBytes;
    }

    private Anexo()
    {
        NomeArquivo = null!;
        TipoMime = null!;
        Caminho = null!;
    }

    public string NomeArquivo { get; private set; }

    public string TipoMime { get; private set; }

    public long TamanhoBytes { get; private set; }

    public DateTimeOffset DataHoraUpload { get; private set; }

    public string Caminho { get; private set; }

    public OrigemAnexo Origem { get; private set; }

    public Guid? ChamadoId { get; private set; }

    public Guid? AtendimentoId { get; private set; }

    public Guid? TecnicoId { get; private set; }

    /// <summary>RF0042: midia do problema, vinculada ao chamado.</summary>
    public static Anexo ParaChamado(
        Guid id,
        Guid chamadoId,
        string nomeArquivo,
        string tipoMime,
        long tamanhoBytes,
        DateTimeOffset dataHoraUpload,
        string caminho)
    {
        if (!TiposMimeDeMidia.Contains(tipoMime, StringComparer.OrdinalIgnoreCase))
        {
            throw new ExcecaoDeDominio(
                $"Tipo de arquivo '{tipoMime}' nao aceito para anexo de chamado.",
                "RF0042");
        }

        return new Anexo(
            id,
            nomeArquivo,
            tipoMime,
            tamanhoBytes,
            dataHoraUpload,
            caminho,
            OrigemAnexo.ChamadoAbertura,
            chamadoId,
            atendimentoId: null,
            tecnicoId: null);
    }

    /// <summary>
    /// RF0057: foto do servico finalizado. O diagrama de classes liga essas fotos ao
    /// atendimento, e nao ao chamado, por isso elas ficam fora da cota de 5 da RNF0043,
    /// que vale para a midia do problema.
    /// </summary>
    public static Anexo ParaAtendimento(
        Guid id,
        Guid atendimentoId,
        string nomeArquivo,
        string tipoMime,
        long tamanhoBytes,
        DateTimeOffset dataHoraUpload,
        string caminho)
    {
        if (!TiposMimeDeMidia.Contains(tipoMime, StringComparer.OrdinalIgnoreCase))
        {
            throw new ExcecaoDeDominio(
                $"Tipo de arquivo '{tipoMime}' nao aceito para foto da conclusao.",
                "RF0057");
        }

        return new Anexo(
            id,
            nomeArquivo,
            tipoMime,
            tamanhoBytes,
            dataHoraUpload,
            caminho,
            OrigemAnexo.ConclusaoAtendimento,
            chamadoId: null,
            atendimentoId: atendimentoId,
            tecnicoId: null);
    }

    /// <summary>RNF0032: documento de certificacao ou qualificacao do tecnico.</summary>
    public static Anexo ParaTecnico(
        Guid id,
        Guid tecnicoId,
        string nomeArquivo,
        string tipoMime,
        long tamanhoBytes,
        DateTimeOffset dataHoraUpload,
        string caminho)
    {
        if (!TiposMimeDeDocumento.Contains(tipoMime, StringComparer.OrdinalIgnoreCase))
        {
            throw new ExcecaoDeDominio(
                $"Documento de tecnico aceita apenas PDF, PNG ou JPEG. Recebido: '{tipoMime}'.",
                "RNF0032");
        }

        return new Anexo(
            id,
            nomeArquivo,
            tipoMime,
            tamanhoBytes,
            dataHoraUpload,
            caminho,
            OrigemAnexo.DocumentoTecnico,
            chamadoId: null,
            atendimentoId: null,
            tecnicoId: tecnicoId);
    }

    /// <summary>RN0032: a foto obrigatoria precisa ser imagem, nao qualquer midia.</summary>
    public bool EhFoto() => TipoMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}
