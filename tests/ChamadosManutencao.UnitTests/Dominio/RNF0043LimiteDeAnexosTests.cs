using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.UnitTests.Common;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.UnitTests.Dominio;

/// <summary>
/// RNF0043: no maximo 5 anexos por chamado e 10 MB por arquivo. A cota vale para a midia
/// do problema; as fotos da conclusao pertencem ao atendimento (decisao D38).
/// </summary>
[Trait("Requisito", "RNF0043")]
public class RNF0043LimiteDeAnexosTests
{
    private static Anexo Midia(Guid chamadoId, int indice, long tamanho = 1024) =>
        Anexo.ParaChamado(
            Construtor.Id(200 + indice),
            chamadoId,
            $"foto-{indice}.jpg",
            "image/jpeg",
            tamanho,
            Construtor.Agora,
            $"storage/chamados/1/foto-{indice}.jpg");

    [Fact]
    public void Cinco_anexos_de_abertura_sao_aceitos()
    {
        var chamado = Construtor.Chamado();

        for (var i = 1; i <= 5; i++)
        {
            chamado.AdicionarAnexo(Midia(chamado.Id, i));
        }

        chamado.Anexos.Count.ShouldBe(5);
    }

    [Fact]
    public void Sexto_anexo_de_abertura_e_recusado()
    {
        var chamado = Construtor.Chamado();

        for (var i = 1; i <= 5; i++)
        {
            chamado.AdicionarAnexo(Midia(chamado.Id, i));
        }

        var excecao = Should.Throw<ExcecaoDeDominio>(
            () => chamado.AdicionarAnexo(Midia(chamado.Id, 6)));

        excecao.Requisito.ShouldBe("RNF0043");
        chamado.Anexos.Count.ShouldBe(5);
    }

    /// <summary>
    /// O diagrama de classes liga as fotos da conclusao ao atendimento (0..*), e nao ao
    /// chamado (0..5): um chamado com a cota cheia ainda pode ser concluido com fotos.
    /// </summary>
    [Fact]
    public void Fotos_da_conclusao_ficam_fora_da_cota_do_chamado()
    {
        var chamado = Construtor.Chamado();

        for (var i = 1; i <= 5; i++)
        {
            chamado.AdicionarAnexo(Midia(chamado.Id, i));
        }

        var atendimento = new Atendimento(
            Construtor.Id(300),
            chamado.Id,
            Construtor.Id(4),
            Construtor.Agora);

        for (var i = 10; i <= 15; i++)
        {
            atendimento.AdicionarFotoDaConclusao(Anexo.ParaAtendimento(
                Construtor.Id(400 + i),
                atendimento.Id,
                $"servico-{i}.jpg",
                "image/jpeg",
                1024,
                Construtor.Agora,
                $"storage/atendimentos/1/servico-{i}.jpg"));
        }

        chamado.Anexos.Count.ShouldBe(5);
        atendimento.FotosDaConclusao.Count.ShouldBe(6);
    }

    [Fact]
    public void Arquivo_acima_de_dez_megabytes_e_recusado()
    {
        var chamado = Construtor.Chamado();

        var excecao = Should.Throw<ExcecaoDeDominio>(() => Midia(
            chamado.Id,
            11,
            Anexo.TamanhoMaximoEmBytes + 1));

        excecao.Requisito.ShouldBe("RNF0043");
    }

    [Fact]
    public void Arquivo_com_exatamente_dez_megabytes_e_aceito()
    {
        var chamado = Construtor.Chamado();

        var anexo = Midia(chamado.Id, 12, Anexo.TamanhoMaximoEmBytes);

        chamado.AdicionarAnexo(anexo);

        chamado.Anexos.Count.ShouldBe(1);
    }

    [Fact]
    public void Anexo_de_outro_chamado_e_recusado()
    {
        var chamado = Construtor.Chamado();

        Should.Throw<ExcecaoDeDominio>(
            () => chamado.AdicionarAnexo(Midia(Construtor.Id(999), 13)));
    }
}
