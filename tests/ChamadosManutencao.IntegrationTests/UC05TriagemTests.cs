using System.Net;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.Application.UC05Triagem;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.IntegrationTests.Comum;
using Shouldly;
using Xunit;

namespace ChamadosManutencao.IntegrationTests;

/// <summary>
/// UC05 Triar e Atribuir Chamado.
/// Requisitos: RF0044, RF0045, RF0046, RF0047, RF0048, RF0049, RF0050, RN0022, RN0023,
/// RN0033, RN0034.
/// </summary>
[Trait("Escopo", "UC05")]
public class UC05TriagemTests : TesteDeIntegracao
{
    public UC05TriagemTests(AmbienteDeIntegracao ambiente) : base(ambiente)
    {
    }

    /// <summary>RF0044: o administrador ve os chamados de todos os clientes.</summary>
    [Fact]
    public async Task Administrador_consulta_chamados_de_todos_os_clientes()
    {
        var cenario = await MontarCenarioBasicoAsync();
        await AbrirChamadoAsync(cenario);

        var outroCliente = await CadastrarClienteAsync();
        var cenarioDoOutro = cenario with { Cliente = outroCliente };
        await AbrirChamadoAsync(cenarioDoOutro);

        var todos = await (await Admin.GetAsync("/api/v1/chamados"))
            .LerAsync<IReadOnlyCollection<ChamadoResumoDto>>();

        todos.Count.ShouldBe(2);
    }

    /// <summary>RF0044: filtros por status, urgencia, categoria, cliente e numero.</summary>
    [Fact]
    public async Task Consulta_do_administrador_aceita_filtros_combinaveis()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var alta = await AbrirChamadoAsync(cenario);
        await AbrirChamadoAsync(cenario);

