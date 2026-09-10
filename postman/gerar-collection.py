"""Gera a collection do Postman a partir do documento OpenAPI servido pela API.

Com a API no ar:

    curl -s http://localhost:5080/openapi/v1.json -o openapi.json
    python postman/gerar-collection.py openapi.json         postman/ChamadosManutencao.postman_collection.json         postman/Chamados-local.postman_environment.json

O que o gerador acrescenta ao que vem do OpenAPI: pasta de bootstrap, jornada completa,
token por papel em cada requisicao, exemplos de corpo preenchidos e scripts que encadeiam
os identificadores de uma requisicao para a seguinte.
"""
import collections
import json
import sys

NL = "\n"

ENTRADA, SAIDA_COLLECTION, SAIDA_AMBIENTE = sys.argv[1], sys.argv[2], sys.argv[3]

doc = json.load(open(ENTRADA, encoding="utf-8"), object_pairs_hook=collections.OrderedDict)
SCHEMAS = doc["components"]["schemas"]

# ---------------------------------------------------------------- variaveis

VARIAVEL_DE_ROTA = [
    ("/api/v1/clientes/{id}/formas-pagamento/{formaId}", {"id": "clienteId", "formaId": "formaPagamentoId"}),
    ("/api/v1/clientes/{id}/imoveis/{imovelId}", {"id": "clienteId", "imovelId": "imovelId"}),
    ("/api/v1/clientes/{id}", {"id": "clienteId"}),
    ("/api/v1/tecnicos/{id}", {"id": "tecnicoId"}),
    ("/api/v1/categorias-servico/{id}", {"id": "categoriaId"}),
    ("/api/v1/tipos-servico/{id}", {"id": "tipoServicoId"}),
    ("/api/v1/chamados/{id}", {"id": "chamadoId"}),
    ("/api/v1/agendamentos/{id}", {"id": "agendamentoId"}),
    ("/api/v1/atendimentos/{id}", {"id": "atendimentoId"}),
    ("/api/v1/orcamentos/{id}", {"id": "orcamentoId"}),
    ("/api/v1/faturas/{id}", {"id": "faturaId"}),
    ("/api/v1/avaliacoes/{id}", {"id": "avaliacaoId"}),
]


def variaveis_da_rota(rota):
    for prefixo, mapa in VARIAVEL_DE_ROTA:
        if rota.startswith(prefixo):
            return mapa
    return {}


# ---------------------------------------------------------------- exemplos

EXEMPLO_POR_NOME = {
    "nomeCompleto": "Ana Paula Martins",
    "cpf": "66677788899",
    "email": "ana.martins@exemplo.com",
    "telefone": "11940000001",
    "senha": "Senha@123",
    "senhaAtual": "{{senha}}",
    "novaSenha": "NovaSenha@2026",
    "apelido": "Apartamento do Centro",
    "logradouro": "Rua Sete de Abril",
    "numero": "120",
    "complemento": "Apto 71",
    "bairro": "Centro",
    "cep": "01043000",
    "cidade": "Sao Paulo",
    "estado": "SP",
    "nome": "{{novoNome}}",
    "descricao": "Cadastrado pela collection do Postman.",
    "descricaoProblema": "Vazamento embaixo da pia da cozinha desde ontem.",
    "motivo": "Cliente pediu o cancelamento por telefone.",
    "observacao": "Triagem manual pelo administrador.",
    "relatoTecnico": "Registro trocado e teste de pressao aprovado.",
    "texto": "Lamentamos o ocorrido e ja ajustamos a rota da equipe.",
    "comentario": "Chegou no horario e resolveu.",
    "nota": 5,
    "decisao": "Aprovado",
    "solicitarNovoOrcamento": False,
    "quantidade": 1,
    "valorUnitario": 90.00,
    "taxaDeslocamento": 25.00,
    "cepInicial": "01000000",
    "cepFinal": "01999999",
    "duracaoEmMinutos": 120,
    "dataHoraProposta": "{{dataAgendamento}}",
    "tipo": "CartaoCredito",
    "principal": True,
    "numeroCartao": "4111111111111111",
    "nomeTitular": "ANA P MARTINS",
    "validade": "12/2030",
    "codigoSeguranca": "123",
    "chavePix": None,
    "nomeSacado": None,
    "emailEnvio": None,
    "exigeFoto": True,
    "categoriaDeRisco": False,
    "indicacaoDeRisco": True,
    "imovelId": "{{imovelId}}",
    "categoriaServicoId": "{{categoriaId}}",
    "tipoServicoId": "{{tipoServicoId}}",
    "tecnicoId": "{{tecnicoId}}",
    "formaPagamentoId": "{{formaPagamentoId}}",
    "especialidadeIds": ["{{categoriaId}}"],
}

CORPO_MANUAL = {
    ("post", "/api/v1/auth/login"): {"email": "{{emailAdministrador}}", "senha": "{{senha}}"},
    ("post", "/api/v1/clientes"): {
        "nomeCompleto": "Cliente Novo",
        "cpf": "{{novoCpf}}",
        "email": "{{novoEmail}}",
        "telefone": "11940000001",
        "senha": "Senha@123",
        "imoveis": [{
            "apelido": "Apartamento do Centro",
            "tipoImovel": "Apartamento",
            "logradouro": "Rua Sete de Abril",
            "numero": "120",
            "complemento": "Apto 71",
            "bairro": "Centro",
            "cep": "01043000",
            "cidade": "Sao Paulo",
            "estado": "SP",
        }],
    },
    ("post", "/api/v1/tecnicos"): {
        "nomeCompleto": "Tecnico Novo",
        "cpf": "{{novoCpf}}",
        "email": "{{novoEmail}}",
        "telefone": "11930000011",
        "senha": "Senha@123",
        "especialidadeIds": ["{{categoriaId}}"],
        "areasAtendimento": [
            {"bairro": "Centro", "cepInicial": "01000000", "cepFinal": "01999999", "taxaDeslocamento": 25.00}
        ],
    },
    ("put", "/api/v1/tecnicos/{id}"): {
        "nomeCompleto": "Tecnico Alterado",
        "email": "{{novoEmail}}",
        "telefone": "11930000012",
    },
    ("put", "/api/v1/clientes/{id}"): {
        "nomeCompleto": "Cliente Alterado",
        "email": "{{novoEmail}}",
        "telefone": "11940000002",
    },
    ("put", "/api/v1/tecnicos/{id}/areas-atendimento"): {
        "areas": [
            {"bairro": "Centro", "cepInicial": "01000000", "cepFinal": "01999999", "taxaDeslocamento": 25.00},
            {"bairro": "Pinheiros", "cepInicial": "05400000", "cepFinal": "05499999", "taxaDeslocamento": 35.00},
        ]
    },
    ("post", "/api/v1/atendimentos/{id}/orcamento"): {
        "itens": [
            {"descricao": "Registro de gaveta 1/2", "tipo": "Peca", "quantidade": 1, "valorUnitario": 90.00},
            {"descricao": "Mao de obra", "tipo": "MaoDeObra", "quantidade": 2, "valorUnitario": 75.00},
        ]
    },
    ("post", "/api/v1/clientes/{id}/formas-pagamento"): {
        "tipo": "CartaoCredito",
        "apelido": "Cartao principal",
        "principal": True,
        "numeroCartao": "4111111111111111",
        "nomeTitular": "ANA P MARTINS",
        "validade": "12/2030",
        "codigoSeguranca": "123",
    },
}


