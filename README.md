# Sistema de Abertura de Chamados de Manutenção

Backend do sistema de abertura de chamados de manutenção de uma empresa de serviços
residenciais. API HTTP em .NET 10, PostgreSQL 17 em container, sem interface gráfica.

O projeto é *spec-driven*: cada requisito do DRS tem código que o implementa e teste que o
prova. O mapa completo está em [`docs/RASTREABILIDADE.md`](docs/RASTREABILIDADE.md).

| Documento | Conteúdo |
|---|---|
| [`postman/README.md`](postman/README.md) | Collection do Postman: a interface para operar o sistema enquanto não há tela |
| [`docs/TESTES_MANUAIS.md`](docs/TESTES_MANUAIS.md) | Roteiro manual: 93 cenários com o comando, o resultado esperado e o observado |
| [`docs/ARQUITETURA.md`](docs/ARQUITETURA.md) | Desenho da solução, agregados, transversais e as dúvidas levantadas na análise |
| [`docs/DECISOES.md`](docs/DECISOES.md) | Registro das decisões tomadas, com a justificativa de cada uma |
| [`docs/RASTREABILIDADE.md`](docs/RASTREABILIDADE.md) | Os 78 requisitos com caso de uso, endpoint, classe e teste |
| [`docs/DRS_Chamados_Manutencao.docx`](docs/DRS_Chamados_Manutencao.docx) | Documento de requisitos |
| [`docs/Diagramas_Chamados_Manutencao.pdf`](docs/Diagramas_Chamados_Manutencao.pdf) | Diagramas UML: casos de uso e classes de domínio |
| [`docs/Matriz_Rastreabilidade_Chamados_Manutencao.xlsx`](docs/Matriz_Rastreabilidade_Chamados_Manutencao.xlsx) | Os 78 requisitos cruzados com os 12 casos de uso |

---

# Parte 1 — Subir o projeto

## Pré-requisitos

| Ferramenta | Versão | Para quê |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10 | compilar e rodar |
| [Docker](https://docs.docker.com/get-docker/) | recente | banco e testes de integração |
| `curl` | acompanha o Git Bash e o Windows 10+ | chamar a API pelo terminal |
| `dotnet-ef` | 10 | gerar e aplicar migrations |

```bash
dotnet tool install --global dotnet-ef
```

Confira antes de começar:

```bash
dotnet --version     # 10.x
docker version       # o Docker precisa estar em execução
```

## Conflito de porta 5432 (leia antes de subir o banco)

O `docker-compose.yml` publica o PostgreSQL na porta indicada por `POSTGRES_PORT`, que por
padrão é `5432`. **Se você tem um PostgreSQL instalado no Windows, ele já ocupa essa porta** e o
container sobe sem que a API consiga chegar nele — o sintoma é `role "chamados" não existe`,
porque a conexão cai no servidor errado.

Descubra se há um serviço concorrente:

```powershell
Get-Service | Where-Object { $_.DisplayName -like '*PostgreSQL*' }
netstat -ano | Select-String ":5432" | Select-String "LISTENING"
```

**Opção A — parar o PostgreSQL do Windows** (exige terminal como administrador):

```powershell
Stop-Service postgresql-x64-18 -Force
Set-Service  postgresql-x64-18 -StartupType Manual   # não volta sozinho no próximo boot
```

**Opção B — publicar o container em outra porta.** No `.env`, troque `POSTGRES_PORT=55432` e
aponte a aplicação para ela criando `src/ChamadosManutencao.Api/appsettings.Local.json` —
arquivo não versionado, lido depois dos `appsettings` e antes das variáveis de ambiente:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=55432;Database=chamados;Username=chamados;Password=chamados_dev_senha"
  }
}
```

O `dotnet ef` não lê esse arquivo: passe a cadeia de conexão pelo ambiente ao aplicar as
migrations.

Os **testes de integração não são afetados** por esse conflito: o Testcontainers publica o
PostgreSQL numa porta aleatória livre.

## Subir

```bash
cp .env.example .env
docker compose up -d postgres
docker inspect -f '{{.State.Health.Status}}' chamados_postgres   # healthy
dotnet ef database update -p src/ChamadosManutencao.Infrastructure -s src/ChamadosManutencao.Api
dotnet run --project src/ChamadosManutencao.Api
```

Em `Development` as migrations pendentes são aplicadas no startup, então o `dotnet ef` é
opcional no dia a dia — fora de `Development` ele é obrigatório.

| Endereço | O que é |
|---|---|
| `http://localhost:5080/swagger` | Swagger UI: os endpoints agrupados por caso de uso, com cliente HTTP embutido |
| `http://localhost:5080/openapi/v1.json` | o documento OpenAPI cru (66 operações) |

