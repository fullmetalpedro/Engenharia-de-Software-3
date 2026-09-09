# Roteiro de testes manuais

Cenários exercitados **na API em execução**, com `curl` contra `http://localhost:5080` e
PostgreSQL 17 em container. Não é a suíte automatizada: cada requisição abaixo foi disparada à
mão e a resposta conferida.

Ambiente da execução registrada aqui:

- base recriada do zero e populada com `dotnet run --project src/ChamadosManutencao.Api -- seed`
- API em `Development`, jobs ligados
- senha de todos os usuários do seed: `Senha@123`

Preparação comum a todos os blocos:

```bash
BASE=http://localhost:5080/api/v1

login() {
  curl -s -X POST $BASE/auth/login -H 'Content-Type: application/json' \
    -d "{\"email\":\"$1\",\"senha\":\"Senha@123\"}" \
    | python -c "import sys,json;print(json.load(sys.stdin)['accessToken'])"
}

ADMIN="Authorization: Bearer $(login admin@chamados.local)"
CLIENTE="Authorization: Bearer $(login ana.martins@exemplo.com)"
TECNICO="Authorization: Bearer $(login carlos.ribeiro@chamados.local)"
```

Legenda da coluna **Resultado**: ✅ conforme o esperado.

---

## Autenticação e autorização

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 1 | Login com credenciais válidas | RNF0022 | 200 com `accessToken` e `papel` | ✅ papéis `Administrador`, `Tecnico`, `Cliente` |
| 2 | Login com senha errada | RNF0022 | 401 | ✅ 401 |
| 3 | Requisição sem token | — | 401 | ✅ 401 em `/clientes` |
| 4 | Cliente em rota de administrador | — | 403 | ✅ 403 em `GET /clientes` |
| 5 | Técnico em rota de cliente | — | 403 | ✅ 403 em `GET /chamados/meus` |
| 6 | Nenhuma resposta expõe senha ou hash | RNF0022 | zero ocorrências | ✅ 0 ocorrências de "senha" no corpo |

```bash
curl -s -X POST $BASE/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"admin@chamados.local","senha":"Errada@123"}' -o /dev/null -w '%{http_code}\n'
```

---

## UC01 — Clientes

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 7 | Cadastro válido com um imóvel | RF0011, RN0011, RNF0023 | 201 com código `CLI-` | ✅ `CLI-000007` |
| 8 | Cadastro sem imóvel | RN0011 | 400 | ✅ `errors.Imoveis` |
| 9 | Senha fraca | RNF0021 | 400 | ✅ mensagem da política |
| 10 | Consulta por e-mail, nome e código | RF0015 | filtro combinável | ✅ 1 e 2 resultados conforme o filtro |
| 11 | Alterar nome, e-mail e telefone | RF0012 | 200 com dados novos | ✅ |
| 12 | Inativar e reativar | RF0013, RF0014 | 200; login bloqueado enquanto inativo | ✅ inativo → login 401; reativado → 200 |
| 13 | Adicionar segundo imóvel | RF0016 | 201 | ✅ 2 imóveis |
| 14 | Imóvel com CEP inválido | RN0012 | 400 | ✅ "O CEP deve conter 8 digitos" |
| 15 | Remover imóvel quando há outro | RF0016 | 204 | ✅ |
| 16 | Remover o último imóvel | RN0011 | 422 | ✅ `requisito: RN0011` |

---

## UC02 — Técnicos

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 17 | Cadastrar técnico | RF0021, RNF0031 | 201 com código `TEC-` | ✅ `TEC-000005` |
| 18 | Consulta por bairro e por CEP | RF0025 | filtra pela área de atendimento | ✅ Carlos por "Centro", João por CEP `03078000` |
| 19 | Alterar dados | RF0022 | 200 | ✅ |
| 20 | Inativar e reativar | RF0023, RF0024 | 200 | ✅ |
| 21 | Atribuir chamado a técnico inativo | RF0023 | 422 | ✅ "Tecnico inativo nao pode receber chamados" |
| 22 | Definir especialidades | RF0026 | substitui a lista inteira | ✅ passou a ter 2 |
| 23 | Especialidades vazias | RN0021 | 400 | ✅ |
| 24 | Definir áreas de atendimento | RF0027 | 200 com as taxas | ✅ Centro 28,50 / Bela Vista 32,00 |
| 25 | Faixa de CEP invertida | RF0027 | 422 | ✅ "CEP inicial ... nao pode ser maior que o CEP final" |
| 26 | Documento PDF | RNF0032 | 201 | ✅ |
| 27 | Documento DOCX | RNF0032 | recusado | ✅ 422 |