def resolver(schema):
    if "$ref" in schema:
        return SCHEMAS[schema["$ref"].rsplit("/", 1)[-1]]
    if "oneOf" in schema:
        for alternativa in schema["oneOf"]:
            resolvido = resolver(alternativa)
            if resolvido.get("enum") or resolvido.get("type") not in (None, "null"):
                return resolvido
    return schema


def exemplo(schema, nome=None):
    schema = resolver(schema)

    if nome in EXEMPLO_POR_NOME:
        return EXEMPLO_POR_NOME[nome]

    if "enum" in schema:
        valores = [v for v in schema["enum"] if v is not None]
        return schema.get("default", valores[0] if valores else None)

    tipos = schema.get("type", "string")
    tipos = [tipos] if isinstance(tipos, str) else [t for t in tipos if t != "null"]
    tipo = tipos[0] if tipos else "string"

    if tipo == "object":
        return {p: exemplo(sub, p) for p, sub in schema.get("properties", {}).items()}
    if tipo == "array":
        return [exemplo(schema.get("items", {}), None)]
    if tipo == "boolean":
        return False
    if tipo == "integer":
        return 1
    if tipo == "number":
        return 100.00
    if schema.get("format") == "uuid":
        return "00000000-0000-0000-0000-000000000000"
    if schema.get("format") == "date-time":
        return "{{dataAgendamento}}"
    return "texto"


# ---------------------------------------------------------------- scripts

TESTE_PADRAO = (
    'pm.test("Status de sucesso", function () {' + NL
    + "    pm.expect(pm.response.code).to.be.oneOf([200, 201, 204]);" + NL
    + "});"
)

CAPTURA = {
    ("post", "/api/v1/auth/login"): (
        "const corpo = pm.response.json();" + NL
        + 'pm.collectionVariables.set("token", corpo.accessToken);' + NL
        + 'pm.collectionVariables.set("token" + corpo.papel, corpo.accessToken);' + NL
        + NL
        + "// O identificador do usuario vem na claim sub do proprio token." + NL
        + "const conteudo = JSON.parse(atob(corpo.accessToken.split('.')[1]"
        + ".replace(/-/g, '+').replace(/_/g, '/')));" + NL
        + 'if (corpo.papel === "Cliente") pm.collectionVariables.set("clienteId", conteudo.sub);' + NL
        + 'if (corpo.papel === "Tecnico") pm.collectionVariables.set("tecnicoId", conteudo.sub);' + NL
        + 'if (corpo.papel === "Administrador") pm.collectionVariables.set("administradorId", conteudo.sub);' + NL
        + 'console.log("Token ativo: " + corpo.papel + " (" + conteudo.sub + ")");'
    ),
    ("post", "/api/v1/auth/refresh"):
        'pm.collectionVariables.set("token", pm.response.json().accessToken);',
    ("post", "/api/v1/clientes"): (
        "const c = pm.response.json();" + NL
        + "// Guarda em variavel propria: clienteId continua sendo o do token em uso." + NL
        + 'pm.collectionVariables.set("clienteNovoId", c.id);' + NL
        + 'console.log("Cliente criado: " + c.codigoCliente + " / " + c.email);'
    ),
    ("post", "/api/v1/clientes/{id}/imoveis"):
        'pm.collectionVariables.set("imovelId", pm.response.json().id);',
    ("post", "/api/v1/clientes/{id}/formas-pagamento"):
        'pm.collectionVariables.set("formaPagamentoId", pm.response.json().id);',
    ("post", "/api/v1/tecnicos"):
        'pm.collectionVariables.set("tecnicoId", pm.response.json().id);',
    ("post", "/api/v1/categorias-servico"):
        'pm.collectionVariables.set("categoriaId", pm.response.json().id);',
    ("post", "/api/v1/categorias-servico/{id}/tipos-servico"):
        'pm.collectionVariables.set("tipoServicoId", pm.response.json().id);',
    ("post", "/api/v1/chamados"): (
        "const ch = pm.response.json();" + NL
        + 'pm.collectionVariables.set("chamadoId", ch.id);' + NL
        + 'pm.collectionVariables.set("numeroChamado", ch.numero);'
    ),
    ("post", "/api/v1/chamados/{id}/agendamentos"):
        'pm.collectionVariables.set("agendamentoId", pm.response.json().id);',
    ("post", "/api/v1/agendamentos/{id}/reagendamento"):
        'pm.collectionVariables.set("agendamentoId", pm.response.json().id);',
    ("post", "/api/v1/chamados/{id}/atendimento"):
        'pm.collectionVariables.set("atendimentoId", pm.response.json().id);',
    ("post", "/api/v1/atendimentos/{id}/orcamento"):
        'pm.collectionVariables.set("orcamentoId", pm.response.json().id);',
    ("post", "/api/v1/atendimentos/{id}/conclusao"): (
        "const r = pm.response.json();" + NL
        + 'pm.collectionVariables.set("garantiaId", r.garantia.id);' + NL
        + "if (r.faturaId) pm.collectionVariables.set(\"faturaId\", r.faturaId);"
    ),
    ("post", "/api/v1/chamados/{id}/avaliacao"):
        'pm.collectionVariables.set("avaliacaoId", pm.response.json().id);',
    ("post", "/api/v1/atendimentos/{id}/garantia/acionamento"): (
        "const g = pm.response.json();" + NL
        + 'pm.collectionVariables.set("chamadoDeGarantiaId", g.chamadoId);' + NL
        + 'pm.collectionVariables.set("chamadoId", g.chamadoId);'
    ),
}

DATA_FUTURA = (
    "// Horario livre no futuro, para o agendamento nao cair no passado." + NL
    + "const daqui = new Date(Date.now() + 1000 * 60 * 60 * 24 * 7);" + NL
    + 'pm.collectionVariables.set("dataAgendamento", daqui.toISOString());'
)