O job de segundo plano sobe junto e **executa uma vez no startup**, repetindo a cada 15
minutos. Para desligá-lo: `export Jobs__Habilitados=false`.

## Criar o administrador

O sistema nasce vazio. Os 78 requisitos pressupõem o ator administrador em onze deles, mas
nenhum descreve como cadastrá-lo — não há endpoint, e não deve haver. Ele entra por linha de
comando:

```bash
export ADMIN_INICIAL_EMAIL=admin@suaempresa.com.br
export ADMIN_INICIAL_SENHA='UmaSenha@Forte1'
export ADMIN_INICIAL_CPF=12345678901
export ADMIN_INICIAL_NOME='Regina Souza'
export ADMIN_INICIAL_MATRICULA=ADM-000001

dotnet run --project src/ChamadosManutencao.Api -- criar-admin
```

Idempotente: se já existe administrador, não faz nada. Daí em diante tudo passa pela API — o
administrador cadastra o catálogo e os técnicos, e os clientes criam a própria conta.

Se for usar a collection do Postman, repita esse e-mail e essa senha nas variáveis
`emailAdministrador` e `senhaAdministrador` do ambiente — é por elas que a collection entra
como administrador.

## Testes

**Unitários** — sem Docker nem banco, uma classe por regra de negócio:

```bash
dotnet test tests/ChamadosManutencao.UnitTests          # 249 testes, < 1s
```

**Integração** — PostgreSQL 17 descartável via Testcontainers, HTTP real contra a API em
memória. O Docker precisa estar rodando:

```bash
dotnet test tests/ChamadosManutencao.IntegrationTests   # 154 testes, ~3min20
```

Filtrando por caso de uso:

```bash
dotnet test tests/ChamadosManutencao.IntegrationTests --filter "Escopo=UC07"
dotnet test tests/ChamadosManutencao.IntegrationTests --filter "Escopo=Jornada"
```

Valores de `Escopo`: `Autenticacao`, `UC01` a `UC12`, `Autorizacao`, `Jornada`, `RNF0011`.

**Tudo de uma vez**, e cobertura do domínio:

```bash
dotnet test
dotnet test tests/ChamadosManutencao.UnitTests --collect:"XPlat Code Coverage"
```

**Manual** — o roteiro com os `curl` de cada cenário, o esperado e o observado está em
[`docs/TESTES_MANUAIS.md`](docs/TESTES_MANUAIS.md).

## Docker

| Comando | Efeito |
|---|---|
| `docker compose up -d postgres` | sobe só o banco, com volume `chamados_pgdata` e healthcheck |
| `docker compose --profile tools up -d adminer` | Adminer em `http://localhost:8080` para inspeção manual |
| `docker compose --profile full up -d --build` | sobe banco e API em container, com volume para os anexos |
| `docker compose down` | derruba os serviços preservando o volume |
| `docker compose down -v` | derruba tudo e **apaga** o volume do banco |