---

## UC03 — Catálogo

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 28 | Listar categorias | RF0034 | 4 categorias com `exigeFoto` e `categoriaDeRisco` | ✅ |
| 29 | Cadastrar categoria | RF0031 | 201 | ✅ "Pintura" |
| 30 | Cadastrar tipo de serviço | RF0032 | 201 vinculado à categoria | ✅ |
| 31 | Tipo com nome repetido na categoria | RF0032 | 422 | ✅ |
| 32 | Alterar categoria e tipo | RF0033 | 200 | ✅ |
| 33 | Catálogo exige autenticação | — | 401 sem token | ✅ |

---

## UC04 — Abertura de chamados

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 34 | Abertura sem foto em categoria que exige | RN0032 | recusada | ✅ 409, `requisito: RN0032` |
| 35 | Abertura com foto (multipart) | RF0041, RF0042, RN0032 | 201 | ✅ chamado nº 41 |
| 36 | Número sequencial | RNF0042 | incrementa | ✅ 41 → 42 → 43 → 44 |
| 37 | Categoria sem risco + indicação de risco | RN0031 | mantém a urgência informada | ✅ `Baixa` |
| 38 | Sexto anexo do mesmo chamado | RNF0043 | recusado | ✅ 5 aceitos (a foto da abertura conta), 6º = 422 |
| 39 | Cliente lista só os próprios chamados | RF0043 | 10 chamados da Ana | ✅ |
| 40 | Cancelar chamado ABERTO | RF0045, RN0033 | 204 | ✅ status vira `Cancelado` |
| 41 | Cancelar chamado EM ATENDIMENTO | RN0033 | recusado | ✅ 422, `requisito: RN0033` |
| 42 | Histórico de status | RF0050 | uma linha por transição, com hora e observação | ✅ 5 linhas de `Aberto` a `Concluido` |
| 43 | Reabertura dentro de 7 dias | RN0035 | volta para EM ANÁLISE | ✅ 204, status `EmAnalise` |

```bash
# 35 — abertura com foto
printf '\xff\xd8\xff\xe0\x00\x10JFIF' > foto.jpg
curl -s -X POST $BASE/chamados -H "$CLIENTE" \
  -F "imovelId=$IMOVEL" -F "categoriaServicoId=$CATEGORIA" -F "tipoServicoId=$TIPO" \
  -F "descricaoProblema=Vazamento sob a pia" -F "indicacaoDeRisco=false" -F "urgencia=Media" \
  -F "arquivos=@foto.jpg;type=image/jpeg"
```

---

## UC05 — Triagem

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 44 | Administrador lista todos os chamados | RF0044 | 40 do seed, filtráveis por status | ✅ 6/5/6/4/15/4 por status |
| 45 | Classificar urgência | RF0046 | 204 | ✅ virou `Alta` |
| 46 | Atribuir técnico | RF0047 | 204, chamado vai para EM ANÁLISE | ✅ |
| 47 | Atribuir técnico sem a especialidade | RN0022 | 422 | ✅ `requisito: RN0022` |
| 48 | Atribuir técnico fora da área | RN0023 | 422 | ✅ `requisito: RN0023` |
| 49 | Reatribuir a técnico habilitado | RF0048 | 204 | ✅ técnico trocado |
| 50 | Alterar status manualmente | RF0049 | 204 | ✅ `Aberto` → `EmAnalise` |
| 51 | Transição inválida | RN0034 | 422 | ✅ "EmAnalise nao pode ir para Concluido" |
| 52 | Cancelamento administrativo em atendimento | RF0045, RN0033 | 204 | ✅ |

---

