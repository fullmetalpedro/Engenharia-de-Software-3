using System.Net;
using System.Net.Http.Headers;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC02Tecnicos;
using ChamadosManutencao.IntegrationTests.Comum;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// UC02 Gerenciar Cadastro de Tecnicos.
/// Requisitos: RF0021 a RF0027, RN0021, RNF0031, RNF0032.
/// </summary>
[Trait("Escopo", "UC02")]
public class UC02TecnicosTests : TesteDeIntegracao
{
    public UC02TecnicosTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    /// <summary>RF0021, RN0021, RNF0031.</summary>
    [Fact]
    public async Task Cadastro_cria_tecnico_com_codigo_especialidade_e_area()
    {
        var categoria = await CriarCategoriaAsync();
        var tecnico = await CriarTecnicoAsync(categoria.Id);

        tecnico.Dados.CodigoTecnico.ShouldStartWith("TEC-");
        tecnico.Dados.Ativo.ShouldBeTrue();
        tecnico.Dados.Especialidades.Single().Id.ShouldBe(categoria.Id);
        tecnico.Dados.AreasAtendimento.Single().Bairro.ShouldBe("Centro");
    }

    /// <summary>RN0021: o cadastro exige ao menos uma especialidade.</summary>
    [Fact]
    public async Task Cadastro_sem_especialidade_e_recusado()
    {
        var comando = new CadastrarTecnicoCommand(
            "Sem Especialidade",
            Cpf(),
            $"tecnico{Proximo()}@chamados.local",
            "11930000001",
            SenhaPadrao,
            [],
            [new AreaAtendimentoCommand("Centro", "01000000", "01999999", 25m)]);

        var resposta = await Admin.PostarAsync("/api/v1/tecnicos", comando);

        await resposta.DeveTerStatusAsync(HttpStatusCode.BadRequest);
    }