PRE_REQUEST = {
    ("post", "/api/v1/clientes"): (
        "const carimbo = Date.now().toString().slice(-9);" + NL
        + 'pm.collectionVariables.set("novoEmailCliente", "cliente." + carimbo + "@exemplo.com");' + NL
        + 'pm.collectionVariables.set("novoEmail", "cliente." + carimbo + "@exemplo.com");' + NL
        + 'pm.collectionVariables.set("novoCpf", ("1" + carimbo + "0").slice(0, 11));'
    ),
    ("post", "/api/v1/tecnicos"): (
        "const carimbo = Date.now().toString().slice(-9);" + NL
        + 'pm.collectionVariables.set("novoEmail", "tecnico." + carimbo + "@chamados.local");' + NL
        + 'pm.collectionVariables.set("novoCpf", ("2" + carimbo + "0").slice(0, 11));'
    ),
    ("put", "/api/v1/tecnicos/{id}"): (
        "const carimbo = Date.now().toString().slice(-9);" + NL
        + 'pm.collectionVariables.set("novoEmail", "tecnico." + carimbo + "@chamados.local");'
    ),
    ("put", "/api/v1/clientes/{id}"): (
        "const carimbo = Date.now().toString().slice(-9);" + NL
        + 'pm.collectionVariables.set("novoEmail", "cliente." + carimbo + "@exemplo.com");'
    ),
    ("post", "/api/v1/categorias-servico"):
        'pm.collectionVariables.set("novoNome", "Categoria " + Date.now().toString().slice(-6));',
    ("put", "/api/v1/categorias-servico/{id}"):
        'pm.collectionVariables.set("novoNome", "Categoria " + Date.now().toString().slice(-6));',
    ("post", "/api/v1/categorias-servico/{id}/tipos-servico"):
        'pm.collectionVariables.set("novoNome", "Servico " + Date.now().toString().slice(-6));',
    ("put", "/api/v1/tipos-servico/{id}"):
        'pm.collectionVariables.set("novoNome", "Servico " + Date.now().toString().slice(-6));',
    ("post", "/api/v1/chamados/{id}/agendamentos"): DATA_FUTURA,
    ("post", "/api/v1/agendamentos/{id}/reagendamento"): DATA_FUTURA,
}


def evento(listen, codigo):
    return {"listen": listen, "script": {"type": "text/javascript", "exec": codigo.split(NL)}}


def eventos(metodo, rota):
    lista = []

    pre = PRE_REQUEST.get((metodo, rota))
    if pre:
        lista.append(evento("prerequest", pre))

    codigo = TESTE_PADRAO
    captura = CAPTURA.get((metodo, rota))
    if captura:
        indentado = NL.join("    " + linha for linha in captura.split(NL))
        codigo += (NL + NL + "if (pm.response.code < 300 && pm.response.text()) {"
                   + NL + indentado + NL + "}")

    lista.append(evento("test", codigo))
    return lista


# ---------------------------------------------------------------- multipart

ARQUIVO = "@arquivo"

FORMULARIOS = {
    ("post", "/api/v1/chamados/{id}/anexos"): [("arquivo", ARQUIVO)],
    ("post", "/api/v1/tecnicos/{id}/documentos"): [("arquivo", ARQUIVO)],
}

VARIANTES_MULTIPART = {
    ("post", "/api/v1/chamados"): {
        "nome": "Abrir chamado com foto (multipart)",
        "campos": [
            ("imovelId", "{{imovelId}}"),
            ("categoriaServicoId", "{{categoriaId}}"),
            ("tipoServicoId", "{{tipoServicoId}}"),
            ("descricaoProblema", "Vazamento embaixo da pia da cozinha desde ontem."),
            ("indicacaoDeRisco", "true"),
            ("arquivos", ARQUIVO),
        ],
        "descricao": "RN0032: obrigatorio nas categorias marcadas com exigeFoto. "
                     "Selecione uma imagem no campo arquivos antes de enviar.",
    },
    ("post", "/api/v1/atendimentos/{id}/conclusao"): {
        "nome": "Concluir atendimento com fotos (multipart)",
        "campos": [
            ("relatoTecnico", "Registro trocado e teste de pressao aprovado."),
            ("arquivos", ARQUIVO),
        ],
        "descricao": "RF0057: fotos do servico finalizado. O JSON puro tambem e aceito.",
    },
}


def corpo_formdata(campos):
    itens = []
    for nome, valor in campos:
        if valor == ARQUIVO:
            itens.append({"key": nome, "type": "file", "src": [],
                          "description": "selecione um arquivo antes de enviar"})
        else:
            itens.append({"key": nome, "value": valor, "type": "text"})
    return {"mode": "formdata", "formdata": itens}


# ---------------------------------------------------------------- requisicao

