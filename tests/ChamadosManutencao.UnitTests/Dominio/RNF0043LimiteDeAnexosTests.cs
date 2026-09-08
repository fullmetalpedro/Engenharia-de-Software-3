using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RNF0043: no maximo 5 anexos por chamado e 10 MB por arquivo. O limite e contado por origem
/// (decisao D09): as fotos da conclusao nao consomem a cota das midias da abertura.
/// </summary>
[Trait("Requisito", "RNF0043")]
public class RNF0043LimiteDeAnexosTests
{
    private static Anexo Midia(Guid chamadoId, int indice, OrigemAnexo origem, long tamanho = 1024) =>
        Anexo.ParaChamado(
            Construtor.Id(200 + indice),
            chamadoId,
            $"foto-{indice}.jpg",
            "image/jpeg",
            tamanho,
            Construtor.Agora,
            $"storage/chamados/1/foto-{indice}.jpg",
            origem);

    [Fact]
    public void Cinco_anexos_de_abertura_sao_aceitos()
    {
        var chamado = Construtor.Chamado();

        for (var i = 1; i <= 5; i++)
        {
            chamado.AdicionarAnexo(Midia(chamado.Id, i, OrigemAnexo.ChamadoAbertura));
        }

        chamado.Anexos.Count.ShouldBe(5);
    }

    [Fact]
    public void Sexto_anexo_de_abertura_e_recusado()
    {
        var chamado = Construtor.Chamado();

        for (var i = 1; i <= 5; i++)
        {
            chamado.AdicionarAnexo(Midia(chamado.Id, i, OrigemAnexo.ChamadoAbertura));
        }

        var excecao = Should.Throw<ExcecaoDeDominio>(
            () => chamado.AdicionarAnexo(Midia(chamado.Id, 6, OrigemAnexo.ChamadoAbertura)));

        excecao.Requisito.ShouldBe("RNF0043");
        chamado.Anexos.Count.ShouldBe(5);
    }

    [Fact]
    public void Fotos_da_conclusao_tem_cota_propria()
    {
        var chamado = Construtor.Chamado();

        for (var i = 1; i <= 5; i++)
        {
            chamado.AdicionarAnexo(Midia(chamado.Id, i, OrigemAnexo.ChamadoAbertura));
        }

        chamado.AdicionarAnexo(Midia(chamado.Id, 10, OrigemAnexo.ConclusaoAtendimento));

        chamado.Anexos.Count.ShouldBe(6);
    }

    [Fact]
    public void Arquivo_acima_de_dez_megabytes_e_recusado()
    {
        var chamado = Construtor.Chamado();

        var excecao = Should.Throw<ExcecaoDeDominio>(() => Midia(
            chamado.Id,
            11,
            OrigemAnexo.ChamadoAbertura,
            Anexo.TamanhoMaximoEmBytes + 1));

        excecao.Requisito.ShouldBe("RNF0043");
    }

    [Fact]
    public void Arquivo_com_exatamente_dez_megabytes_e_aceito()
    {
        var chamado = Construtor.Chamado();

        var anexo = Midia(chamado.Id, 12, OrigemAnexo.ChamadoAbertura, Anexo.TamanhoMaximoEmBytes);

        chamado.AdicionarAnexo(anexo);

        chamado.Anexos.Count.ShouldBe(1);
    }

    [Fact]
    public void Anexo_de_outro_chamado_e_recusado()
    {
        var chamado = Construtor.Chamado();

        Should.Throw<ExcecaoDeDominio>(
            () => chamado.AdicionarAnexo(Midia(Construtor.Id(999), 13, OrigemAnexo.ChamadoAbertura)));
    }
}