## Quando algo dá errado

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| `role "chamados" não existe` | outro PostgreSQL na porta 5432 | seção do conflito de porta |
| `Jwt:ChaveSecreta deve ser configurada com ao menos 32 caracteres` | `.env` não copiado | `cp .env.example .env` e reabra o terminal |
| 401 em tudo, inclusive no login do administrador | o `criar-admin` não rodou nesta base | rode-o com as variáveis `ADMIN_INICIAL_*` |
| Testes de integração param criando o container | Docker parado ou imagem ausente | `docker version`, depois `docker pull postgres:17-alpine` |
| API sobe mas responde 500 em tudo | migrations não aplicadas fora de Development | rode o `dotnet ef database update` |

Toda escrita fica gravada em `log_transacao` e toda notificação em `notificacao_enviada` — as
duas tabelas são a primeira parada de qualquer investigação:

```bash
docker exec chamados_postgres psql -U chamados -d chamados \
  -c "SELECT data_hora, entidade, operacao, usuario_id FROM log_transacao ORDER BY data_hora DESC LIMIT 10;"
```

---

# Parte 2 — Como usar

O sistema não tem tela. A interface é a API HTTP em `/api/v1`, e há duas formas confortáveis de
usá-la:

- **[Collection do Postman](postman/README.md)** — os 68 endpoints por caso de uso, já com corpo
  preenchido, token do papel certo em cada requisição e os identificadores passando de uma
  chamada para a outra. É o painel de controle do sistema; comece por aqui.
- **Swagger UI**, em `http://localhost:5080/swagger` — a documentação OpenAPI navegável, com um
  cliente HTTP embutido.

Os exemplos abaixo usam `curl` porque cabem no texto. No Postman e no Swagger é o mesmo, sem
digitar.

## Os três papéis

| Papel | Quem é | O que faz |
|---|---|---|
| **Cliente** | quem mora no imóvel | abre chamado, confirma agendamento, aprova orçamento, paga a fatura, avalia, aciona garantia |
| **Técnico** | quem executa o serviço | propõe horário, inicia o atendimento, registra orçamento, conclui o serviço |
| **Administrador** | a empresa | mantém catálogo e técnicos, tria chamados, atribui técnico, responde avaliações, analisa o histórico |

Autenticação por JWT Bearer em `POST /api/v1/auth/login`; o token vale 60 minutos e depois é
preciso entrar de novo. Todo endpoint, exceto login e cadastro de
cliente, exige token. Além do papel, os endpoints de dono checam a propriedade: o cliente só
enxerga os próprios chamados, o técnico só executa o atendimento que lhe foi atribuído.

```bash
BASE=http://localhost:5080/api/v1

curl -s -X POST $BASE/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"admin@chamados.local","senha":"Senha@123"}'
```

```json
{ "accessToken": "eyJhbGciOi...", "expiraEm": "2026-09-09T01:30:00+00:00", "papel": "Administrador" }
```

## Colocando a operação de pé

Ordem importa: um chamado só existe se houver categoria, tipo de serviço, técnico habilitado e
cliente com imóvel. Faça uma vez, na implantação. O administrador já deve existir — ele vem do
`criar-admin`, descrito na Parte 1.

### 1. Montar o catálogo de serviços

```bash
curl -s -X POST $BASE/categorias-servico -H "$ADMIN" -H 'Content-Type: application/json' -d '{
  "nome": "Hidraulica",
  "descricao": "Reparos em tubulacoes, registros e loucas sanitarias.",
  "exigeFoto": true,
  "categoriaDeRisco": false
}'
```

Duas marcas mudam o comportamento do sistema e valem uma decisão consciente:

- **`exigeFoto`** — a abertura de chamado nessa categoria é recusada sem ao menos uma imagem.
  Ligue para eletrodomésticos e ar-condicionado, onde a foto do equipamento evita visita à toa.
- **`categoriaDeRisco`** — se o cliente marcar "indicação de risco" na abertura, a urgência vai
  automaticamente para **Alta**, independentemente do que ele escolheu. Ligue para elétrica e
  hidráulica, onde vazamento e curto-circuito não podem esperar na fila.

Depois, os tipos de serviço:

```bash
curl -s -X POST $BASE/categorias-servico/$CATEGORIA/tipos-servico \
  -H "$ADMIN" -H 'Content-Type: application/json' \
  -d '{"nome":"Reparo de vazamento","descricao":"Localizacao e reparo de vazamento."}'
```

### 2. Cadastrar a equipe

O técnico precisa de **especialidades** (as categorias que ele atende) e **áreas de atendimento**
(faixas de CEP, cada uma com sua taxa de deslocamento). Sem os dois ele não recebe chamado
nenhum.

```bash
curl -s -X POST $BASE/tecnicos -H "$ADMIN" -H 'Content-Type: application/json' -d '{
  "nomeCompleto": "Carlos Ribeiro",
  "cpf": "22233344455",
  "email": "carlos.ribeiro@suaempresa.com.br",
  "telefone": "11930000011",
  "senha": "Senha@123",
  "especialidadeIds": ["<id da categoria Hidraulica>"],
  "areasAtendimento": [
    { "bairro": "Centro",    "cepInicial": "01000000", "cepFinal": "01999999", "taxaDeslocamento": 25.00 },
    { "bairro": "Pinheiros", "cepInicial": "05400000", "cepFinal": "05499999", "taxaDeslocamento": 35.00 }
  ]
}'
```

A **taxa de deslocamento é por área**, não por técnico: é ela que entra na fatura, escolhida pela
faixa de CEP onde o imóvel está. Cobrar mais caro por bairro distante é só cadastrar a área com
taxa maior.

O técnico recebe um código sequencial (`TEC-000001`) e já faz login com a senha que você
definiu. Certificações vão em anexo, PDF ou imagem:

```bash
curl -s -X POST $BASE/tecnicos/$TECNICO/documentos -H "$ADMIN" \
  -F "arquivo=@certificado-nr10.pdf;type=application/pdf"
```

### 3. Os clientes se cadastram sozinhos

`POST /clientes` é anônimo — é o formulário de "criar conta". Exige nome, CPF, telefone, e-mail,
senha forte e **ao menos um imóvel**:

```bash
curl -s -X POST $BASE/clientes -H 'Content-Type: application/json' -d '{
  "nomeCompleto": "Ana Paula Martins",
  "cpf": "66677788899",
  "email": "ana.martins@exemplo.com",
  "telefone": "11940000001",
  "senha": "Senha@123",
  "imoveis": [{
    "apelido": "Apartamento do Centro",
    "tipoImovel": "Apartamento",
    "logradouro": "Rua Sete de Abril", "numero": "120", "complemento": "Apto 71",
    "bairro": "Centro", "cep": "01043000", "cidade": "Sao Paulo", "estado": "SP"
  }]
}'
```

Senha forte quer dizer 8 caracteres, maiúscula, minúscula e caractere especial. O cliente ganha
um código (`CLI-000001`) e o apelido do imóvel é o que ele vê na hora de abrir o chamado.

## O dia a dia do cliente

### Abrir um chamado

```bash
curl -s -X POST $BASE/chamados -H "$CLIENTE" \
  -F "imovelId=$IMOVEL" \
  -F "categoriaServicoId=$CATEGORIA" \
  -F "tipoServicoId=$TIPO" \
  -F "descricaoProblema=Vazamento embaixo da pia da cozinha desde ontem" \
  -F "indicacaoDeRisco=true" \
  -F "urgencia=Media" \
  -F "arquivos=@foto-do-vazamento.jpg;type=image/jpeg"
```

Sem foto, dá para mandar JSON puro no mesmo endereço. Com foto — obrigatória nas categorias
marcadas com `exigeFoto` — vai `multipart/form-data`, e a foto sobe **na mesma requisição** da
abertura: não existe chamado meio-criado esperando anexo.

```json
{ "id": "01a08391-...", "numero": 41, "status": "Aberto", "urgencia": "Alta" }
```

