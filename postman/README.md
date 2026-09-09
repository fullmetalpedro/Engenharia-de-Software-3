# Collection do Postman

Enquanto o sistema não tem tela, esta collection é a interface para operá-lo: os 68 endpoints
organizados por caso de uso, com corpo preenchido, token certo em cada requisição e os
identificadores passando de uma chamada para a outra.

| Arquivo | O que é |
|---|---|
| `ChamadosManutencao.postman_collection.json` | a collection: 16 pastas, 112 requisições |
| `Chamados-local.postman_environment.json` | ambiente apontando para `http://localhost:5080` |
| `gerar-collection.py` | regenera os dois a partir do OpenAPI da API |

---

## Começar

1. Suba o ambiente e popule a base — passo a passo em [`../docs/COMO_RODAR.md`](../docs/COMO_RODAR.md):

   ```bash
   docker compose up -d postgres
   dotnet run --project src/ChamadosManutencao.Api -- seed
   dotnet run --project src/ChamadosManutencao.Api
   ```

2. No Postman: **Import** → arraste os dois arquivos `.json`.

3. Selecione o ambiente **Chamados - local** no canto superior direito.

4. Abra a pasta **Começar aqui** e rode ela inteira (botão *Run folder*). Ela autentica e
   carrega os identificadores do seed nas variáveis da collection.

Pronto. A partir daí, qualquer requisição das pastas UC01 a UC12 funciona sem você copiar
GUID nenhum.

---

## Como está organizada

| Pasta | Para quê |
|---|---|
| **Começar aqui** | login e carga dos ids do seed. Rode primeiro. |
| **Jornada completa** | o caminho feliz inteiro, em 18 passos, do catálogo à garantia. Roda de cima para baixo sem intervenção — é a demonstração do sistema. |
| **Autenticação** | login, refresh e troca de senha |
| **UC01 a UC12** | um caso de uso por pasta, na ordem da matriz de rastreabilidade |
| **Infraestrutura** | health check |

### Token por papel, automático

Cada requisição já sai com o token do papel que o endpoint exige — não é preciso trocar de
token na mão. O endpoint de triagem usa `{{tokenAdministrador}}`, o de abertura de chamado usa
`{{tokenCliente}}`, o de conclusão usa `{{tokenTecnico}}`. Os três são preenchidos pela pasta
**Começar aqui**.

Se quiser mandar uma requisição com outro papel para ver o 403, troque o token na aba
*Authorization* dela.

### Encadeamento

Toda requisição que cria alguma coisa guarda o identificador numa variável de collection:

```
POST /chamados            → chamadoId, numeroChamado
POST /chamados/{id}/atendimento → atendimentoId
POST /atendimentos/{id}/orcamento → orcamentoId
POST /atendimentos/{id}/conclusao → faturaId, garantiaId
POST /chamados/{id}/avaliacao → avaliacaoId
```

Por isso a ordem das pastas importa: UC07 usa o `atendimentoId` que o próprio UC07 criou, UC11
usa a `faturaId` que a conclusão gerou.

O login também extrai o identificador do usuário da claim `sub` do token, então `clienteId` e
`tecnicoId` sempre correspondem a quem está autenticado.

### Filtros

Os parâmetros de query vêm desabilitados, com os valores de exemplo já preenchidos. Marque a
caixinha dos que quiser usar. Onde o parâmetro é um enum, a descrição lista os valores aceitos.

### Upload

Quatro requisições enviam arquivo (`multipart/form-data`): anexo do chamado, documento do
técnico, e as variantes *com foto* da abertura e da conclusão. Selecione o arquivo no campo
antes de enviar — o Postman não guarda o caminho no `.json` exportado.

---

## Duas armadilhas

**`Autenticação > Troca a senha do usuário autenticado`** muda a senha de verdade. Se rodar com
o token do administrador, o `Senha@123` do seed deixa de valer para ele e os próximos logins
falham com 401. Para voltar ao estado inicial:

```bash
docker compose down -v && docker compose up -d postgres
dotnet run --project src/ChamadosManutencao.Api -- seed
```

**Rodar a collection inteira de uma vez não funciona** — e não deveria. As pastas por caso de
uso são um painel de controle, não um script: `UC04 > Cancelar chamado` cancela o mesmo chamado
que `UC05 > Atribuir técnico` iria triar. Para uma execução de ponta a ponta, use a pasta
**Jornada completa**, que foi montada para isso.

---

## Rodar pela linha de comando

Com o [newman](https://github.com/postmanlabs/newman):

```bash
npm install -g newman

newman run postman/ChamadosManutencao.postman_collection.json \
  -e postman/Chamados-local.postman_environment.json \
  --folder "Jornada completa"
```

Saída esperada numa base recém-populada: **30 requisições, 30 asserções, 0 falhas**.

---

## Regenerar

A collection é derivada do documento OpenAPI que a própria API publica, então ela acompanha o
código. Depois de acrescentar ou mudar um endpoint:

```bash
curl -s http://localhost:5080/openapi/v1.json -o /tmp/openapi.json

python postman/gerar-collection.py /tmp/openapi.json \
  postman/ChamadosManutencao.postman_collection.json \
  postman/Chamados-local.postman_environment.json
```

O gerador acrescenta ao que vem do OpenAPI aquilo que ele não sabe: qual papel cada endpoint
exige (lido das políticas de autorização), os exemplos de corpo, os scripts de encadeamento, a
pasta de bootstrap e a jornada completa. Ao mexer nas políticas de autorização da API, atualize
o dicionário `POLITICA` no topo do gerador.