## UC06 — Agendamento

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 53 | Técnico propõe horário | RF0051 | 201, `origemProposta: Tecnico`, chamado AGENDADO | ✅ |
| 54 | Cliente confirma | RF0052 | 200, `dataHoraConfirmacao` preenchida | ✅ |
| 55 | Reagendamento pelo cliente | RF0053 | novo agendamento; o anterior vira REAGENDADO | ✅ 3 agendamentos, 2 `Reagendado` + 1 `Proposto` |
| 56 | Janela sobreposta do mesmo técnico | RN0041 | recusada | ✅ 409, `requisito: RN0041` |
| 57 | Agendar chamado sem técnico | RF0051 | recusado | ✅ 409 |

---

## UC07 — Execução

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 58 | Técnico inicia atendimento | RF0054 | 201, chamado EM ATENDIMENTO | ✅ |
| 59 | Registrar orçamento | RF0055, RN0043 | pendente, prazo de 48 h | ✅ 90 + 150 = 240; prazo em +2 dias |
| 60 | Concluir com orçamento pendente | RN0043 | recusado | ✅ 422, `requisito: RN0043` |
| 61 | Cliente aprova | RF0056 | 200 | ✅ |
| 62 | Cliente recusa sem pedir novo | RN0042 | chamado cancelado | ✅ status `Cancelado` |
| 63 | Conclusão com foto | RF0057 | 200, garantia de 90 dias | ✅ `prazoDias: 90`, `vigente: true` |
| 64 | Fatura gerada na conclusão | RF0082, RN0071 | peças + mão de obra + taxa | ✅ 90 + 150 + 25 = **265,00**, vencimento em +15 dias |

---

## UC08 — Avaliação

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 65 | Avaliar chamado concluído | RF0061 | 201 | ✅ nota 5 |
| 66 | Nota fora de 1..5 | RF0061 | 400 | ✅ |
| 67 | Avaliação após 15 dias da conclusão | RN0052 | recusada | ✅ 409, "O prazo de 15 dias corridos para avaliar ja expirou" |
| 68 | Avaliação dentro do prazo, mesmo chamado | RN0052 | 201 | ✅ (conclusão trazida para D-1) |
| 69 | Administrador responde publicamente | RF0063 | 200 | ✅ |
| 70 | Avaliações do técnico | RF0062 | lista com a resposta anexada | ✅ 3 avaliações, 1 respondida |

---

## UC09 — Análise

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 71 | Série mensal por categoria | RF0071, RN0061, RNF0051 | eixo X mês/ano, uma série por categoria | ✅ 13 colunas `2025-09`…`2026-09`, séries Eletrica (30) e Hidraulica (9) |
| 72 | Cancelados fora da contagem | RN0063 | total ignora CANCELADO | ✅ 43 chamados − 4 cancelados = 39 |
| 73 | Intervalo de 25 meses | RN0062 | 400 | ✅ "no minimo 1 e no maximo 24 meses" |
| 74 | Exportação | RF0074 | CSV com `Content-Disposition` | ✅ `text/csv`, `analise-chamados-202509-202609.csv` |
| 75 | Cliente tentando abrir a análise | — | 403 | ✅ |

O CSV veio assim:

```csv
serie;2025-09;2025-10;2025-11;...;2026-09;total
Eletrica;1;3;3;...;0;30
Hidraulica;0;1;1;...;3;9
```

---

## UC10 — Formas de pagamento

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 76 | Cadastrar cartão | RF0081 | 201 | ✅ `Visa **** 1111`, principal |
| 77 | Número completo na resposta | RNF0061 | ausente | ✅ 0 ocorrências de `4111111111111111` |
| 78 | Número completo no banco | RNF0061 | ausente | ✅ `SELECT count(*) ... = 0` |

```bash
docker exec chamados_postgres psql -U chamados -d chamados -tAc \
  "SELECT count(*) FROM forma_pagamento
   WHERE token_operadora LIKE '%4111111111111111%'
      OR ultimos_quatro_digitos = '4111111111111111';"
```

---

## UC11 — Faturamento

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 79 | Pagar fatura em aberto | RF0084 | 201 com identificador da transação | ✅ `TRX-20260909003004571`, 265,00 |
| 80 | Fatura passa a PAGA | RF0084 | status `Paga` | ✅ |
| 81 | Consulta do cliente | RF0083 | só as próprias | ✅ 3 faturas da Ana |
| 82 | Filtro por status e por número do chamado | RF0083 | combinável | ✅ |

---