POLITICA = {
    ("delete", "/api/v1/clientes/{id}/formas-pagamento/{formaId}"): "Cliente",
    ("delete", "/api/v1/clientes/{id}/imoveis/{imovelId}"): "Cliente",
    ("get", "/api/v1/analises/chamados"): "Administrador",
    ("get", "/api/v1/analises/chamados/exportacao"): "Administrador",
    ("get", "/api/v1/atendimentos/{id}/garantia"): "ClienteOuAdministrador",
    ("get", "/api/v1/atendimentos/{id}/orcamento"): "autenticado",
    ("get", "/api/v1/categorias-servico"): "autenticado",
    ("get", "/api/v1/chamados"): "Administrador",
    ("get", "/api/v1/chamados/meus"): "Cliente",
    ("get", "/api/v1/chamados/{id}"): "autenticado",
    ("get", "/api/v1/chamados/{id}/agendamentos"): "autenticado",
    ("get", "/api/v1/chamados/{id}/historico-status"): "autenticado",
    ("get", "/api/v1/clientes"): "Administrador",
    ("get", "/api/v1/clientes/{id}"): "ClienteOuAdministrador",
    ("get", "/api/v1/clientes/{id}/formas-pagamento"): "ClienteOuAdministrador",
    ("get", "/api/v1/clientes/{id}/imoveis"): "ClienteOuAdministrador",
    ("get", "/api/v1/faturas"): "Administrador",
    ("get", "/api/v1/faturas/minhas"): "Cliente",
    ("get", "/api/v1/faturas/{id}"): "ClienteOuAdministrador",
    ("get", "/api/v1/faturas/{id}/pagamentos"): "ClienteOuAdministrador",
    ("get", "/api/v1/tecnicos"): "Administrador",
    ("get", "/api/v1/tecnicos/{id}"): "Administrador",
    ("get", "/api/v1/tecnicos/{id}/avaliacoes"): "autenticado",
    ("get", "/api/v1/tipos-servico"): "autenticado",
    ("patch", "/api/v1/chamados/{id}/status"): "Administrador",
    ("patch", "/api/v1/chamados/{id}/urgencia"): "Administrador",
    ("patch", "/api/v1/clientes/{id}/ativacao"): "Administrador",
    ("patch", "/api/v1/clientes/{id}/formas-pagamento/{formaId}/principal"): "Cliente",
    ("patch", "/api/v1/clientes/{id}/inativacao"): "Administrador",
    ("patch", "/api/v1/tecnicos/{id}/ativacao"): "Administrador",
    ("patch", "/api/v1/tecnicos/{id}/inativacao"): "Administrador",
    ("post", "/api/v1/agendamentos/{id}/confirmacao"): "Cliente",
    ("post", "/api/v1/agendamentos/{id}/reagendamento"): "ClienteOuTecnico",
    ("post", "/api/v1/atendimentos/{id}/conclusao"): "Tecnico",
    ("post", "/api/v1/atendimentos/{id}/garantia/acionamento"): "Cliente",
    ("post", "/api/v1/atendimentos/{id}/orcamento"): "Tecnico",
    ("post", "/api/v1/auth/alterar-senha"): "autenticado",
    ("post", "/api/v1/auth/login"): "anonimo",
    ("post", "/api/v1/avaliacoes/{id}/resposta"): "Administrador",
    ("post", "/api/v1/categorias-servico"): "Administrador",
    ("post", "/api/v1/categorias-servico/{id}/tipos-servico"): "Administrador",
    ("post", "/api/v1/chamados"): "Cliente",
    ("post", "/api/v1/chamados/{id}/agendamentos"): "TecnicoOuAdministrador",
    ("post", "/api/v1/chamados/{id}/anexos"): "Cliente",
    ("post", "/api/v1/chamados/{id}/atendimento"): "Tecnico",
    ("post", "/api/v1/chamados/{id}/atribuicao"): "Administrador",
    ("post", "/api/v1/chamados/{id}/avaliacao"): "Cliente",
    ("post", "/api/v1/chamados/{id}/cancelamento"): "Cliente",
    ("post", "/api/v1/chamados/{id}/cancelamento-administrativo"): "Administrador",
    ("post", "/api/v1/chamados/{id}/reabertura"): "Cliente",
    ("post", "/api/v1/clientes"): "anonimo",
    ("post", "/api/v1/clientes/{id}/formas-pagamento"): "Cliente",
    ("post", "/api/v1/clientes/{id}/imoveis"): "Cliente",
    ("post", "/api/v1/faturas/{id}/pagamentos"): "Cliente",
    ("post", "/api/v1/orcamentos/{id}/decisao"): "Cliente",
    ("post", "/api/v1/tecnicos"): "Administrador",
    ("post", "/api/v1/tecnicos/{id}/documentos"): "Administrador",
    ("put", "/api/v1/categorias-servico/{id}"): "Administrador",
    ("put", "/api/v1/chamados/{id}/atribuicao"): "Administrador",
    ("put", "/api/v1/clientes/{id}"): "ClienteOuAdministrador",
    ("put", "/api/v1/clientes/{id}/formas-pagamento/{formaId}"): "Cliente",
    ("put", "/api/v1/clientes/{id}/imoveis/{imovelId}"): "Cliente",
    ("put", "/api/v1/tecnicos/{id}"): "Administrador",
    ("put", "/api/v1/tecnicos/{id}/areas-atendimento"): "Administrador",
    ("put", "/api/v1/tecnicos/{id}/especialidades"): "Administrador",
    ("put", "/api/v1/tipos-servico/{id}"): "Administrador",
}

TOKEN_DO_PAPEL = {
    "Administrador": "{{tokenAdministrador}}",
    "Cliente": "{{tokenCliente}}",
    "Tecnico": "{{tokenTecnico}}",
    "ClienteOuAdministrador": "{{tokenCliente}}",
    "TecnicoOuAdministrador": "{{tokenTecnico}}",
    "ClienteOuTecnico": "{{tokenCliente}}",
    "autenticado": "{{token}}",
}


def autenticacao_da_rota(metodo, rota):
    """Cada requisicao ja sai com o token do papel que o endpoint exige."""
    papel = POLITICA.get((metodo, rota), "autenticado")
    if papel == "anonimo":
        return {"type": "noauth"}
    valor = TOKEN_DO_PAPEL[papel]
    if valor == "{{token}}":
        return None
    return {"type": "bearer", "bearer": [{"key": "token", "value": valor, "type": "string"}]}

QUERY_HABILITADA = {"dataInicio", "dataFim"}

VALOR_DE_QUERY = {
    "dataInicio": "{{dataInicio}}",
    "dataFim": "{{dataFim}}",
    "page": "1",
    "pageSize": "20",
    "categoriaId": "{{categoriaId}}",
    "tecnicoId": "{{tecnicoId}}",
    "clienteId": "{{clienteId}}",
    "categoriaIds": "{{categoriaId}}",
    "tecnicoIds": "{{tecnicoId}}",
    "numeroChamado": "{{numeroChamado}}",
    "numero": "{{numeroChamado}}",
    "especialidadeId": "{{categoriaId}}",
    "agruparPor": "categoria",
}

SEM_AUTENTICACAO = {("post", "/api/v1/auth/login"), ("post", "/api/v1/clientes"), ("get", "/health")}


def descricao_da_query(parametro):
    resolvido = resolver(parametro.get("schema", {}))
    if "enum" in resolvido:
        return "valores: " + ", ".join(str(v) for v in resolvido["enum"] if v is not None)
    return ""


def montar_url(rota, operacao):
    mapa = variaveis_da_rota(rota)

    caminho = [
        "{{" + mapa.get(parte[1:-1], parte[1:-1]) + "}}" if parte.startswith("{") else parte
        for parte in rota.strip("/").split("/")
    ]

    query = []
    for parametro in operacao.get("parameters", []):
        if parametro.get("in") != "query":
            continue
        nome = parametro["name"]
        query.append({
            "key": nome,
            "value": VALOR_DE_QUERY.get(nome, ""),
            "description": descricao_da_query(parametro),
            "disabled": nome not in QUERY_HABILITADA,
        })

    ativos = [p for p in query if not p["disabled"]]
    raw = "{{baseUrl}}/" + "/".join(caminho)
    if ativos:
        raw += "?" + "&".join(f"{p['key']}={p['value']}" for p in ativos)

    url = {"raw": raw, "host": ["{{baseUrl}}"], "path": caminho}
    if query:
        url["query"] = query
    return url


