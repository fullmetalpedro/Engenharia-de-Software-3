using System.Net;
using System.Net.Http.Headers;
using ChamadosManutencao.Application.Autenticacao;
using ChamadosManutencao.Application.UC01Clientes;
using ChamadosManutencao.Application.UC02Tecnicos;
using ChamadosManutencao.Application.UC03Catalogo;
using ChamadosManutencao.Application.UC04Chamados;
using ChamadosManutencao.Application.UC05Triagem;
using ChamadosManutencao.Application.UC06Agendamento;
using ChamadosManutencao.Application.UC07Atendimento;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Pessoas;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChamadosManutencao.IntegrationTests.Comum;

/// <summary>
/// Base das classes de teste: base limpa antes de cada teste, um administrador ja autenticado
/// e os atalhos para montar o cenario (cliente, tecnico, catalogo) pela propria API.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public abstract class TesteDeIntegracao : IAsyncLifetime
{
    public const string SenhaPadrao = "Senha@123";

    private int _sequencial;

    protected TesteDeIntegracao(AmbienteDeIntegracao ambiente) => Ambiente = ambiente;

    protected AmbienteDeIntegracao Ambiente { get; }

    /// <summary>Cliente HTTP sem token, para verificar o 401 (secao 9 da arquitetura).</summary>
    protected HttpClient Anonimo { get; private set; } = null!;

    protected HttpClient Admin { get; private set; } = null!;

    protected Guid AdminId { get; private set; }

    public async Task InitializeAsync()
    {
        await Ambiente.LimparBaseAsync();

        Anonimo = Ambiente.CreateClient();

        var (id, cliente) = await CriarAdministradorAsync();
        AdminId = id;
        Admin = cliente;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- autenticacao ----------

    protected async Task<HttpClient> AutenticarAsync(string email, string senha = SenhaPadrao)
    {
        var resposta = await Anonimo.PostarAsync("/api/v1/auth/login", new LoginCommand(email, senha));
        var token = await resposta.LerAsync<TokenResponse>();

        var cliente = Ambiente.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        return cliente;
    }

    /// <summary>
    /// O administrador nasce direto no banco: nao ha endpoint publico de cadastro de
    /// administrador, so o comando de linha criar-admin.
    /// </summary>
    protected async Task<(Guid Id, HttpClient Cliente)> CriarAdministradorAsync(string? email = null)
    {
        email ??= $"admin{Proximo()}@chamados.local";

        var id = await Ambiente.ComContextoAsync(async contexto =>
        {
            using var escopo = Ambiente.Services.CreateScope();
            var hash = escopo.ServiceProvider.GetRequiredService<IServicoDeHashDeSenha>();

            var administrador = new Administrador(
                Guid.CreateVersion7(),
                $"ADM-{Proximo():0000}",
                "Administrador de Teste",
                Cpf(),
                email,
                "11930000000",
                hash.GerarHash(SenhaPadrao));

            contexto.Administradores.Add(administrador);
            await contexto.SaveChangesAsync();

            return administrador.Id;
        });

        return (id, await AutenticarAsync(email));
    }

    // ---------- cenario ----------

    protected async Task<CenarioDeCliente> CadastrarClienteAsync(
        string cep = "01043000",
        string bairro = "Centro",
        TipoImovel tipoImovel = TipoImovel.Apartamento)
    {
        var email = $"cliente{Proximo()}@exemplo.com";

        var comando = new CadastrarClienteCommand(
            "Cliente de Teste",
            Cpf(),
            email,
            "11940000000",
            SenhaPadrao,
            [
                new ImovelCommand(
                    "Imovel principal",
                    tipoImovel,
                    "Rua das Flores",
                    "100",
                    null,
                    bairro,
                    cep,
                    "Sao Paulo",
                    "SP")
            ]);

        var resposta = await Anonimo.PostarAsync("/api/v1/clientes", comando);
        var dto = await resposta.LerAsync<ClienteDetalheDto>();

        return new CenarioDeCliente(dto, email, await AutenticarAsync(email));
    }

    protected async Task<CategoriaDto> CriarCategoriaAsync(
        string nome = "Eletrica",
        bool exigeFoto = false,
        bool categoriaDeRisco = false)
    {
        var resposta = await Admin.PostarAsync(
            "/api/v1/categorias-servico",
            new CategoriaCommand($"{nome} {Proximo()}", "Categoria de teste.", exigeFoto, categoriaDeRisco));

        return await resposta.LerAsync<CategoriaDto>();
    }

    protected async Task<TipoServicoDto> CriarTipoServicoAsync(Guid categoriaId, string nome = "Servico")
    {
        var resposta = await Admin.PostarAsync(
            $"/api/v1/categorias-servico/{categoriaId}/tipos-servico",
            new TipoServicoCommand($"{nome} {Proximo()}", "Tipo de servico de teste."));

        return await resposta.LerAsync<TipoServicoDto>();
    }

    protected async Task<CenarioDeTecnico> CriarTecnicoAsync(
        Guid especialidadeId,
        string cepInicial = "01000000",
        string cepFinal = "01999999",
        string bairro = "Centro",
        decimal taxaDeslocamento = 25m)
    {
        var email = $"tecnico{Proximo()}@chamados.local";

        var comando = new CadastrarTecnicoCommand(
            "Tecnico de Teste",
            Cpf(),
            email,
            "11930000001",
            SenhaPadrao,
            [especialidadeId],
            [new AreaAtendimentoCommand(bairro, cepInicial, cepFinal, taxaDeslocamento)]);

        var resposta = await Admin.PostarAsync("/api/v1/tecnicos", comando);
        var dto = await resposta.LerAsync<TecnicoDetalheDto>();

        return new CenarioDeTecnico(dto, email, await AutenticarAsync(email));
    }

    /// <summary>Catalogo + tecnico + cliente prontos, que a maioria dos testes precisa.</summary>
    protected async Task<CenarioBasico> MontarCenarioBasicoAsync(
        bool exigeFoto = false,
        bool categoriaDeRisco = false)
    {
        var categoria = await CriarCategoriaAsync(exigeFoto: exigeFoto, categoriaDeRisco: categoriaDeRisco);
        var tipoServico = await CriarTipoServicoAsync(categoria.Id);
        var tecnico = await CriarTecnicoAsync(categoria.Id);
        var cliente = await CadastrarClienteAsync();

        return new CenarioBasico(categoria, tipoServico, tecnico, cliente);
    }

    // ---------- fluxo do chamado ----------

    /// <summary>Abre um chamado como cliente, em JSON (sem anexos).</summary>
    protected async Task<ChamadoCriadoDto> AbrirChamadoAsync(
        CenarioBasico cenario,
        bool indicacaoDeRisco = false,
        string descricao = "Problema relatado pelo cliente no teste.")
    {
        var comando = new AbrirChamadoCommand(
            cenario.Cliente.ImovelId,
            cenario.Categoria.Id,
            cenario.TipoServico.Id,
            descricao,
            indicacaoDeRisco);

        var resposta = await cenario.Cliente.Http.PostarAsync("/api/v1/chamados", comando);

        return await resposta.LerAsync<ChamadoCriadoDto>();
    }

    /// <summary>Abre o chamado em multipart, com uma foto anexada (RN0032).</summary>
    protected async Task<HttpResponseMessage> AbrirChamadoComFotoAsync(
        CenarioBasico cenario,
        int quantidadeDeFotos = 1,
        bool indicacaoDeRisco = false)
    {
        using var formulario = new MultipartFormDataContent
        {
            { new StringContent(cenario.Cliente.ImovelId.ToString()), "imovelId" },
            { new StringContent(cenario.Categoria.Id.ToString()), "categoriaServicoId" },
            { new StringContent(cenario.TipoServico.Id.ToString()), "tipoServicoId" },
            { new StringContent("Equipamento com problema, foto anexada."), "descricaoProblema" },
            { new StringContent(indicacaoDeRisco.ToString()), "indicacaoDeRisco" }
        };

        for (var indice = 0; indice < quantidadeDeFotos; indice++)
        {
            formulario.Add(ConteudoDeFoto(), "arquivos", $"foto{indice}.jpg");
        }

        return await cenario.Cliente.Http.PostAsync("/api/v1/chamados", formulario);
    }

    /// <summary>Bytes de uma imagem minima, suficiente para o teste de upload.</summary>
    protected static ByteArrayContent ConteudoDeFoto(string tipoMime = "image/jpeg")
    {
        var conteudo = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46]);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue(tipoMime);

        return conteudo;
    }

    /// <summary>Chamado ABERTO, com tecnico atribuido (EM ANALISE).</summary>
    protected async Task<ChamadoCriadoDto> AbrirEAtribuirAsync(CenarioBasico cenario)
    {
        var chamado = await AbrirChamadoAsync(cenario);

        await (await Admin.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/atribuicao",
                new AtribuirTecnicoCommand(cenario.Tecnico.Id)))
            .DeveTerStatusAsync(HttpStatusCode.NoContent);

        return chamado;
    }

    /// <summary>Chamado AGENDADO, com o agendamento ja confirmado pelo cliente.</summary>
    protected async Task<(ChamadoCriadoDto Chamado, AgendamentoDto Agendamento)> AgendarAsync(
        CenarioBasico cenario,
        DateTimeOffset? dataHora = null)
    {
        var chamado = await AbrirEAtribuirAsync(cenario);

        var agendamento = await (await cenario.Tecnico.Http.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/agendamentos",
                new AgendarCommand(dataHora ?? DateTimeOffset.UtcNow.AddDays(1), 120)))
            .LerAsync<AgendamentoDto>();

        await (await cenario.Cliente.Http.PostarAsync($"/api/v1/agendamentos/{agendamento.Id}/confirmacao"))
            .DeveTerStatusAsync(HttpStatusCode.OK);

        return (chamado, agendamento);
    }

    /// <summary>Chamado EM ATENDIMENTO, com o atendimento aberto pelo tecnico.</summary>
    protected async Task<(ChamadoCriadoDto Chamado, AtendimentoDto Atendimento)> IniciarAtendimentoAsync(
        CenarioBasico cenario)
    {
        var (chamado, _) = await AgendarAsync(cenario);

        var atendimento = await (await cenario.Tecnico.Http.PostarAsync(
                $"/api/v1/chamados/{chamado.Id}/atendimento"))
            .LerAsync<AtendimentoDto>();

        return (chamado, atendimento);
    }

    /// <summary>
    /// Chamado CONCLUIDO com orcamento aprovado: a conclusao gera a garantia e a fatura.
    /// </summary>
    protected async Task<(ChamadoCriadoDto Chamado, AtendimentoDto Atendimento, ResultadoDaConclusaoDto Conclusao)>
        ConcluirAtendimentoAsync(CenarioBasico cenario, decimal valorPeca = 100m, decimal valorMaoObra = 200m)
    {
        var (chamado, atendimento) = await IniciarAtendimentoAsync(cenario);

        var orcamento = await (await cenario.Tecnico.Http.PostarAsync(
                $"/api/v1/atendimentos/{atendimento.Id}/orcamento",
                new RegistrarOrcamentoCommand(
                [
                    new ItemOrcamentoCommand("Peca de reposicao", TipoItem.Peca, 1, valorPeca),
                    new ItemOrcamentoCommand("Mao de obra", TipoItem.MaoDeObra, 1, valorMaoObra)
                ])))
            .LerAsync<OrcamentoDto>();

        await (await cenario.Cliente.Http.PostarAsync(
                $"/api/v1/orcamentos/{orcamento.Id}/decisao",
                new DecisaoDeOrcamentoCommand("Aprovado")))
            .DeveTerStatusAsync(HttpStatusCode.OK);

        var conclusao = await (await cenario.Tecnico.Http.PostarAsync(
                $"/api/v1/atendimentos/{atendimento.Id}/conclusao",
                new ConcluirAtendimentoCommand("Servico executado e testado com o cliente.")))
            .LerAsync<ResultadoDaConclusaoDto>();

        return (chamado, atendimento, conclusao);
    }

    // ---------- utilitarios ----------

    /// <summary>CPF de 11 digitos unico dentro da execucao (o dominio nao valida digito).</summary>
    protected string Cpf() => $"{DateTime.UtcNow.Ticks % 100000000:00000000}{Proximo():000}";

    protected int Proximo() => Interlocked.Increment(ref _sequencial);
}

/// <summary>Resposta da abertura de chamado (RF0041).</summary>
public sealed record ChamadoCriadoDto(Guid Id, long Numero, StatusChamado Status, Urgencia Urgencia);

/// <summary>Cliente cadastrado e ja autenticado.</summary>
public sealed record CenarioDeCliente(ClienteDetalheDto Dados, string Email, HttpClient Http)
{
    public Guid Id => Dados.Id;

    public Guid ImovelId => Dados.Imoveis.First().Id;
}

/// <summary>Tecnico cadastrado e ja autenticado.</summary>
public sealed record CenarioDeTecnico(TecnicoDetalheDto Dados, string Email, HttpClient Http)
{
    public Guid Id => Dados.Id;
}

/// <summary>Catalogo, tecnico e cliente prontos para abrir um chamado.</summary>
public sealed record CenarioBasico(
    CategoriaDto Categoria,
    TipoServicoDto TipoServico,
    CenarioDeTecnico Tecnico,
    CenarioDeCliente Cliente);