O RF0041 lista quatro dados na abertura — imóvel, categoria, tipo de serviço e descrição — e o
RF0046 dá a classificação de urgência ao administrador. Por isso **o cliente não escolhe a
urgência**: o chamado nasce `Media`, e só sobe para `Alta` sozinho quando a categoria é de risco
e o cliente marca `indicacaoDeRisco`, que é o gatilho que o RN0031 exige.

Limite de **5 arquivos por chamado**, 10 MB cada — a cota é do chamado inteiro, somando o que
entra na abertura e o que o técnico anexa na conclusão.

### Acompanhar, confirmar horário, decidir o orçamento

```bash
curl -s -H "$CLIENTE" "$BASE/chamados/meus?status=Agendado"
curl -s -H "$CLIENTE" "$BASE/chamados/$CHAMADO/historico-status"

curl -s -X POST $BASE/agendamentos/$AGENDAMENTO/confirmacao -H "$CLIENTE"

curl -s -X POST $BASE/orcamentos/$ORCAMENTO/decisao \
  -H "$CLIENTE" -H 'Content-Type: application/json' -d '{"decisao":"Aprovado"}'
```

O histórico mostra cada transição com data, hora, responsável e observação — é a resposta para
"o que aconteceu com o meu chamado".

Na decisão do orçamento, a diferença entre os caminhos é grande:

| Decisão | Corpo | O que acontece |
|---|---|---|
| Aprovar | `{"decisao":"Aprovado"}` | o técnico segue e conclui |
| Recusar | `{"decisao":"Recusado"}` | **o chamado é cancelado** |
| Recusar pedindo outro | `{"decisao":"Recusado","solicitarNovoOrcamento":true}` | o chamado continua em atendimento |
| Não responder em 48 h | — | o chamado é **cancelado automaticamente** |

### Forma de pagamento e fatura

```bash
curl -s -X POST $BASE/clientes/$CLIENTE_ID/formas-pagamento \
  -H "$CLIENTE" -H 'Content-Type: application/json' -d '{
    "tipo": "CartaoCredito", "apelido": "Cartao principal", "principal": true,
    "numeroCartao": "4111111111111111", "nomeTitular": "ANA P MARTINS",
    "validade": "12/2030", "codigoSeguranca": "123"
  }'
```

O número completo e o CVV **não são guardados**: o sistema tokeniza e persiste apenas token,
bandeira, quatro últimos dígitos e validade. Exatamente uma forma fica como principal por vez, e
a exclusão é lógica — a forma some das opções mas o histórico de pagamentos continua íntegro.

A fatura nasce sozinha quando o técnico conclui, com vencimento em 15 dias:

```bash
curl -s -H "$CLIENTE" "$BASE/faturas/minhas?status=Emitida"

curl -s -X POST $BASE/faturas/$FATURA/pagamentos \
  -H "$CLIENTE" -H 'Content-Type: application/json' -d "{\"formaPagamentoId\":\"$FORMA\"}"
```

### Avaliar e acionar garantia

Nota de 1 a 5, **em até 15 dias corridos** após a conclusão:

```bash
curl -s -X POST $BASE/chamados/$CHAMADO/avaliacao \
  -H "$CLIENTE" -H 'Content-Type: application/json' \
  -d '{"nota":5,"comentario":"Chegou no horario e resolveu."}'
```

Todo serviço concluído sai com **90 dias de garantia**. Se o problema voltar:

```bash
curl -s -X POST $BASE/atendimentos/$ATENDIMENTO/garantia/acionamento \
  -H "$CLIENTE" -H 'Content-Type: application/json' \
  -d '{"descricaoProblema":"O vazamento voltou na mesma tubulacao"}'
```

Nasce um chamado novo, marcado como chamado de garantia, urgência Alta, vinculado ao original,
já atribuído ao técnico que fez o serviço — e **sem fatura no fim**. Três condições: dentro dos
90 dias, fatura do serviço original quitada, e mesmo tipo de serviço.

Se o serviço não resolveu de primeira e faz menos de 7 dias, existe um caminho mais curto:
reabrir o próprio chamado, que volta para EM ANÁLISE sem gerar chamado novo.

