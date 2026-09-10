using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RN0032: categorias que exigem foto bloqueiam a abertura sem ao menos uma foto.
/// O bloqueio em si vive no handler de abertura; aqui ficam a marcacao da categoria e a
/// deteccao de foto no chamado, que o handler consulta.
/// </summary>
[Trait("Requisito", "RN0032")]
public class RN0032FotoObrigatoriaTests
{
    [Fact]
    public void Categoria_marcada_como_exige_foto_expoe_a_exigencia()
    {
        var categoria = Construtor.Categoria(exigeFoto: true);

        categoria.ExigeFoto.ShouldBeTrue();
    }

    [Fact]
    public void Chamado_sem_anexo_nao_possui_foto_de_abertura()
    {
        var chamado = Construtor.Chamado();

        chamado.PossuiFotoDeAbertura().ShouldBeFalse();
    }

    [Fact]
    public void Chamado_com_video_na_abertura_nao_satisfaz_a_exigencia_de_foto()
    {
        var chamado = Construtor.Chamado();

        chamado.AdicionarAnexo(Anexo.ParaChamado(
            Construtor.Id(30),
            chamado.Id,
            "video.mp4",
            "video/mp4",
            2048,
            Construtor.Agora,
            "storage/chamados/1/video.mp4"));

        chamado.PossuiFotoDeAbertura().ShouldBeFalse();
    }

    [Fact]
    public void Chamado_com_foto_na_abertura_satisfaz_a_exigencia()
    {
        var chamado = Construtor.Chamado();

        chamado.AdicionarAnexo(Anexo.ParaChamado(
            Construtor.Id(31),
            chamado.Id,
            "equipamento.jpg",
            "image/jpeg",
            4096,
            Construtor.Agora,
            "storage/chamados/1/equipamento.jpg"));

        chamado.PossuiFotoDeAbertura().ShouldBeTrue();
    }

    [Fact]
    public void Foto_da_conclusao_nao_conta_como_foto_de_abertura()
    {
        var chamado = Construtor.Chamado();

        var foto = Anexo.ParaAtendimento(
            Construtor.Id(32),
            Construtor.Id(300),
            "servico-finalizado.jpg",
            "image/jpeg",
            4096,
            Construtor.Agora,
            "storage/atendimentos/1/servico-finalizado.jpg");

        Should.Throw<ExcecaoDeDominio>(() => chamado.AdicionarAnexo(foto));

        chamado.PossuiFotoDeAbertura().ShouldBeFalse();
    }

    [Fact]
    public void Documento_de_tecnico_nao_pode_ser_anexado_a_chamado()
    {
        var chamado = Construtor.Chamado();

        var excecao = Should.Throw<ExcecaoDeDominio>(() => Anexo.ParaChamado(
            Construtor.Id(33),
            chamado.Id,
            "certificado.pdf",
            "application/pdf",
            1024,
            Construtor.Agora,
            "storage/tecnicos/1/certificado.pdf"));

        excecao.Requisito.ShouldBe("RF0042");
    }
}