    /// <summary>RF0027: CEP inicial maior que o final nao forma faixa.</summary>
    [Fact]
    public async Task Area_de_atendimento_com_faixa_invertida_e_recusada()
    {
        var categoria = await CriarCategoriaAsync();
        var tecnico = await CriarTecnicoAsync(categoria.Id);

        var resposta = await Admin.ColocarAsync(
            $"/api/v1/tecnicos/{tecnico.Id}/areas-atendimento",
            new DefinirAreasCommand([new AreaAtendimentoCommand("Centro", "02999999", "02000000", 30m)]));

        resposta.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>RF0022.</summary>
    [Fact]
    public async Task Alteracao_atualiza_dados_cadastrais()
    {
        var categoria = await CriarCategoriaAsync();
        var tecnico = await CriarTecnicoAsync(categoria.Id);

        var alterado = await (await Admin.ColocarAsync(
                $"/api/v1/tecnicos/{tecnico.Id}",
                new AlterarTecnicoCommand("Tecnico Alterado", "alterado@chamados.local", "11966666666")))
            .LerAsync<TecnicoDetalheDto>();

        alterado.NomeCompleto.ShouldBe("Tecnico Alterado");
        alterado.Email.ShouldBe("alterado@chamados.local");
    }

    /// <summary>RF0023 e RF0024.</summary>
    [Fact]
    public async Task Inativacao_e_reativacao_alternam_a_situacao()
    {
        var categoria = await CriarCategoriaAsync();
        var tecnico = await CriarTecnicoAsync(categoria.Id);

        var inativo = await (await Admin.AlterarAsync($"/api/v1/tecnicos/{tecnico.Id}/inativacao"))
            .LerAsync<TecnicoDetalheDto>();
        inativo.Ativo.ShouldBeFalse();

        var ativo = await (await Admin.AlterarAsync($"/api/v1/tecnicos/{tecnico.Id}/ativacao"))
            .LerAsync<TecnicoDetalheDto>();
        ativo.Ativo.ShouldBeTrue();
    }

    /// <summary>RF0025: filtro por especialidade e por bairro/CEP da area de atendimento.</summary>
    [Fact]
    public async Task Consulta_filtra_por_especialidade_bairro_e_cep()
    {
        var eletrica = await CriarCategoriaAsync("Eletrica");
        var hidraulica = await CriarCategoriaAsync("Hidraulica");

        var doCentro = await CriarTecnicoAsync(eletrica.Id, "01000000", "01999999", "Centro");
        await CriarTecnicoAsync(hidraulica.Id, "02000000", "02999999", "Pinheiros");

        var porEspecialidade = await (await Admin.GetAsync(
                $"/api/v1/tecnicos?especialidadeId={eletrica.Id}"))
            .LerAsync<IReadOnlyCollection<TecnicoResumoDto>>();

        porEspecialidade.Count.ShouldBe(1);
        porEspecialidade.Single().Id.ShouldBe(doCentro.Id);

        var porBairro = await (await Admin.GetAsync("/api/v1/tecnicos?bairro=Pinheiros"))
            .LerAsync<IReadOnlyCollection<TecnicoResumoDto>>();

        porBairro.Count.ShouldBe(1);

        var porCep = await (await Admin.GetAsync("/api/v1/tecnicos?cep=01043000"))
            .LerAsync<IReadOnlyCollection<TecnicoResumoDto>>();

        porCep.ShouldContain(t => t.Id == doCentro.Id);
    }

    /// <summary>RF0026: a lista de especialidades e substituida por completo.</summary>
    [Fact]
    public async Task Definir_especialidades_substitui_a_lista()
    {
        var eletrica = await CriarCategoriaAsync("Eletrica");
        var hidraulica = await CriarCategoriaAsync("Hidraulica");
        var tecnico = await CriarTecnicoAsync(eletrica.Id);

        var atualizado = await (await Admin.ColocarAsync(
                $"/api/v1/tecnicos/{tecnico.Id}/especialidades",
                new DefinirEspecialidadesCommand([eletrica.Id, hidraulica.Id])))
            .LerAsync<TecnicoDetalheDto>();

        atualizado.Especialidades.Count.ShouldBe(2);
    }

    /// <summary>RN0021: o tecnico nao pode ficar sem especialidade.</summary>
    [Fact]
    public async Task Definir_especialidades_com_lista_vazia_e_recusado()
    {
        var categoria = await CriarCategoriaAsync();
        var tecnico = await CriarTecnicoAsync(categoria.Id);

        var resposta = await Admin.ColocarAsync(
            $"/api/v1/tecnicos/{tecnico.Id}/especialidades",
            new DefinirEspecialidadesCommand([]));

        resposta.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>RNF0032: documento de certificacao em PDF ou imagem.</summary>
    [Fact]
    public async Task Documento_em_pdf_e_aceito_e_docx_e_recusado()
    {
        var categoria = await CriarCategoriaAsync();
        var tecnico = await CriarTecnicoAsync(categoria.Id);

        using var pdf = new MultipartFormDataContent();
        var conteudoPdf = new ByteArrayContent("%PDF-1.4 conteudo"u8.ToArray());
        conteudoPdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        pdf.Add(conteudoPdf, "arquivo", "certificado.pdf");

        await (await Admin.PostAsync($"/api/v1/tecnicos/{tecnico.Id}/documentos", pdf))
            .DeveTerStatusAsync(HttpStatusCode.Created);

        using var docx = new MultipartFormDataContent();
        var conteudoDocx = new ByteArrayContent("PK conteudo"u8.ToArray());
        conteudoDocx.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        docx.Add(conteudoDocx, "arquivo", "certificado.docx");

        var recusado = await Admin.PostAsync($"/api/v1/tecnicos/{tecnico.Id}/documentos", docx);

        recusado.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Consulta_de_tecnico_inexistente_devolve_404()
    {
        var resposta = await Admin.GetAsync($"/api/v1/tecnicos/{Guid.NewGuid()}");

        await resposta.DeveTerStatusAsync(HttpStatusCode.NotFound);
    }
}