```bash
curl -s -X POST $BASE/chamados/$CHAMADO/reabertura -H "$CLIENTE"
```

## O dia a dia do técnico

O técnico entra em cena depois que o administrador o atribui a um chamado.

```bash
# 1. Propor o horario. O sistema recusa se colidir com outro atendimento seu.
curl -s -X POST $BASE/chamados/$CHAMADO/agendamentos \
  -H "$TECNICO" -H 'Content-Type: application/json' \
  -d '{"dataHoraProposta":"2026-09-10T09:00:00Z","duracaoEmMinutos":120}'

# 2. Iniciar no local: marca a hora de chegada e leva o chamado para EM ATENDIMENTO.
curl -s -X POST $BASE/chamados/$CHAMADO/atendimento -H "$TECNICO"

# 3. Registrar o orcamento, item a item.
curl -s -X POST $BASE/atendimentos/$ATENDIMENTO/orcamento \
  -H "$TECNICO" -H 'Content-Type: application/json' -d '{
    "itens": [
      {"descricao":"Registro de gaveta 1/2","tipo":"Peca","quantidade":1,"valorUnitario":90.00},
      {"descricao":"Mao de obra","tipo":"MaoDeObra","quantidade":2,"valorUnitario":75.00}
    ]
  }'

# 4. Concluir, com as fotos do servico finalizado.
curl -s -X POST $BASE/atendimentos/$ATENDIMENTO/conclusao -H "$TECNICO" \
  -F "relatoTecnico=Registro trocado e teste de pressao aprovado." \
  -F "arquivos=@servico-finalizado.jpg;type=image/jpeg"
```

A conclusão é a chamada que mais faz coisa acontecer: leva o chamado para CONCLUÍDO, abre a
garantia de 90 dias, **emite a fatura** (peças + mão de obra + taxa de deslocamento da área do
imóvel) e convida o cliente a avaliar. Se houver orçamento pendente de decisão, a conclusão é
barrada.

Serviço simples que não precisa de peça? Pule o orçamento e conclua direto — a fatura sai com
peças e mão de obra zeradas, cobrando só a taxa de deslocamento.

## O dia a dia do administrador

```bash
# A fila de trabalho. Filtros combinam livremente: status, urgencia, categoriaId,
# tecnicoId, clienteId, dataInicio, dataFim, numero.
curl -s -H "$ADMIN" "$BASE/chamados?status=Aberto&urgencia=Alta"

# Ajustar a prioridade quando a automatica nao refletiu a realidade.
curl -s -X PATCH $BASE/chamados/$CHAMADO/urgencia \
  -H "$ADMIN" -H 'Content-Type: application/json' -d '{"urgencia":"Alta"}'

# Atribuir o tecnico: o passo que tira o chamado de ABERTO.
curl -s -X POST $BASE/chamados/$CHAMADO/atribuicao \
  -H "$ADMIN" -H 'Content-Type: application/json' -d "{\"tecnicoId\":\"$TECNICO\"}"

# Cancelar depois que o atendimento comecou e prerrogativa do administrador.
curl -s -X POST $BASE/chamados/$CHAMADO/cancelamento-administrativo \
  -H "$ADMIN" -H 'Content-Type: application/json' \
  -d '{"motivo":"Cliente pediu cancelamento por telefone."}'
```

Trocar de técnico é o mesmo endereço da atribuição, com `PUT`. Nota baixa merece resposta
pública:

```bash
curl -s -H "$ADMIN" "$BASE/tecnicos/$TECNICO/avaliacoes"
curl -s -X POST $BASE/avaliacoes/$AVALIACAO/resposta \
  -H "$ADMIN" -H 'Content-Type: application/json' \
  -d '{"texto":"Lamentamos o atraso e ja ajustamos a rota da equipe."}'
```

### Analisar o histórico

```bash
curl -s -H "$ADMIN" "$BASE/analises/chamados?dataInicio=2025-09-01T00:00:00Z&dataFim=2026-09-01T00:00:00Z"
```

