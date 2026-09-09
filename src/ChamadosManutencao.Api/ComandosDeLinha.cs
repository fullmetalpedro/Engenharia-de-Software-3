using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Avaliacoes;
using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Financeiro;
using ChamadosManutencao.Domain.Pessoas;
using ChamadosManutencao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Api;

/// <summary>
/// Comandos utilitarios executados fora do pipeline HTTP:
/// <c>dotnet run --project src/ChamadosManutencao.Api -- seed | criar-admin</c>.
/// </summary>
internal static class ComandosDeLinha
{
    private const string SenhaPadrao = "Senha@123";

    private static readonly string[] Comandos = ["seed", "criar-admin"];

    /// <summary>Diz se o primeiro argumento e um comando desta classe, e nao um argumento do host.</summary>
    public static bool EhComandoConhecido(string argumento) =>
        Comandos.Contains(argumento.Trim().ToLowerInvariant());

    public static async Task ExecutarAsync(WebApplication app, string[] args)
    {
        var comando = args[0].Trim().ToLowerInvariant();

        using var escopo = app.Services.CreateScope();
        var provedor = escopo.ServiceProvider;
        var registrador = provedor.GetRequiredService<ILoggerFactory>().CreateLogger("ComandosDeLinha");
        var contexto = provedor.GetRequiredService<AppDbContext>();

        await contexto.Database.MigrateAsync();

        switch (comando)
        {
            case "seed":
                if (!app.Environment.IsDevelopment())
                {
                    registrador.LogError(
                        "O comando 'seed' so roda em Development. Ambiente atual: {Ambiente}.",
                        app.Environment.EnvironmentName);
                    Environment.ExitCode = 1;
                    return;
                }

                await SemearAsync(provedor, contexto, registrador);
                return;

            case "criar-admin":
                await CriarAdministradorInicialAsync(provedor, contexto, registrador);
                return;

            default:
                registrador.LogError(
                    "Comando '{Comando}' desconhecido. Comandos validos: seed, criar-admin.",
                    comando);
                Environment.ExitCode = 1;
                return;
        }
    }

    // ---------- criar-admin ----------

    /// <summary>
    /// Cria o primeiro administrador a partir das variaveis de ambiente ADMIN_INICIAL_*.
    /// Idempotente: se ja existe algum administrador, nao faz nada.
    /// </summary>
    private static async Task CriarAdministradorInicialAsync(
        IServiceProvider provedor,
        AppDbContext contexto,
        ILogger registrador)
    {
        if (await contexto.Administradores.AnyAsync())
        {
            registrador.LogInformation("Ja existe administrador cadastrado. Nada a fazer.");
            return;
        }

        var email = Environment.GetEnvironmentVariable("ADMIN_INICIAL_EMAIL");
        var senha = Environment.GetEnvironmentVariable("ADMIN_INICIAL_SENHA");
        var cpf = Environment.GetEnvironmentVariable("ADMIN_INICIAL_CPF");
        var nome = Environment.GetEnvironmentVariable("ADMIN_INICIAL_NOME");
        var matricula = Environment.GetEnvironmentVariable("ADMIN_INICIAL_MATRICULA");
        var telefone = Environment.GetEnvironmentVariable("ADMIN_INICIAL_TELEFONE") ?? "1130000000";

        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(senha)
            || string.IsNullOrWhiteSpace(cpf)
            || string.IsNullOrWhiteSpace(nome)
            || string.IsNullOrWhiteSpace(matricula))
        {
            registrador.LogError(
                "Defina ADMIN_INICIAL_EMAIL, ADMIN_INICIAL_SENHA, ADMIN_INICIAL_CPF, "
                + "ADMIN_INICIAL_NOME e ADMIN_INICIAL_MATRICULA antes de rodar 'criar-admin'.");
            Environment.ExitCode = 1;
            return;
        }

