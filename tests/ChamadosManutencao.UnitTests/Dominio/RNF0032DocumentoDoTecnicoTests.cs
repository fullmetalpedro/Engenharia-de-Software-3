using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RNF0032: upload de documentos do tecnico restrito a PDF, PNG e JPEG.
/// </summary>
[Trait("Requisito", "RNF0032")]
public class RNF0032DocumentoDoTecnicoTests
{
    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    public void Tipos_permitidos_sao_aceitos(string tipoMime)
    {
        var tecnico = Construtor.Tecnico();

        var documento = Anexo.ParaTecnico(
            Construtor.Id(300),
            tecnico.Id,
            "certificado",
            tipoMime,
            2048,
            Construtor.Agora,
            "storage/tecnicos/1/certificado");

        tecnico.AdicionarDocumento(documento);

        tecnico.Documentos.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("application/zip")]
    [InlineData("text/plain")]
    public void Tipos_nao_permitidos_sao_recusados(string tipoMime)
    {
        var tecnico = Construtor.Tecnico();

        var excecao = Should.Throw<ExcecaoDeDominio>(() => Anexo.ParaTecnico(
            Construtor.Id(301),
            tecnico.Id,
            "arquivo",
            tipoMime,
            2048,
            Construtor.Agora,
            "storage/tecnicos/1/arquivo"));

        excecao.Requisito.ShouldBe("RNF0032");
    }
}