```json
{
  "periodo": { "inicio": "2025-09", "fim": "2026-09" },
  "eixoX": ["2025-09", "2025-10", "..."],
  "series": [
    { "legenda": "Eletrica",   "valores": [1, 3, 3, "..."] },
    { "legenda": "Hidraulica", "valores": [0, 1, 1, "..."] }
  ],
  "totalChamados": 39
}
```

A resposta já vem no formato do gráfico de linhas: eixo X é mês/ano, cada série é uma linha.
Chamados cancelados não entram na conta. O intervalo vai de 1 a 24 meses.

```bash
# Comparar tecnicos, ou olhar so duas categorias.
curl -s -H "$ADMIN" "$BASE/analises/chamados?dataInicio=...&dataFim=...&agruparPor=tecnico&tecnicoIds=$T1&tecnicoIds=$T2"
curl -s -H "$ADMIN" "$BASE/analises/chamados?dataInicio=...&dataFim=...&categoriaIds=$C1&categoriaIds=$C2"

# Levar para a reuniao: CSV com ponto e virgula, abre direto no Excel.
curl -s -OJ -H "$ADMIN" "$BASE/analises/chamados/exportacao?dataInicio=...&dataFim=..."
```

### Manutenção do cadastro

Cliente que saiu, técnico que se desligou: **inative, não apague**. Inativo não faz login e não
recebe chamado, mas o histórico continua de pé.

```bash
curl -s -X PATCH $BASE/clientes/$CLIENTE_ID/inativacao -H "$ADMIN"
curl -s -X PATCH $BASE/tecnicos/$TECNICO/inativacao   -H "$ADMIN"
```

## O que acontece sozinho

Uma única rotina de segundo plano, a cada 15 minutos e uma vez ao subir a aplicação:

| Rotina | Requisito | O que faz |
|---|---|---|
| Expiração de orçamento | RN0043 | passou de 48 h sem decisão: expira o orçamento e cancela o chamado |

É o único prazo que o DRS manda o sistema cumprir sozinho — *"caso o prazo expire sem
manifestação, o chamado deve ser automaticamente cancelado"*. Os outros três prazos são
verificados na hora do pedido, não por rotina: a reabertura de 7 dias (RN0035), a janela de
avaliação de 15 dias (RN0052) e a garantia de 90 dias (RN0072) recusam a operação quando o
prazo já passou.

O status `Vencida` da fatura existe no modelo e é filtrável na consulta (RF0083), mas nada no
DRS pede que o sistema faça essa transição sozinho — nenhum requisito descreve quem a executa.

Além disso, **toda mudança de status notifica o cliente** e **toda escrita é auditada**. Senha e
token de cartão aparecem no log como `"***"` — nunca em claro.

## Quando o sistema diz não

Erros de negócio vêm em ProblemDetails (RFC 9457), com a mensagem pronta para mostrar ao
usuário:

```json
{
  "title": "Regra de negocio violada",
  "status": 422,
  "detail": "O tecnico nao possui a categoria do chamado entre suas especialidades."
}
```

A coluna *Regra* da tabela abaixo é deste documento, não da resposta.

| Você tentou | Resposta | Regra | Saída |
|---|---|---|---|
| Abrir chamado sem foto em categoria que exige | 409 `RN0032` | foto obrigatória | anexe a foto na mesma requisição |
| Atribuir técnico sem a especialidade | 422 `RN0022` | especialidade | escolha outro ou acrescente a categoria a ele |
| Atribuir técnico fora da área | 422 `RN0023` | área de atendimento | cadastre a faixa de CEP para ele |
| Atribuir a técnico inativo | 422 `RF0023` | inativo não atende | reative antes |
| Agendar em horário ocupado | 409 `RN0041` | agenda do técnico | escolha outra janela |
| Concluir com orçamento pendente | 422 `RN0043` | decisão do cliente | espere a aprovação ou a expiração |
| Cancelar chamado já em atendimento | 422 `RN0033` | só ABERTO ou AGENDADO | use o cancelamento administrativo |
| Pular status (ABERTO → CONCLUÍDO) | 422 `RN0034` | fluxo de status | siga a sequência |
| Reabrir depois de 7 dias | 422 `RN0035` | prazo de reabertura | acione a garantia |
| Avaliar depois de 15 dias | 409 `RN0052` | janela de avaliação | a janela fechou |
| Acionar garantia com fatura em aberto | 409 `RN0072` | fatura quitada | pague a fatura primeiro |
| Remover o último imóvel do cliente | 422 `RN0011` | ao menos um imóvel | cadastre outro antes |
| Sexto anexo no chamado | 422 `RNF0043` | 5 arquivos por chamado | remova ou agrupe |