        var politica = provedor.GetRequiredService<IPoliticaDeSenha>();

        if (!politica.EhForte(senha, out var motivo))
        {
            registrador.LogError("ADMIN_INICIAL_SENHA rejeitada: {Motivo}", motivo);
            Environment.ExitCode = 1;
            return;
        }

        var hash = provedor.GetRequiredService<IServicoDeHashDeSenha>();
        var geradorId = provedor.GetRequiredService<IGeradorId>();

        var administrador = new Administrador(
            geradorId.NovoId(),
            matricula,
            nome,
            cpf,
            email,
            telefone,
            hash.GerarHash(senha));

        contexto.Administradores.Add(administrador);
        await contexto.SaveChangesAsync();

        registrador.LogInformation(
            "Administrador inicial criado: {Email} (matricula {Matricula}).",
            administrador.Email,
            administrador.Matricula);
    }

    // ---------- seed ----------

    /// <summary>
    /// Popula a base com um conjunto de dados de demonstracao. Idempotente: a presenca de
    /// qualquer chamado indica que o seed ja rodou.
    /// </summary>
    private static async Task SemearAsync(
        IServiceProvider provedor,
        AppDbContext contexto,
        ILogger registrador)
    {
        if (await contexto.Chamados.AnyAsync())
        {
            registrador.LogInformation("A base ja contem chamados. Seed ignorado.");
            return;
        }

        var geradorId = provedor.GetRequiredService<IGeradorId>();
        var sequencias = provedor.GetRequiredService<IGeradorDeSequencias>();
        var hash = provedor.GetRequiredService<IServicoDeHashDeSenha>();
        var relogio = provedor.GetRequiredService<IRelogio>();

        var agora = relogio.Agora;
        var senhaHash = hash.GerarHash(SenhaPadrao);
        var sorteio = new Random(20260908);

        var categorias = CriarCategorias(geradorId);
        contexto.CategoriasServico.AddRange(categorias);

        var administrador = await contexto.Administradores.FirstOrDefaultAsync();

        if (administrador is null)
        {
            administrador = new Administrador(
                geradorId.NovoId(),
                "ADM-0001",
                "Regina Souza",
                "11122233344",
                "admin@chamados.local",
                "11930000001",
                senhaHash);

            contexto.Administradores.Add(administrador);
        }

        var tecnicos = await CriarTecnicosAsync(geradorId, sequencias, senhaHash, categorias);
        contexto.Tecnicos.AddRange(tecnicos);

        var clientes = await CriarClientesAsync(geradorId, sequencias, senhaHash);
        contexto.Clientes.AddRange(clientes);

        await contexto.SaveChangesAsync();

        await CriarChamadosAsync(
            contexto,
            geradorId,
            sequencias,
            agora,
            sorteio,
            administrador,
            tecnicos,
            clientes,
            categorias);

        await contexto.SaveChangesAsync();

        registrador.LogInformation(
            "Seed concluido: {Categorias} categorias, {Tecnicos} tecnicos, {Clientes} clientes, "
            + "{Chamados} chamados, {Faturas} faturas. Senha de todos os usuarios: {Senha}.",
            categorias.Count,
            tecnicos.Count,
            clientes.Count,
            await contexto.Chamados.CountAsync(),
            await contexto.Faturas.CountAsync(),
            SenhaPadrao);
    }

    private static List<CategoriaServico> CriarCategorias(IGeradorId geradorId)
    {
        var eletrica = new CategoriaServico(
            geradorId.NovoId(),
            "Eletrica",
            "Instalacoes e reparos eletricos residenciais.",
            exigeFoto: true,
            categoriaDeRisco: true);

        eletrica.AdicionarTipoServico(geradorId.NovoId(), "Troca de disjuntor", "Substituicao de disjuntor no quadro.");
        eletrica.AdicionarTipoServico(geradorId.NovoId(), "Curto-circuito", "Diagnostico e reparo de curto-circuito.");
        eletrica.AdicionarTipoServico(geradorId.NovoId(), "Instalacao de tomada", "Instalacao de ponto de tomada.");

        var hidraulica = new CategoriaServico(
            geradorId.NovoId(),
            "Hidraulica",
            "Reparos em tubulacoes, registros e loucas sanitarias.",
            exigeFoto: true,
            categoriaDeRisco: false);

        hidraulica.AdicionarTipoServico(geradorId.NovoId(), "Vazamento", "Localizacao e reparo de vazamento.");
        hidraulica.AdicionarTipoServico(geradorId.NovoId(), "Desentupimento", "Desentupimento de ralo, pia ou vaso.");
        hidraulica.AdicionarTipoServico(geradorId.NovoId(), "Troca de registro", "Substituicao de registro de agua.");

        var arCondicionado = new CategoriaServico(
            geradorId.NovoId(),
            "Ar-condicionado",
            "Instalacao, limpeza e manutencao de aparelhos de ar-condicionado.",
            exigeFoto: false,
            categoriaDeRisco: false);

        arCondicionado.AdicionarTipoServico(geradorId.NovoId(), "Limpeza de split", "Higienizacao completa do aparelho.");
        arCondicionado.AdicionarTipoServico(geradorId.NovoId(), "Recarga de gas", "Recarga de fluido refrigerante.");

        var eletrodomesticos = new CategoriaServico(
            geradorId.NovoId(),
            "Eletrodomesticos",
            "Conserto de geladeira, maquina de lavar, fogao e microondas.",
            exigeFoto: false,
            categoriaDeRisco: false);

        eletrodomesticos.AdicionarTipoServico(geradorId.NovoId(), "Maquina de lavar", "Diagnostico e conserto de maquina de lavar.");
        eletrodomesticos.AdicionarTipoServico(geradorId.NovoId(), "Geladeira", "Diagnostico e conserto de refrigerador.");

        return [eletrica, hidraulica, arCondicionado, eletrodomesticos];
    }

    private static async Task<List<Tecnico>> CriarTecnicosAsync(
        IGeradorId geradorId,
        IGeradorDeSequencias sequencias,
        string senhaHash,
        List<CategoriaServico> categorias)
    {
        var definicoes = new[]
        {
            (Nome: "Carlos Ribeiro", Cpf: "22233344455", Email: "carlos.ribeiro@chamados.local",
                Telefone: "11930000011", Especialidades: new[] { 0, 1 },
                Bairro: "Centro", CepInicial: "01000000", CepFinal: "01999999", Taxa: 25m),
            (Nome: "Marina Alves", Cpf: "33344455566", Email: "marina.alves@chamados.local",
                Telefone: "11930000012", Especialidades: new[] { 1, 3 },
                Bairro: "Pinheiros", CepInicial: "02000000", CepFinal: "02999999", Taxa: 30m),
            (Nome: "Joao Batista", Cpf: "44455566677", Email: "joao.batista@chamados.local",
                Telefone: "11930000013", Especialidades: new[] { 2, 0 },
                Bairro: "Moema", CepInicial: "03000000", CepFinal: "03999999", Taxa: 35m),
            (Nome: "Fernanda Lima", Cpf: "55566677788", Email: "fernanda.lima@chamados.local",
                Telefone: "11930000014", Especialidades: new[] { 0, 1, 2, 3 },
                Bairro: "Zona Sul", CepInicial: "04000000", CepFinal: "05999999", Taxa: 40m)
        };

        var tecnicos = new List<Tecnico>();

        foreach (var definicao in definicoes)
        {
            var tecnico = new Tecnico(
                geradorId.NovoId(),
                await sequencias.ProximoCodigoDeTecnicoAsync(),
                definicao.Nome,
                definicao.Cpf,
                definicao.Email,
                definicao.Telefone,
                senhaHash,
                definicao.Especialidades.Select(indice => categorias[indice]));

            tecnico.DefinirAreasAtendimento([
                new AreaAtendimento(
                    geradorId.NovoId(),
                    tecnico.Id,
                    definicao.Bairro,
                    definicao.CepInicial,
                    definicao.CepFinal,
                    definicao.Taxa)
            ]);

            tecnicos.Add(tecnico);
        }

        return tecnicos;
    }

    private static async Task<List<Cliente>> CriarClientesAsync(
        IGeradorId geradorId,
        IGeradorDeSequencias sequencias,
        string senhaHash)
    {
        var definicoes = new[]
        {
            (Nome: "Ana Paula Martins", Cpf: "66677788899", Email: "ana.martins@exemplo.com",
                Telefone: "11940000001", Apelido: "Apartamento Centro", Tipo: TipoImovel.Apartamento,
                Logradouro: "Rua Sete de Abril", Numero: "120", Bairro: "Centro",
                Cep: "01043000", Cidade: "Sao Paulo"),
            (Nome: "Bruno Carvalho", Cpf: "77788899900", Email: "bruno.carvalho@exemplo.com",
                Telefone: "11940000002", Apelido: "Casa da familia", Tipo: TipoImovel.Casa,
                Logradouro: "Rua Cardeal Arcoverde", Numero: "45", Bairro: "Pinheiros",
                Cep: "02411000", Cidade: "Sao Paulo"),
            (Nome: "Camila Nogueira", Cpf: "88899900011", Email: "camila.nogueira@exemplo.com",
                Telefone: "11940000003", Apelido: "Cobertura Moema", Tipo: TipoImovel.Apartamento,
                Logradouro: "Alameda dos Nhambiquaras", Numero: "800", Bairro: "Moema",
                Cep: "03078000", Cidade: "Sao Paulo"),
            (Nome: "Diego Fonseca", Cpf: "99900011122", Email: "diego.fonseca@exemplo.com",
                Telefone: "11940000004", Apelido: "Loja da esquina", Tipo: TipoImovel.Comercio,
                Logradouro: "Avenida Santo Amaro", Numero: "2200", Bairro: "Brooklin",
                Cep: "04506000", Cidade: "Sao Paulo"),
            (Nome: "Elaine Prado", Cpf: "10011122233", Email: "elaine.prado@exemplo.com",
                Telefone: "11940000005", Apelido: "Sobrado", Tipo: TipoImovel.Casa,
                Logradouro: "Rua Girassol", Numero: "310", Bairro: "Vila Madalena",
                Cep: "05433000", Cidade: "Sao Paulo"),
            (Nome: "Fabio Teixeira", Cpf: "11122233355", Email: "fabio.teixeira@exemplo.com",
                Telefone: "11940000006", Apelido: "Studio", Tipo: TipoImovel.Apartamento,
                Logradouro: "Rua Augusta", Numero: "1500", Bairro: "Consolacao",
                Cep: "01305100", Cidade: "Sao Paulo")
        };

        var clientes = new List<Cliente>();
        var indice = 0;

        foreach (var definicao in definicoes)
        {
            var clienteId = geradorId.NovoId();

            var imovel = new Imovel(
                geradorId.NovoId(),
                clienteId,
                definicao.Apelido,
                definicao.Tipo,
                definicao.Logradouro,
                definicao.Numero,
                complemento: null,
                definicao.Bairro,
                definicao.Cep,
                definicao.Cidade,
                "SP");

            var cliente = new Cliente(
                clienteId,
                await sequencias.ProximoCodigoDeClienteAsync(),
                definicao.Nome,
                definicao.Cpf,
                definicao.Email,
                definicao.Telefone,
                senhaHash,
                [imovel]);

            FormaPagamento forma = indice % 2 == 0
                ? new CartaoCredito(
                    geradorId.NovoId(),
                    clienteId,
                    "Cartao principal",
                    principal: true,
                    "Visa",
                    $"{1000 + indice}",
                    $"tok_{clienteId:N}",
                    "12/2030",
                    definicao.Nome)
                : new Pix(
                    geradorId.NovoId(),
                    clienteId,
                    "PIX principal",
                    principal: true,
                    definicao.Email,
                    TipoChavePix.Email);

            cliente.AdicionarFormaPagamento(forma);
            clientes.Add(cliente);
            indice++;
        }

        return clientes;
    }

    /// <summary>
    /// Cria 40 chamados distribuidos nos ultimos 12 meses, levando cada um ate um status
    /// diferente para que toda consulta do sistema tenha dado para devolver.
    /// </summary>
    private static async Task CriarChamadosAsync(
        AppDbContext contexto,
        IGeradorId geradorId,
        IGeradorDeSequencias sequencias,
        DateTimeOffset agora,
        Random sorteio,
        Administrador administrador,
        List<Tecnico> tecnicos,
        List<Cliente> clientes,
        List<CategoriaServico> categorias)
    {
        // Status alvo de cada chamado, na ordem em que sao criados.
        StatusChamado[] alvos =
        [
            .. Enumerable.Repeat(StatusChamado.Aberto, 6),
            .. Enumerable.Repeat(StatusChamado.EmAnalise, 5),
            .. Enumerable.Repeat(StatusChamado.Agendado, 6),
            .. Enumerable.Repeat(StatusChamado.EmAtendimento, 4),
            .. Enumerable.Repeat(StatusChamado.Concluido, 15),
            .. Enumerable.Repeat(StatusChamado.Cancelado, 4)
        ];

        string[] problemas =
        [
            "Disjuntor desarma toda vez que ligo o chuveiro.",
            "Vazamento embaixo da pia da cozinha.",
            "Ar-condicionado do quarto nao gela mais.",
            "Maquina de lavar para no meio do ciclo.",
            "Tomada da sala soltou faisca ao conectar o carregador.",
            "Vaso sanitario entupido desde ontem a noite.",
            "Geladeira fazendo barulho alto e nao resfria.",
            "Registro do banheiro nao fecha por completo."
        ];

        var concluidos = 0;

        for (var indice = 0; indice < alvos.Length; indice++)
        {
            var alvo = alvos[indice];
            var cliente = clientes[indice % clientes.Count];
            var imovel = cliente.Imoveis.First();

            // Tecnico que cobre o CEP do imovel; a categoria sai das especialidades dele.
            var candidatos = tecnicos.Where(t => t.AtendeCep(imovel.Cep)).ToList();
            var tecnico = candidatos[indice % candidatos.Count];
            var categoria = categorias.First(c => tecnico.AtendeCategoria(c.Id));
            var tipoServico = categoria.TiposServico.ElementAt(indice % categoria.TiposServico.Count);

            // Aberturas espalhadas nos ultimos 12 meses, da mais antiga para a mais recente.
            var abertura = agora.AddDays(-350 + (indice * 8)).AddHours(sorteio.Next(-4, 5));

            var chamado = Chamado.Abrir(
                geradorId.NovoId(),
                await sequencias.ProximoNumeroDeChamadoAsync(),
                cliente.Id,
                imovel.Id,
                tipoServico.Id,
                problemas[indice % problemas.Length],
                categoria.CategoriaDeRisco ? Urgencia.Alta : (Urgencia)(1 + (indice % 3)),
                abertura,
                geradorId);

            contexto.Chamados.Add(chamado);

            if (alvo == StatusChamado.Aberto)
            {
                continue;
            }

            if (alvo == StatusChamado.Cancelado)
            {
                chamado.Cancelar(cliente.Id, abertura.AddHours(6), geradorId);
                continue;
            }

            chamado.AtribuirTecnico(
                tecnico,
                categoria.Id,
                imovel.Cep,
                administrador.Id,
                abertura.AddHours(2),
                geradorId);

            if (alvo == StatusChamado.EmAnalise)
            {
                continue;
            }

            var inicioPrevisto = abertura.AddDays(2);

            var agendamento = new Agendamento(
                geradorId.NovoId(),
                chamado.Id,
                inicioPrevisto,
                120,
                OrigemProposta.Tecnico);

            chamado.AdicionarAgendamento(agendamento, tecnico.Id, abertura.AddHours(3), geradorId);
            agendamento.Confirmar(abertura.AddHours(4));

            if (alvo == StatusChamado.Agendado)
            {
                continue;
            }

            chamado.RegistrarInicioDeAtendimento(tecnico.Id, inicioPrevisto, geradorId);

            var atendimento = new Atendimento(geradorId.NovoId(), chamado.Id, tecnico.Id, inicioPrevisto);
            contexto.Atendimentos.Add(atendimento);

            if (alvo == StatusChamado.EmAtendimento)
            {
                continue;
            }

            var orcamentoId = geradorId.NovoId();

            var orcamento = new Orcamento(
                orcamentoId,
                atendimento.Id,
                inicioPrevisto.AddMinutes(20),
                [
                    new ItemOrcamento(
                        geradorId.NovoId(),
                        orcamentoId,
                        "Peca de reposicao",
                        TipoItem.Peca,
                        1,
                        80m + (indice % 5 * 20m)),
                    new ItemOrcamento(
                        geradorId.NovoId(),
                        orcamentoId,
                        "Mao de obra",
                        TipoItem.MaoDeObra,
                        2,
                        60m)
                ]);

            atendimento.RegistrarOrcamento(orcamento);
            orcamento.Aprovar(inicioPrevisto.AddMinutes(40));

            var conclusao = inicioPrevisto.AddHours(2);

            var garantia = atendimento.Concluir(
                "Servico executado e testado com o cliente presente.",
                cliente.Id,
                chamado.ChamadoDeGarantia,
                conclusao,
                geradorId);

            contexto.Garantias.Add(garantia);

            chamado.AlterarStatus(
                StatusChamado.Concluido,
                tecnico.Id,
                "Atendimento concluido.",
                conclusao,
                geradorId);

            var area = tecnico.AreaQueAtende(imovel.Cep);

            var fatura = new Fatura(
                geradorId.NovoId(),
                await sequencias.ProximoNumeroDeFaturaAsync(),
                atendimento.Id,
                cliente.Id,
                chamado.Id,
                conclusao,
                orcamento.CalcularValorPecas(),
                orcamento.CalcularValorMaoObra(),
                area?.TaxaDeslocamento ?? 0m);

            // Uma fatura em cada status, ciclando Paga, Vencida, Cancelada e Emitida.
            switch (concluidos % 4)
            {
                case 0:
                    fatura.RegistrarPagamento(new Pagamento(
                        geradorId.NovoId(),
                        fatura.Id,
                        cliente.FormasPagamento.First().Id,
                        conclusao.AddDays(1),
                        fatura.ValorTotal,
                        $"TRX-{fatura.Numero:000000}",
                        StatusPagamento.Aprovado));
                    break;

                case 1:
                    fatura.RegistrarVencimento(agora);
                    break;

                case 2:
                    fatura.Cancelar();
                    break;
            }

            contexto.Faturas.Add(fatura);

            // Parte dos concluidos recebe avaliacao do cliente.
            if (concluidos % 3 != 2)
            {
                contexto.Avaliacoes.Add(new Avaliacao(
                    geradorId.NovoId(),
                    chamado.Id,
                    cliente.Id,
                    tecnico.Id,
                    3 + (concluidos % 3),
                    concluidos % 2 == 0 ? "Atendimento rapido e bem feito." : null,
                    conclusao.AddDays(2)));
            }

            concluidos++;
        }
    }
}