def montar_requisicao(metodo, rota, operacao):
    requisicao = {
        "method": metodo.upper(),
        "header": [],
        "url": montar_url(rota, operacao),
        "description": NL.join(
            parte for parte in [operacao.get("summary"), operacao.get("description")] if parte
        ),
    }

    if (metodo, rota) in FORMULARIOS:
        requisicao["body"] = corpo_formdata(FORMULARIOS[(metodo, rota)])
    else:
        corpo = CORPO_MANUAL.get((metodo, rota))
        if corpo is None:
            conteudo = operacao.get("requestBody", {}).get("content", {}).get("application/json")
            if conteudo:
                corpo = exemplo(conteudo["schema"])
        if corpo is not None:
            requisicao["header"].append({"key": "Content-Type", "value": "application/json"})
            requisicao["body"] = {
                "mode": "raw",
                "raw": json.dumps(corpo, indent=2, ensure_ascii=False),
                "options": {"raw": {"language": "json"}},
            }

    auth = autenticacao_da_rota(metodo, rota)
    if auth is not None:
        requisicao["auth"] = auth

    return requisicao


# ------------------------------------------------------- pasta "Comecar aqui"

def requisicao_avulsa(metodo, caminho, nome, descricao="", corpo=None, query=None,
                      script="", sem_auth=False, papel=None):
    partes = caminho.strip("/").split("/")
    raw = "{{baseUrl}}/" + "/".join(partes)
    url = {"raw": raw, "host": ["{{baseUrl}}"], "path": partes}

    if query:
        url["query"] = [{"key": chave, "value": valor} for chave, valor in query]
        url["raw"] = raw + "?" + "&".join(f"{chave}={valor}" for chave, valor in query)

    requisicao = {"method": metodo, "header": [], "url": url, "description": descricao or nome}

    if corpo is not None:
        requisicao["header"].append({"key": "Content-Type", "value": "application/json"})
        requisicao["body"] = {
            "mode": "raw",
            "raw": json.dumps(corpo, indent=2, ensure_ascii=False),
            "options": {"raw": {"language": "json"}},
        }
    if sem_auth:
        requisicao["auth"] = {"type": "noauth"}
    elif papel:
        requisicao["auth"] = {
            "type": "bearer",
            "bearer": [{"key": "token", "value": TOKEN_DO_PAPEL[papel], "type": "string"}],
        }

    codigo = TESTE_PADRAO + ((NL + NL + script) if script else "")

    return {"name": nome, "request": requisicao, "event": [evento("test", codigo)], "response": []}


def login(papel, variavel_email):
    return requisicao_avulsa(
        "POST", "/api/v1/auth/login", "Entrar como " + papel,
        descricao="Autentica com o e-mail da variavel " + variavel_email + " e guarda o token.",
        corpo={"email": "{{" + variavel_email + "}}", "senha": "{{senha}}"},
        sem_auth=True,
        script=CAPTURA[("post", "/api/v1/auth/login")],
    )


PASTA_INICIAL = {
    "name": "Comecar aqui",
    "description": (
        "Rode esta pasta inteira uma vez, de cima para baixo. Ela monta o cenario do zero e "
        "guarda os identificadores nas variaveis da collection: a partir daqui as outras pastas "
        "funcionam sem voce copiar GUID nenhum." + NL + NL
        + "Pre-requisito: o administrador precisa existir. Ele nasce por linha de comando, que e "
        + "o unico caminho previsto:" + NL + NL
        + "    dotnet run --project src/ChamadosManutencao.Api -- criar-admin" + NL + NL
        + "Use no ambiente o mesmo e-mail e a mesma senha que voce passou nas variaveis "
        + "ADMIN_INICIAL_*." + NL + NL
        + "Para trocar de papel depois, rode so o login correspondente."
    ),
    "item": [
        login("administrador", "emailAdministrador"),
        requisicao_avulsa(
            "POST", "/api/v1/categorias-servico", "Criar a categoria de servico",
            descricao="Guarda categoriaId. Sem exigencia de foto, para o resto rodar so com JSON.",
            corpo={"nome": "{{novoNome}}", "descricao": "Categoria criada pela collection.",
                   "exigeFoto": False, "categoriaDeRisco": False},
            papel="Administrador",
            script='pm.collectionVariables.set("categoriaId", pm.response.json().id);',
        ),
        requisicao_avulsa(
            "POST", "/api/v1/categorias-servico/{{categoriaId}}/tipos-servico",
            "Criar o tipo de servico",
            descricao="Guarda tipoServicoId.",
            corpo={"nome": "{{novoNome}}", "descricao": "Tipo de servico criado pela collection."},
            papel="Administrador",
            script='pm.collectionVariables.set("tipoServicoId", pm.response.json().id);',
        ),
        requisicao_avulsa(
            "POST", "/api/v1/tecnicos", "Cadastrar o tecnico",
            descricao="Especialidade na categoria criada e area de atendimento no Centro. "
                      "Guarda tecnicoId e o e-mail dele.",
            corpo={"nomeCompleto": "Tecnico da Collection", "cpf": "{{novoCpf}}",
                   "email": "{{novoEmail}}", "telefone": "11930000011", "senha": "Senha@123",
                   "especialidadeIds": ["{{categoriaId}}"],
                   "areasAtendimento": [{"bairro": "Centro", "cepInicial": "01000000",
                                         "cepFinal": "01999999", "taxaDeslocamento": 25.00}]},
            papel="Administrador",
            script=("const t = pm.response.json();" + NL
                    + 'pm.collectionVariables.set("tecnicoId", t.id);' + NL
                    + 'if (pm.environment.has("emailTecnico")) pm.environment.set("emailTecnico", t.email);' + NL
                    + 'else pm.collectionVariables.set("emailTecnico", t.email);' + NL
                    + 'console.log("tecnico " + t.codigoTecnico + " / " + t.email);'),
        ),
        requisicao_avulsa(
            "POST", "/api/v1/clientes", "Cadastrar o cliente",
            descricao="Cadastro anonimo, com um imovel no Centro. Guarda o e-mail e o imovelId.",
            corpo={"nomeCompleto": "Cliente da Collection", "cpf": "{{novoCpf}}",
                   "email": "{{novoEmailCliente}}", "telefone": "11940000001", "senha": "Senha@123",
                   "imoveis": [{"apelido": "Apartamento do Centro", "tipoImovel": "Apartamento",
                                "logradouro": "Rua Sete de Abril", "numero": "120",
                                "complemento": "Apto 71", "bairro": "Centro", "cep": "01043000",
                                "cidade": "Sao Paulo", "estado": "SP"}]},
            sem_auth=True,
            script=("const c = pm.response.json();" + NL
                    + 'pm.collectionVariables.set("imovelId", c.imoveis[0].id);' + NL
                    + 'if (pm.environment.has("emailCliente")) pm.environment.set("emailCliente", c.email);' + NL
                    + 'else pm.collectionVariables.set("emailCliente", c.email);' + NL
                    + 'console.log("cliente " + c.codigoCliente + " / " + c.email);'),
        ),
        login("cliente", "emailCliente"),
        login("tecnico", "emailTecnico"),
        login("administrador de novo", "emailAdministrador"),
    ],
}