        // RF0046: a urgencia vem da triagem, nao da abertura.
        await (await Admin.AlterarAsync(
                $"/api/v1/chamados/{alta.Id}/urgencia",
                new ClassificarUrgenciaCommand(Urgencia.Alta)))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);

        var porNumero = await (await Admin.GetAsync($"/api/v1/chamados?numero={alta.Numero}"))
            .LerAsync<IReadOnlyCollection<ChamadoResumoDto>>();
        porNumero.Count.ShouldBe(1);

        var porUrgencia = await (await Admin.GetAsync("/api/v1/chamados?urgencia=Alta&status=Aberto"))
            .LerAsync<IReadOnlyCollection<ChamadoResumoDto>>();
        porUrgencia.Count.ShouldBe(1);

        var porCategoria = await (await Admin.GetAsync(
                $"/api/v1/chamados?categoriaId={cenario.Categoria.Id}"))
            .LerAsync<IReadOnlyCollection<ChamadoResumoDto>>();
        porCategoria.Count.ShouldBe(2);

        var porCliente = await (await Admin.GetAsync(
                $"/api/v1/chamados?clienteId={cenario.Cliente.Id}"))
            .LerAsync<IReadOnlyCollection<ChamadoResumoDto>>();
        porCliente.Count.ShouldBe(2);
    }

    /// <summary>RF0046.</summary>
    [Fact]
    public async Task Administrador_classifica_a_urgencia_do_chamado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirChamadoAsync(cenario);

        await (await Admin.AlterarAsync(
                $"/api/v1/chamados/{chamado.Id}/urgencia",
                new ClassificarUrgenciaCommand(Urgencia.Alta)))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);

        var detalhe = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}"))
            .LerAsync<ChamadoDetalheDto>();

        detalhe.Urgencia.ShouldBe(Urgencia.Alta);
    }

    /// <summary>RF0047 e RN0034: a atribuicao leva o chamado ABERTO para EM ANALISE.</summary>
    [Fact]
    public async Task Atribuicao_de_tecnico_leva_o_chamado_para_em_analise()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirEAtribuirAsync(cenario);

        var detalhe = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}"))
            .LerAsync<ChamadoDetalheDto>();

        detalhe.Status.ShouldBe(StatusChamado.EmAnalise);
        detalhe.TecnicoId.ShouldBe(cenario.Tecnico.Id);
    }

    /// <summary>RN0022: a categoria do chamado precisa estar entre as especialidades.</summary>
    [Fact]
    public async Task Atribuicao_a_tecnico_sem_a_especialidade_e_recusada()
    {
        var eletrica = await CriarCategoriaAsync("Eletrica");
        var hidraulica = await CriarCategoriaAsync("Hidraulica");
        var tipoHidraulica = await CriarTipoServicoAsync(hidraulica.Id);
        var tecnicoDeEletrica = await CriarTecnicoAsync(eletrica.Id);
        var cliente = await CadastrarClienteAsync();

        var cenario = new CenarioBasico(hidraulica, tipoHidraulica, tecnicoDeEletrica, cliente);
        var chamado = await AbrirChamadoAsync(cenario);

        var resposta = await Admin.PostarAsync(
            $"/api/v1/chamados/{chamado.Id}/atribuicao",
            new AtribuirTecnicoCommand(tecnicoDeEletrica.Id));

        await resposta.DeveTerStatusAsync(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>RN0023: o imovel precisa estar na area de atendimento do tecnico.</summary>
    [Fact]
    public async Task Atribuicao_a_tecnico_fora_da_area_e_recusada()
    {
        var categoria = await CriarCategoriaAsync();
        var tipoServico = await CriarTipoServicoAsync(categoria.Id);
        var tecnicoDeOutraArea = await CriarTecnicoAsync(categoria.Id, "09000000", "09999999", "Santo Andre");
        var cliente = await CadastrarClienteAsync("01043000");

        var cenario = new CenarioBasico(categoria, tipoServico, tecnicoDeOutraArea, cliente);
        var chamado = await AbrirChamadoAsync(cenario);

        var resposta = await Admin.PostarAsync(
            $"/api/v1/chamados/{chamado.Id}/atribuicao",
            new AtribuirTecnicoCommand(tecnicoDeOutraArea.Id));

        await resposta.DeveTerStatusAsync(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>RF0023: tecnico inativo nao recebe chamado.</summary>
    [Fact]
    public async Task Atribuicao_a_tecnico_inativo_e_recusada()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirChamadoAsync(cenario);

        await (await Admin.AlterarAsync($"/api/v1/tecnicos/{cenario.Tecnico.Id}/inativacao"))
            .DeveTerStatusAsync(HttpStatusCode.OK);

        var resposta = await Admin.PostarAsync(
            $"/api/v1/chamados/{chamado.Id}/atribuicao",
            new AtribuirTecnicoCommand(cenario.Tecnico.Id));

        await resposta.DeveTerStatusAsync(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>RF0048.</summary>
    [Fact]
    public async Task Reatribuicao_troca_o_tecnico_do_chamado()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var substituto = await CriarTecnicoAsync(cenario.Categoria.Id);
        var chamado = await AbrirEAtribuirAsync(cenario);

        await (await Admin.ColocarAsync(
                $"/api/v1/chamados/{chamado.Id}/atribuicao",
                new AtribuirTecnicoCommand(substituto.Id)))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);

        var detalhe = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}"))
            .LerAsync<ChamadoDetalheDto>();

        detalhe.TecnicoId.ShouldBe(substituto.Id);
    }

    /// <summary>RF0049 e RN0034: transicao valida.</summary>
    [Fact]
    public async Task Alteracao_de_status_segue_a_maquina_de_estados()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirChamadoAsync(cenario);

        await (await Admin.AlterarAsync(
                $"/api/v1/chamados/{chamado.Id}/status",
                new AlterarStatusCommand(StatusChamado.EmAnalise, "Triagem manual.")))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);

        var detalhe = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}"))
            .LerAsync<ChamadoDetalheDto>();

        detalhe.Status.ShouldBe(StatusChamado.EmAnalise);
    }

    /// <summary>RN0034: ABERTO nao vai direto para CONCLUIDO.</summary>
    [Fact]
    public async Task Transicao_de_status_invalida_e_recusada()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var chamado = await AbrirChamadoAsync(cenario);

        var resposta = await Admin.AlterarAsync(
            $"/api/v1/chamados/{chamado.Id}/status",
            new AlterarStatusCommand(StatusChamado.Concluido));

        await resposta.DeveTerStatusAsync(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>RN0033: o administrador cancela mesmo depois do inicio do atendimento.</summary>
    [Fact]
    public async Task Cancelamento_administrativo_funciona_com_o_chamado_em_atendimento()
    {
        var cenario = await MontarCenarioBasicoAsync();
        var (chamado, _) = await IniciarAtendimentoAsync(cenario);

        await (await Admin.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/cancelamento-administrativo",
                new CancelamentoAdministrativoCommand("Cliente pediu o cancelamento por telefone.")))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);

        var detalhe = await (await Admin.GetAsync($"/api/v1/chamados/{chamado.Id}"))
            .LerAsync<ChamadoDetalheDto>();

        detalhe.Status.ShouldBe(StatusChamado.Cancelado);
    }
}