## UC12 — Garantia

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 83 | Acionar garantia com fatura em aberto | RN0072 | recusado | ✅ 409, "A fatura correspondente ao atendimento nao esta quitada" |
| 84 | Acionar com fatura quitada | RF0085 | 201, chamado de garantia | ✅ chamado nº 43, `chamadoDeGarantia: true` |
| 85 | Vínculo com o chamado original | RF0085 | `chamadoOriginalId` preenchido | ✅ |
| 86 | Volta ao técnico original | RF0085 | `atribuidoAoTecnicoOriginal: true` | ✅ |

---

## Transversais

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 87 | Tempo de resposta das consultas | RNF0011 | ≤ 1000 ms | ✅ 16 a 47 ms nas seis consultas medidas |
| 88 | Toda escrita no log de transação | RNF0012 | data, hora, usuário, entidade, antes e depois | ✅ 451 registros |
| 89 | Senha e token do cartão no log | RNF0022, RNF0061 | mascarados | ✅ gravados como `"***"` |
| 90 | Notificação a cada mudança de status | RNF0041 | uma por transição | ✅ inclusive "Avalie o atendimento" (RN0051) e "Fatura vencida" |
| 91 | Documentação OpenAPI | — | servida e agrupada por UC | ✅ 68 operações, UC01 a UC12 |

O cabeçalho de tempo vem em toda resposta:

```bash
curl -s -D- -o /dev/null -H "$ADMIN" $BASE/chamados | grep -i X-Tempo-De-Resposta-Ms
# X-Tempo-De-Resposta-Ms: 47
```

---

## Jobs de segundo plano

Os quatro jobs executam uma vez no startup e depois a cada 15 minutos. Para vê-los agindo, deixe
um orçamento e uma fatura vencidos e reinicie a API:

```sql
UPDATE fatura   SET status = 1, data_vencimento  = now() - interval '3 days' WHERE id = '...';
UPDATE orcamento SET status = 1, prazo_aprovacao = now() - interval '5 hours' WHERE id = '...';
```

| # | Cenário | Requisito | Esperado | Resultado |
|---|---|---|---|---|
| 92 | `ExpiracaoOrcamentoJob` | RN0043 | orçamento expira e o chamado é cancelado | ✅ `1 registro(s) processado(s)`; orçamento → `Expirado` |
| 93 | `VencimentoFaturaJob` | RF0083 | fatura emitida vencida vira VENCIDA | ✅ `1 registro(s) processado(s)`; nenhuma fatura ficou em `Emitida` |

```
[21:34:49 INF] ExpiracaoOrcamentoJob: 1 registro(s) processado(s).
[21:34:49 INF] VencimentoFaturaJob: 1 registro(s) processado(s).
```

---

## Defeito encontrado nesta rodada

**Parâmetro de query malformado devolvia 500 em vez de 400.**

Enviando `?dataInicio=abacaxi` (ou qualquer data, GUID ou enum que o ASP.NET não consiga
converter), o `BadHttpRequestException` não era tratado pelo middleware de exceção e escapava
como *Erro interno*:

```json
{"title":"Erro interno","status":500,"instance":"/api/v1/analises/chamados"}
```

A suíte automatizada não pegou porque todos os testes enviam valores bem formados. Corrigido em
`MiddlewareDeExcecao`, que agora mapeia `BadHttpRequestException` para o status que a própria
exceção carrega:

```json
{"title":"Requisicao malformada","status":400,
 "detail":"Failed to bind parameter \"DateTimeOffset dataInicio\" from \"abacaxi\"."}
```

---

## Não verificado

- **Renderização do Scalar no navegador.** O documento OpenAPI e a página HTML são servidos
  corretamente (HTTP 200, título `Chamados de Manutencao`, 68 operações), mas a automação de
  navegador desta sessão não conseguiu carregar `localhost`. Abra
  `http://localhost:5080/scalar/` manualmente para conferir o visual.
- **RN0062, piso de 1 mês.** O validador conta meses inteiros de forma inclusiva
  (`fim.Month - inicio.Month + 1`), então qualquer intervalo válido resulta em no mínimo 1 e o
  piso nunca reprova nada. Só o teto de 24 meses e `fim < inicio` reprovam. Se a intenção da
  regra era exigir 30 dias corridos, o validador precisa mudar.