# ----------------------------------------------------- pasta "Jornada completa"

def passo(numero, metodo, caminho, titulo, papel, corpo=None, script="", descricao=""):
    return requisicao_avulsa(
        metodo, caminho, str(numero) + ". " + titulo,
        descricao=descricao or titulo, corpo=corpo, script=script, papel=papel,
    )


PASTA_JORNADA = {
    "name": "Jornada completa",
    "description": (
        "O caminho feliz inteiro, na ordem, cada passo com o papel certo: catalogo e equipe, "
        "abertura, triagem, agendamento, execucao, conclusao, pagamento, avaliacao e garantia."
        + NL + NL
        + "Diferente das pastas por caso de uso, esta roda de cima para baixo sem intervencao: "
        + "use o Runner do Postman para ver o sistema funcionando de ponta a ponta." + NL + NL
        + "Ela monta o proprio cenario: cria uma categoria sem exigencia de foto, um tipo de "
        + "servico e um tecnico para o Centro, e usa o cliente do seed."
    ),
    "item": [
        login("administrador", "emailAdministrador"),
        passo(1, "POST", "/api/v1/categorias-servico", "Administrador cria a categoria",
              "Administrador",
              corpo={"nome": "{{novoNome}}", "descricao": "Categoria da jornada.",
                     "exigeFoto": False, "categoriaDeRisco": False},
              descricao="Sem exigencia de foto, para a jornada rodar so com JSON.",
              script='pm.collectionVariables.set("categoriaId", pm.response.json().id);'),
        passo(2, "POST", "/api/v1/categorias-servico/{{categoriaId}}/tipos-servico",
              "Administrador cria o tipo de servico", "Administrador",
              corpo={"nome": "{{novoNome}}", "descricao": "Tipo de servico da jornada."},
              script='pm.collectionVariables.set("tipoServicoId", pm.response.json().id);'),
        passo(3, "POST", "/api/v1/tecnicos", "Administrador cadastra o tecnico", "Administrador",
              corpo={"nomeCompleto": "Tecnico da Jornada", "cpf": "{{novoCpf}}",
                     "email": "{{novoEmail}}", "telefone": "11930000011", "senha": "Senha@123",
                     "especialidadeIds": ["{{categoriaId}}"],
                     "areasAtendimento": [{"bairro": "Centro", "cepInicial": "01000000",
                                           "cepFinal": "01999999", "taxaDeslocamento": 25.00}]},
              descricao="Especialidade na categoria criada e area de atendimento no Centro.",
              script=("const t = pm.response.json();" + NL
                      + 'pm.collectionVariables.set("tecnicoId", t.id);' + NL
                      + 'if (pm.environment.has("emailTecnico")) pm.environment.set("emailTecnico", t.email);' + NL
                      + 'else pm.collectionVariables.set("emailTecnico", t.email);' + NL
                      + 'console.log("tecnico " + t.codigoTecnico + " / " + t.email);')),
        requisicao_avulsa(
            "POST", "/api/v1/clientes", "4. Cliente cria a propria conta",
            descricao="Cadastro anonimo, com um imovel no Centro.",
            corpo={"nomeCompleto": "Cliente da Jornada", "cpf": "{{novoCpf}}",
                   "email": "{{novoEmailCliente}}", "telefone": "11940000001",
                   "senha": "Senha@123",
                   "imoveis": [{"apelido": "Apartamento do Centro", "tipoImovel": "Apartamento",
                                "logradouro": "Rua Sete de Abril", "numero": "120",
                                "complemento": "Apto 71", "bairro": "Centro", "cep": "01043000",
                                "cidade": "Sao Paulo", "estado": "SP"}]},
            sem_auth=True,
            script=("const c = pm.response.json();" + NL
                    + 'pm.collectionVariables.set("imovelId", c.imoveis[0].id);' + NL
                    + 'if (pm.environment.has("emailCliente")) pm.environment.set("emailCliente", c.email);' + NL
                    + 'else pm.collectionVariables.set("emailCliente", c.email);')),
        login("cliente", "emailCliente"),
        passo(5, "POST", "/api/v1/chamados", "Cliente abre o chamado", "Cliente",
              corpo={"imovelId": "{{imovelId}}", "categoriaServicoId": "{{categoriaId}}",
                     "tipoServicoId": "{{tipoServicoId}}",
                     "descricaoProblema": "Aparelho parou depois da queda de energia.",
                     "indicacaoDeRisco": False},
              script=("const ch = pm.response.json();" + NL
                      + 'pm.collectionVariables.set("chamadoId", ch.id);' + NL
                      + 'pm.collectionVariables.set("numeroChamado", ch.numero);' + NL
                      + 'console.log("chamado " + ch.numero + " " + ch.status);')),
        login("administrador", "emailAdministrador"),
        passo(6, "POST", "/api/v1/chamados/{{chamadoId}}/atribuicao",
              "Administrador atribui o tecnico", "Administrador",
              corpo={"tecnicoId": "{{tecnicoId}}"}),
        login("tecnico", "emailTecnico"),
        passo(7, "POST", "/api/v1/chamados/{{chamadoId}}/agendamentos",
              "Tecnico propoe o horario", "Tecnico",
              corpo={"dataHoraProposta": "{{dataAgendamento}}", "duracaoEmMinutos": 120},
              script='pm.collectionVariables.set("agendamentoId", pm.response.json().id);'),
        login("cliente", "emailCliente"),
        passo(8, "POST", "/api/v1/agendamentos/{{agendamentoId}}/confirmacao",
              "Cliente confirma o horario", "Cliente"),
        login("tecnico", "emailTecnico"),
        passo(9, "POST", "/api/v1/chamados/{{chamadoId}}/atendimento",
              "Tecnico inicia o atendimento no local", "Tecnico",
              script='pm.collectionVariables.set("atendimentoId", pm.response.json().id);'),
        passo(10, "POST", "/api/v1/atendimentos/{{atendimentoId}}/orcamento",
              "Tecnico registra o orcamento", "Tecnico",
              corpo={"itens": [
                  {"descricao": "Placa eletronica", "tipo": "Peca", "quantidade": 1,
                   "valorUnitario": 180.00},
                  {"descricao": "Mao de obra", "tipo": "MaoDeObra", "quantidade": 2,
                   "valorUnitario": 60.00}]},
              script=("const o = pm.response.json();" + NL
                      + 'pm.collectionVariables.set("orcamentoId", o.id);' + NL
                      + 'console.log("orcamento " + o.valorTotal + " ate " + o.prazoAprovacao);')),
        login("cliente", "emailCliente"),
        passo(11, "POST", "/api/v1/orcamentos/{{orcamentoId}}/decisao",
              "Cliente aprova o orcamento", "Cliente", corpo={"decisao": "Aprovado"}),
        login("tecnico", "emailTecnico"),
        passo(12, "POST", "/api/v1/atendimentos/{{atendimentoId}}/conclusao",
              "Tecnico conclui: nascem a garantia e a fatura", "Tecnico",
              corpo={"relatoTecnico": "Placa substituida e aparelho testado com o cliente."},
              script=("const r = pm.response.json();" + NL
                      + 'pm.collectionVariables.set("garantiaId", r.garantia.id);' + NL
                      + 'pm.collectionVariables.set("faturaId", r.faturaId);' + NL
                      + 'console.log("fatura " + r.numeroDaFatura + " de " + r.valorDaFatura);')),
        login("cliente", "emailCliente"),
        passo(13, "POST", "/api/v1/clientes/{{clienteId}}/formas-pagamento",
              "Cliente cadastra a forma de pagamento", "Cliente",
              corpo={"tipo": "Pix", "apelido": "PIX da jornada", "principal": True,
                     "chavePix": "{{emailCliente}}", "tipoChave": "Email"},
              descricao="PIX, para a jornada nao depender de tokenizacao de cartao.",
              script='pm.collectionVariables.set("formaPagamentoId", pm.response.json().id);'),
        passo(14, "POST", "/api/v1/faturas/{{faturaId}}/pagamentos", "Cliente paga a fatura",
              "Cliente", corpo={"formaPagamentoId": "{{formaPagamentoId}}"},
              script=("const p = pm.response.json();" + NL
                      + 'console.log("pagamento " + p.status + " " + p.identificadorTransacao);')),
        passo(15, "POST", "/api/v1/chamados/{{chamadoId}}/avaliacao", "Cliente avalia", "Cliente",
              corpo={"nota": 5, "comentario": "Chegou no horario e resolveu."},
              script='pm.collectionVariables.set("avaliacaoId", pm.response.json().id);'),
        login("administrador", "emailAdministrador"),
        passo(16, "POST", "/api/v1/avaliacoes/{{avaliacaoId}}/resposta",
              "Administrador responde a avaliacao", "Administrador",
              corpo={"texto": "Obrigado pelo retorno."}),
        login("cliente", "emailCliente"),
        passo(17, "POST", "/api/v1/atendimentos/{{atendimentoId}}/garantia/acionamento",
              "Cliente aciona a garantia", "Cliente",
              corpo={"descricaoProblema": "O mesmo defeito voltou depois de duas semanas."},
              descricao="Gera um chamado novo, vinculado ao original e sem fatura.",
              script=("const g = pm.response.json();" + NL
                      + 'pm.collectionVariables.set("chamadoDeGarantiaId", g.chamadoId);' + NL
                      + 'console.log("chamado de garantia " + g.numeroDoChamado);')),
        login("administrador", "emailAdministrador"),
        passo(18, "GET", "/api/v1/chamados/{{chamadoId}}/historico-status",
              "A jornada inteira no historico", "Administrador",
              script=("const h = pm.response.json();" + NL
                      + 'console.log(h.map(l => l.statusNovo).join(" -> "));')),
    ],
}