Fora isso: **401** é token ausente, expirado ou inválido; **403** é papel errado ou recurso de
outra pessoa; **400** é corpo ou parâmetro malformado, com a lista de campos em `errors`.

O fluxo de status, para consulta rápida:

```
ABERTO ──► EM ANÁLISE ──► AGENDADO ──► EM ATENDIMENTO ──► CONCLUÍDO
   │            │             │               │                │
   └────────────┴─────────────┴───────────────┴──► CANCELADO    │
                                                                │
                            EM ANÁLISE ◄── reabertura (7 dias) ─┘
```

## O que ainda não é real

Limites desta versão, para você não descobrir em produção:

- **Notificação (RNF0041) não sai da máquina.** O notificador grava log estruturado e uma linha
  em `notificacao_enviada`. Não há e-mail nem push. A troca por SMTP ou push é implementar
  `INotificador` — o domínio não muda.
- **O gateway de pagamento é simulado.** Nenhuma cobrança real acontece. A tokenização é fingida
  e a autorização aprova tudo, com uma exceção proposital para testar recusa: **valor terminado
  em `,99` é recusado** com "saldo insuficiente".
- **Anexos sobem, mas não descem.** Os arquivos ficam num volume local sob `storage/` e há
  endpoint de upload para chamado e para documento do técnico — mas **não existe endpoint de
  download**. Hoje o arquivo é recuperável só pelo volume.
- **Boleto é só cadastro.** O tipo existe como forma de pagamento; não há emissão, linha
  digitável nem baixa por retorno bancário.
- **Não há exclusão de cadastro.** Cliente e técnico são inativados, nunca removidos — decisão
  para preservar o histórico de chamados e faturas.

## Conhecer o sistema em 10 minutos

Com o administrador criado e a API no ar, importe a [collection do
Postman](postman/README.md) e rode a pasta **Jornada completa**: 18 passos que montam o cenário
e percorrem o caminho inteiro sem intervenção — catálogo, equipe, conta do cliente, abertura,
triagem, agendamento, atendimento, orçamento, conclusão, fatura, pagamento, avaliação e
garantia. No fim ela imprime o histórico de status do chamado, que é a jornada resumida numa
linha.

Se preferir na mão, a mesma sequência está em [`docs/TESTES_MANUAIS.md`](docs/TESTES_MANUAIS.md),
com o `curl` de cada passo.

---

# Parte 3 — Estrutura

```
src/
  ChamadosManutencao.Domain/           entidades, invariantes, máquina de estados (sem dependências)
  ChamadosManutencao.Application/      handlers por caso de uso, validators, DTOs, abstrações
  ChamadosManutencao.Infrastructure/   EF Core, migrations, repositórios, jobs, serviços simulados
  ChamadosManutencao.Api/              Minimal APIs, segurança, middlewares
tests/
  ChamadosManutencao.UnitTests/        uma classe de teste por regra de negócio
  ChamadosManutencao.IntegrationTests/ endpoints reais contra PostgreSQL em Testcontainers
postman/                               collection, ambiente e o gerador que a mantém em dia
docs/                                  DRS, diagramas, matriz de rastreabilidade e arquitetura
```

Dependências: `Api → Application → Domain` e `Infrastructure → Application → Domain`.