# ---------------------------------------------------------------- montagem

pastas = collections.OrderedDict()

for rota, operacoes in doc["paths"].items():
    for metodo, operacao in operacoes.items():
        tag = (operacao.get("tags") or ["Outros"])[0]
        pastas.setdefault(tag, [])

        nome = (operacao.get("summary") or "").rstrip(".") or (metodo.upper() + " " + rota)

        pastas[tag].append({
            "name": nome,
            "request": montar_requisicao(metodo, rota, operacao),
            "event": eventos(metodo, rota),
            "response": [],
        })

        variante = VARIANTES_MULTIPART.get((metodo, rota))
        if variante:
            requisicao = montar_requisicao(metodo, rota, operacao)
            requisicao["header"] = []
            requisicao["body"] = corpo_formdata(variante["campos"])
            requisicao["description"] = variante["descricao"]
            pastas[tag].append({
                "name": variante["nome"],
                "request": requisicao,
                "event": eventos(metodo, rota),
                "response": [],
            })

ORDEM_DAS_PASTAS = [
    "Autenticacao", "UC01 Clientes", "UC02 Tecnicos", "UC03 Catalogo", "UC04 Chamados",
    "UC05 Triagem", "UC06 Agendamento", "UC07 Atendimento", "UC08 Avaliacao", "UC09 Analise",
    "UC10 Formas de pagamento", "UC11 Faturas", "UC12 Garantia", "Infraestrutura",
]

DESCRICAO_DA_PASTA = {
    "Autenticacao": "Login e troca de senha. Atencao: a troca de senha muda a senha de verdade "
                    "e invalida a anterior para aquele usuario.",
    "UC01 Clientes": "Cadastro, alteracao, ativacao, inativacao, consulta e imoveis. O cadastro e "
                     "anonimo: e o formulario de criar conta.",
    "UC02 Tecnicos": "Equipe, especialidades, areas de atendimento (com a taxa de deslocamento que "
                     "entra na fatura) e documentos de certificacao.",
    "UC03 Catalogo": "Categorias e tipos de servico. exigeFoto e categoriaDeRisco mudam o "
                     "comportamento da abertura de chamado.",
    "UC04 Chamados": "Abertura, anexos, consulta do cliente, cancelamento, reabertura e historico.",
    "UC05 Triagem": "Visao do administrador: fila, urgencia, atribuicao e reatribuicao de tecnico, "
                    "mudanca de status e cancelamento administrativo.",
    "UC06 Agendamento": "Proposta de horario pelo tecnico ou administrador, confirmacao pelo "
                        "cliente e reagendamento.",
    "UC07 Atendimento": "Inicio no local, orcamento, decisao do cliente e conclusao. A conclusao "
                        "gera a garantia e a fatura.",
    "UC08 Avaliacao": "Nota e comentario do cliente, resposta publica do administrador.",
    "UC09 Analise": "Serie mensal por categoria ou tecnico e exportacao em CSV. O intervalo vem de "
                    "dataInicio e dataFim.",
    "UC10 Formas de pagamento": "Cartao, PIX e boleto. O numero do cartao nunca e persistido: so "
                                "token, bandeira, quatro ultimos digitos e validade.",
    "UC11 Faturas": "Consulta de faturas e registro de pagamento.",
    "UC12 Garantia": "Consulta e acionamento da garantia de 90 dias.",
    "Infraestrutura": "Health check.",
}

itens = [PASTA_INICIAL]
for tag in ORDEM_DAS_PASTAS:
    if tag in pastas:
        itens.append({"name": tag, "description": DESCRICAO_DA_PASTA.get(tag, ""),
                      "item": pastas.pop(tag)})
for tag, requisicoes in pastas.items():
    itens.append({"name": tag, "item": requisicoes})

itens.append(PASTA_JORNADA)

VARIAVEIS = [
    ("baseUrl", "http://localhost:5080"),
    ("token", ""), ("tokenAdministrador", ""), ("tokenCliente", ""), ("tokenTecnico", ""),
    ("clienteId", ""), ("imovelId", ""), ("tecnicoId", ""), ("categoriaId", ""),
    ("tipoServicoId", ""), ("chamadoId", ""), ("numeroChamado", ""), ("agendamentoId", ""),
    ("atendimentoId", ""), ("orcamentoId", ""), ("faturaId", ""), ("formaPagamentoId", ""),
    ("avaliacaoId", ""), ("garantiaId", ""), ("chamadoDeGarantiaId", ""),
    ("novoEmail", ""), ("novoEmailCliente", ""), ("novoCpf", ""), ("novoNome", ""),
    ("dataAgendamento", ""),
    ("clienteNovoId", ""), ("administradorId", ""),
]

DESCRICAO = (
    "Interface de operacao do sistema de abertura de chamados de manutencao." + NL + NL
    + "**Como comecar**" + NL + NL
    + "1. Crie o administrador e suba a API:" + NL
    + "   - `dotnet run --project src/ChamadosManutencao.Api -- criar-admin`" + NL
    + "   - `dotnet run --project src/ChamadosManutencao.Api`" + NL
    + "2. Selecione o ambiente **Chamados - local**." + NL
    + "3. Rode a pasta **Comecar aqui** inteira. Ela monta o cenario do zero." + NL
    + "4. A partir dai, use as pastas UC01 a UC12 na ordem que quiser." + NL + NL
    + "**Trocar de papel**: rode `Comecar aqui > Entrar como tecnico` (ou cliente, ou "
    + "administrador). O token fica na variavel token e vale para toda a collection." + NL + NL
    + "O administrador vem do comando criar-admin; o tecnico e o cliente sao criados pela "
    + "pasta Comecar aqui, com senha `Senha@123`." + NL + NL
    + "**Encadeamento**: toda requisicao que cria alguma coisa guarda o identificador em variavel "
    + "de collection (chamadoId, atendimentoId, faturaId...). Seguindo a ordem das pastas, uma "
    + "alimenta a outra." + NL + NL
    + "**Filtros**: os parametros de query vem desabilitados. Marque a caixa dos que quiser usar."
    + NL + NL
    + "**Upload**: nas requisicoes multipart, selecione um arquivo no campo antes de enviar."
    + NL + NL
    + "Guia de operacao completo no README do repositorio."
)

PRE_JORNADA = (
    "const carimbo = Date.now().toString().slice(-9);" + NL
    + 'pm.collectionVariables.set("novoNome", "Jornada " + carimbo.slice(-6));' + NL
    + 'pm.collectionVariables.set("novoEmail", "tecnico." + carimbo + "@chamados.local");' + NL
    + 'pm.collectionVariables.set("novoEmailCliente", "cliente." + carimbo + "@exemplo.com");' + NL
    + 'pm.collectionVariables.set("novoCpf", ("3" + carimbo + "0").slice(0, 11));' + NL
    + "const daqui = new Date(Date.now() + 1000 * 60 * 60 * 24 * 7);" + NL
    + 'pm.collectionVariables.set("dataAgendamento", daqui.toISOString());'
)

PASTA_JORNADA["event"] = [evento("prerequest", PRE_JORNADA)]
PASTA_INICIAL["event"] = [evento("prerequest", PRE_JORNADA)]

collection = {
    "info": {
        "name": "Chamados de Manutencao",
        "description": DESCRICAO,
        "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
    },
    "auth": {"type": "bearer", "bearer": [{"key": "token", "value": "{{token}}", "type": "string"}]},
    "event": [evento(
        "prerequest",
        "// Sem token? Rode 'Comecar aqui > Entrar como administrador' antes." + NL
        + "if (!pm.collectionVariables.get('token') && !pm.request.url.toString().includes('/auth/login')) {" + NL
        + "    console.warn('Nenhum token na collection. Rode a pasta Comecar aqui.');" + NL
        + "}",
    )],
    "variable": [{"key": chave, "value": valor, "type": "string"} for chave, valor in VARIAVEIS],
    "item": itens,
}

ambiente = {
    "name": "Chamados - local",
    "values": [
        {"key": "baseUrl", "value": "http://localhost:5080", "enabled": True, "type": "default"},
        {"key": "emailAdministrador", "value": "admin@chamados.local", "enabled": True, "type": "default"},
        {"key": "emailTecnico", "value": "", "enabled": True, "type": "default"},
        {"key": "emailCliente", "value": "", "enabled": True, "type": "default"},
        {"key": "senha", "value": "Senha@123", "enabled": True, "type": "default"},
        {"key": "dataInicio", "value": "2025-09-01T00:00:00Z", "enabled": True, "type": "default"},
        {"key": "dataFim", "value": "2026-09-01T00:00:00Z", "enabled": True, "type": "default"},
    ],
    "_postman_variable_scope": "environment",
}

json.dump(collection, open(SAIDA_COLLECTION, "w", encoding="utf-8"), indent=2, ensure_ascii=False)
json.dump(ambiente, open(SAIDA_AMBIENTE, "w", encoding="utf-8"), indent=2, ensure_ascii=False)

total = sum(len(pasta["item"]) for pasta in itens)
print("pastas: " + str(len(itens)) + "  requisicoes: " + str(total))
for pasta in itens:
    print("  " + pasta["name"].ljust(28) + str(len(pasta["item"])))
