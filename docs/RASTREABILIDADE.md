# Rastreabilidade — 78 requisitos

Cada requisito do DRS tem código que o implementa e teste que o prova. Requisito sem linha
preenchida significa trabalho não concluído; não há nenhum nesta tabela.

| Origem | Quantidade |
|---|---|
| Requisitos funcionais (RF) | 46 |
| Regras de negócio (RN) | 20 |
| Requisitos não funcionais (RNF) | 12 |
| **Total** | **78** |

Prefixo de todas as rotas: `/api/v1`. Os testes citados estão em
`tests/ChamadosManutencao.UnitTests` (regras de domínio) e
`tests/ChamadosManutencao.IntegrationTests` (endpoints reais contra PostgreSQL em
Testcontainers).

---

## 1. Requisitos funcionais

### UC01 Gerenciar Cadastro de Clientes

| Requisito | Caso de uso | Endpoint | Classe/método | Teste |
|---|---|---|---|---|
| RF0011 Cadastrar cliente | UC01 | `POST /clientes` | `CadastrarClienteHandler.ExecutarAsync` | `UC01ClientesTests.Cadastro_cria_cliente_com_imovel_e_codigo_unico` |
| RF0012 Alterar cliente | UC01 | `PUT /clientes/{id}` | `AlterarClienteHandler.ExecutarAsync` | `UC01ClientesTests.Alteracao_atualiza_nome_email_e_telefone` |
| RF0013 Inativar cliente | UC01 | `PATCH /clientes/{id}/inativacao` | `AlterarSituacaoDoClienteHandler.ExecutarAsync`, `Usuario.Inativar` | `UC01ClientesTests.Inativacao_e_reativacao_alternam_a_situacao`, `AutenticacaoTests.Login_de_cliente_inativo_e_recusado` |
| RF0014 Ativar cliente | UC01 | `PATCH /clientes/{id}/ativacao` | `AlterarSituacaoDoClienteHandler.ExecutarAsync`, `Usuario.Ativar` | `UC01ClientesTests.Inativacao_e_reativacao_alternam_a_situacao`, `RNF0022AutenticacaoTests.Reativacao_devolve_o_acesso` |
| RF0015 Consultar clientes | UC01 | `GET /clientes`, `GET /clientes/{id}` | `ConsultarClientesHandler`, `ObterClienteHandler` | `UC01ClientesTests.Consulta_filtra_por_nome_email_e_situacao`, `UC01ClientesTests.Consulta_de_cliente_inexistente_devolve_404` |
| RF0016 Imóveis do cliente | UC01, UC04 | `POST/GET/PUT/DELETE /clientes/{id}/imoveis` | `ImoveisDoClienteHandler` | `UC01ClientesTests.Cliente_adiciona_altera_e_lista_imoveis` |

### UC02 Gerenciar Cadastro de Técnicos

| Requisito | Caso de uso | Endpoint | Classe/método | Teste |
|---|---|---|---|---|
| RF0021 Cadastrar técnico | UC02 | `POST /tecnicos` | `CadastrarTecnicoHandler.ExecutarAsync` | `UC02TecnicosTests.Cadastro_cria_tecnico_com_codigo_especialidade_e_area` |
| RF0022 Alterar técnico | UC02 | `PUT /tecnicos/{id}` | `ManterTecnicoHandler.AlterarAsync` | `UC02TecnicosTests.Alteracao_atualiza_dados_cadastrais` |
| RF0023 Inativar técnico | UC02 | `PATCH /tecnicos/{id}/inativacao` | `ManterTecnicoHandler.AlterarSituacaoAsync` | `UC02TecnicosTests.Inativacao_e_reativacao_alternam_a_situacao`, `UC05TriagemTests.Atribuicao_a_tecnico_inativo_e_recusada` |
| RF0024 Ativar técnico | UC02 | `PATCH /tecnicos/{id}/ativacao` | `ManterTecnicoHandler.AlterarSituacaoAsync` | `UC02TecnicosTests.Inativacao_e_reativacao_alternam_a_situacao` |
| RF0025 Consultar técnicos | UC02, UC05 | `GET /tecnicos`, `GET /tecnicos/{id}` | `ConsultarTecnicosHandler`, `ObterTecnicoHandler` | `UC02TecnicosTests.Consulta_filtra_por_especialidade_bairro_e_cep` |
| RF0026 Especialidades | UC02, UC05 | `PUT /tecnicos/{id}/especialidades` | `ManterTecnicoHandler.DefinirEspecialidadesAsync`, `Tecnico.DefinirEspecialidades` | `UC02TecnicosTests.Definir_especialidades_substitui_a_lista` |
| RF0027 Áreas de atendimento | UC02, UC05 | `PUT /tecnicos/{id}/areas-atendimento` | `ManterTecnicoHandler.DefinirAreasAsync`, `AreaAtendimento` | `UC02TecnicosTests.Area_de_atendimento_com_faixa_invertida_e_recusada`, `RF0031CatalogoTests.Alterar_area_de_atendimento_atualiza_faixa_e_taxa` |

### UC03 Gerenciar Catálogo de Serviços

| Requisito | Caso de uso | Endpoint | Classe/método | Teste |
|---|---|---|---|---|
| RF0031 Cadastrar categoria | UC03 | `POST /categorias-servico` | `ManterCatalogoHandler.CadastrarCategoriaAsync` | `UC03CatalogoTests.Cadastro_de_categoria_guarda_exige_foto_e_categoria_de_risco` |
| RF0032 Cadastrar tipo de serviço | UC03 | `POST /categorias-servico/{id}/tipos-servico` | `CategoriaServico.AdicionarTipoServico` | `UC03CatalogoTests.Cadastro_de_tipo_de_servico_fica_vinculado_a_categoria`, `RF0031CatalogoTests.Tipo_de_servico_com_nome_repetido_na_categoria_e_recusado` |
| RF0033 Alterar categoria ou tipo | UC03 | `PUT /categorias-servico/{id}`, `PUT /tipos-servico/{id}` | `ManterCatalogoHandler.AlterarCategoriaAsync`, `.AlterarTipoServicoAsync` | `UC03CatalogoTests.Alteracao_de_categoria_e_de_tipo_de_servico` |
| RF0034 Consultar catálogo | UC03, UC04 | `GET /categorias-servico`, `GET /tipos-servico` | `ConsultarCatalogoHandler` | `UC03CatalogoTests.Consulta_de_catalogo_esta_disponivel_para_o_cliente` |

### UC04 Abrir Chamado de Manutenção

| Requisito | Caso de uso | Endpoint | Classe/método | Teste |
|---|---|---|---|---|
| RF0041 Abrir chamado | UC04, UC12 | `POST /chamados` | `AbrirChamadoHandler.ExecutarAsync`, `Chamado.Abrir` (quatro dados; urgência não é entrada) | `UC04ChamadosTests.Abertura_cria_chamado_aberto_com_numero_sequencial` |
| RF0042 Anexar mídia | UC04 | `POST /chamados/{id}/anexos` | `AnexarMidiaAoChamadoHandler`, `Anexo.ParaChamado` | `UC04ChamadosTests.Sexto_anexo_do_chamado_e_recusado` |
| RF0043 Consultar chamados (cliente) | UC04 | `GET /chamados/meus`, `GET /chamados/{id}` | `ConsultarChamadosHandler.MeusChamadosAsync`, `ObterChamadoHandler` | `UC04ChamadosTests.Cliente_consulta_somente_os_proprios_chamados` |
| RF0044 Consultar chamados (administrador) | UC05 | `GET /chamados` | `ConsultarChamadosHandler.ConsultarAsync` | `UC05TriagemTests.Administrador_consulta_chamados_de_todos_os_clientes` |
| RF0045 Cancelar chamado | UC04, UC05 | `POST /chamados/{id}/cancelamento`, `.../cancelamento-administrativo` | `CancelarChamadoHandler`, `CancelamentoAdministrativoHandler` | `UC04ChamadosTests.Cliente_cancela_chamado_aberto`, `UC05TriagemTests.Cancelamento_administrativo_funciona_com_o_chamado_em_atendimento` |
| RF0046 Classificar urgência | UC05 | `PATCH /chamados/{id}/urgencia` | `ClassificarUrgenciaHandler`, `Chamado.ClassificarUrgencia` | `UC05TriagemTests.Administrador_classifica_a_urgencia_do_chamado` |
| RF0047 Atribuir técnico | UC05, UC12 | `POST /chamados/{id}/atribuicao` | `AtribuirTecnicoHandler`, `Chamado.AtribuirTecnico` | `UC05TriagemTests.Atribuicao_de_tecnico_leva_o_chamado_para_em_analise` |
| RF0048 Reatribuir técnico | UC05 | `PUT /chamados/{id}/atribuicao` | `AtribuirTecnicoHandler` | `UC05TriagemTests.Reatribuicao_troca_o_tecnico_do_chamado` |
| RF0049 Alterar status | UC05, UC06, UC07 | `PATCH /chamados/{id}/status` | `AlterarStatusDoChamadoHandler`, `Chamado.AlterarStatus` | `UC05TriagemTests.Alteracao_de_status_segue_a_maquina_de_estados` |
| RF0050 Histórico de status | UC04, UC05 | `GET /chamados/{id}/historico-status` | `ConsultarHistoricoDeStatusHandler`, `HistoricoStatus` | `UC04ChamadosTests.Historico_de_status_registra_a_abertura_e_o_cancelamento` |

### UC06 Agendar Atendimento e UC07 Executar Atendimento

| Requisito | Caso de uso | Endpoint | Classe/método | Teste |
|---|---|---|---|---|
| RF0051 Agendar atendimento | UC06 | `POST /chamados/{id}/agendamentos`, `GET .../agendamentos` | `AgendarAtendimentoHandler`, `Chamado.AdicionarAgendamento` | `UC06AgendamentoTests.Tecnico_propoe_agendamento_e_o_chamado_fica_agendado` |
| RF0052 Confirmar agendamento | UC06 | `POST /agendamentos/{id}/confirmacao` | `ConfirmarAgendamentoHandler`, `Agendamento.Confirmar` | `UC06AgendamentoTests.Cliente_confirma_o_agendamento_proposto` |
| RF0053 Reagendar atendimento | UC06 | `POST /agendamentos/{id}/reagendamento` (cliente ou técnico) | `ReagendarAtendimentoHandler` | `UC06AgendamentoTests.Reagendamento_substitui_o_agendamento_anterior` |
| RF0054 Registrar início | UC07 | `POST /chamados/{id}/atendimento` | `IniciarAtendimentoHandler`, `Chamado.RegistrarInicioDeAtendimento` | `UC07AtendimentoTests.Inicio_do_atendimento_muda_o_status_do_chamado` |
| RF0055 Registrar orçamento | UC07 | `POST /atendimentos/{id}/orcamento`, `GET .../orcamento` | `RegistrarOrcamentoHandler`, `Orcamento` | `UC07AtendimentoTests.Orcamento_registrado_fica_pendente_com_prazo_de_48_horas` |
| RF0056 Aprovar orçamento | UC07 | `POST /orcamentos/{id}/decisao` | `DecidirOrcamentoHandler`, `Orcamento.Aprovar/Recusar` | `UC07AtendimentoTests.Cliente_aprova_o_orcamento` |
| RF0057 Registrar conclusão | UC07, UC11 | `POST /atendimentos/{id}/conclusao` | `ConcluirAtendimentoHandler`, `Atendimento.Concluir` | `UC07AtendimentoTests.Conclusao_encerra_o_chamado_e_abre_a_garantia` |

### UC08 Avaliar Atendimento e UC09 Analisar Histórico

| Requisito | Caso de uso | Endpoint | Classe/método | Teste |
|---|---|---|---|---|
| RF0061 Avaliar atendimento | UC08 | `POST /chamados/{id}/avaliacao` | `RegistrarAvaliacaoHandler`, `Avaliacao` | `UC08AvaliacaoTests.Cliente_avalia_o_atendimento_concluido` |
| RF0062 Consultar avaliações do técnico | UC02, UC08 | `GET /tecnicos/{id}/avaliacoes` | `ConsultarAvaliacoesDoTecnicoHandler` | `UC08AvaliacaoTests.Consulta_lista_as_avaliacoes_do_tecnico` |
| RF0063 Responder avaliação | UC08 | `POST /avaliacoes/{id}/resposta` | `ResponderAvaliacaoHandler`, `Avaliacao.Responder` | `UC08AvaliacaoTests.Administrador_responde_publicamente_a_avaliacao` |
| RF0071 Analisar histórico | UC09 | `GET /analises/chamados` | `AnalisarChamadosHandler.ExecutarAsync` | `UC09AnaliseTests.Analise_agrupa_por_mes_e_devolve_uma_serie_por_categoria` |
| RF0072 Filtrar por técnico | UC09 | `GET /analises/chamados?tecnicoIds=` | `AnalisarChamadosHandler` (filtro e agrupamento) | `UC09AnaliseTests.Analise_agrupada_por_tecnico_usa_o_tecnico_como_serie` |
| RF0073 Selecionar categorias | UC09 | `GET /analises/chamados?categoriaIds=` | `AnalisarChamadosHandler` (filtro de categorias) | `UC09AnaliseTests.Filtro_por_categoria_limita_as_series` |
| RF0074 Exportar análise | UC09 | `GET /analises/chamados/exportacao` | `AnalisarChamadosHandler.ExportarCsvAsync` | `UC09AnaliseTests.Exportacao_devolve_csv_com_o_conteudo_do_grafico` |

### UC10, UC11 e UC12 — Pagamento, faturamento e garantia

| Requisito | Caso de uso | Endpoint | Classe/método | Teste |
|---|---|---|---|---|
| RF0081 Cadastrar forma de pagamento | UC10, UC11 | `POST/GET/PUT/DELETE /clientes/{id}/formas-pagamento`, `PATCH .../principal` | `FormasDePagamentoHandler`, `Cliente.AdicionarFormaPagamento` | `UC10FormasPagamentoTests.Cliente_cadastra_pix_e_boleto`, `.Definir_principal_desmarca_a_anterior`, `RF0081FormasDePagamentoTests` |
| RF0082 Gerar fatura | UC07, UC11 | efeito de `POST /atendimentos/{id}/conclusao` | `ConcluirAtendimentoHandler.GerarFaturaAsync`, `Fatura` | `UC11FaturasTests.Pagamento_aprovado_quita_a_fatura`, `UC12GarantiaTests.Chamado_de_garantia_nao_gera_fatura` |
| RF0083 Consultar faturas | UC11 | `GET /faturas`, `/faturas/minhas`, `/faturas/{id}` | `ConsultarFaturasHandler` | `UC11FaturasTests.Consulta_do_administrador_aceita_filtros_combinaveis`, `.Cliente_consulta_somente_as_proprias_faturas` |
| RF0084 Registrar pagamento | UC11 | `POST /faturas/{id}/pagamentos`, `GET .../pagamentos` | `PagarFaturaHandler`, `Fatura.RegistrarPagamento` | `UC11FaturasTests.Pagamento_aprovado_quita_a_fatura`, `.Fatura_ja_paga_nao_aceita_novo_pagamento` |
| RF0085 Acionar garantia | UC12 | `POST /atendimentos/{id}/garantia/acionamento`, `GET .../garantia` | `AcionarGarantiaHandler`, `Chamado.AbrirParaGarantia` | `UC12GarantiaTests.Acionamento_gera_chamado_de_garantia_vinculado_ao_original`, `JornadaCompletaTests.Do_cadastro_ao_acionamento_da_garantia` |

---

## 2. Regras de negócio

| Requisito | Caso de uso | Endpoint | Classe/método | Teste |
|---|---|---|---|---|
| RN0011 Dados obrigatórios do cliente | UC01 | `POST /clientes` | `Cliente` (construtor), `CadastrarClienteValidator` | `RN0011ClienteTests`, `UC01ClientesTests.Cadastro_sem_imovel_e_recusado` |
| RN0012 Composição do imóvel | UC01 | `POST /clientes`, `POST /clientes/{id}/imoveis` | `Imovel` (construtor), `ImovelValidator` | `RN0012ImovelTests`, `UC01ClientesTests.Cadastro_recusa_imovel_com_cep_invalido` |
| RN0021 Dados obrigatórios do técnico | UC02 | `POST /tecnicos` | `Tecnico` (construtor), `CadastrarTecnicoValidator` | `RN0021TecnicoTests`, `UC02TecnicosTests.Cadastro_sem_especialidade_e_recusado` |
| RN0022 Especialidade na atribuição | UC05, UC12 | `POST/PUT /chamados/{id}/atribuicao` | `Tecnico.AtendeCategoria` via `Chamado.AtribuirTecnico` | `RN0022EspecialidadeTests`, `UC05TriagemTests.Atribuicao_a_tecnico_sem_a_especialidade_e_recusada` |
| RN0023 Área na atribuição | UC05, UC12 | `POST/PUT /chamados/{id}/atribuicao` | `Tecnico.AtendeCep` via `Chamado.AtribuirTecnico` | `RN0023AreaAtendimentoTests`, `UC05TriagemTests.Atribuicao_a_tecnico_fora_da_area_e_recusada` |
| RN0031 Prioridade automática por categoria | UC04, UC05 | `POST /chamados` | `PoliticaUrgencia.Definir` (padrão `Media`, `Alta` com risco) | `RN0031PoliticaUrgenciaTests`, `UC04ChamadosTests.Indicacao_de_risco_em_categoria_de_risco_forca_urgencia_alta` |
| RN0032 Foto obrigatória | UC04 | `POST /chamados` | `AbrirChamadoHandler` (consulta `CategoriaServico.ExigeFoto`) | `RN0032FotoObrigatoriaTests`, `UC04ChamadosTests.Categoria_que_exige_foto_recusa_abertura_sem_anexo` |
| RN0033 Regra de cancelamento | UC04, UC05 | `POST /chamados/{id}/cancelamento` | `Chamado.PodeSerCancelado`, `.Cancelar` | `RN0033CancelamentoTests`, `UC04ChamadosTests.Cliente_nao_cancela_chamado_em_atendimento` |
| RN0034 Fluxo de status | UC04, UC05, UC06, UC07 | todos os que mudam status | `MaquinaDeEstadosDoChamado`, `Chamado.AlterarStatus` | `RN0034MaquinaDeEstadosTests` (teste parametrizado), `UC05TriagemTests.Transicao_de_status_invalida_e_recusada` |
| RN0035 Reabertura de chamado | UC04, UC05 | `POST /chamados/{id}/reabertura` | `Chamado.SolicitarReabertura` | `RN0035ReaberturaTests`, `UC04ChamadosTests.Reabertura_fora_do_prazo_e_recusada` |
| RN0041 Disponibilidade do técnico | UC06 | `POST /chamados/{id}/agendamentos` | `Tecnico.EstaDisponivel`, `TecnicoRepositorio.ObterCompromissosAsync` | `RN0041DisponibilidadeTests`, `UC06AgendamentoTests.Agendamento_sobreposto_para_o_mesmo_tecnico_e_recusado` |
| RN0042 Cancelamento por recusa de orçamento | UC07 | `POST /orcamentos/{id}/decisao` | `DecidirOrcamentoHandler` | `RN0042RecusaDeOrcamentoTests`, `UC07AtendimentoTests.Recusa_do_orcamento_cancela_o_chamado` |
| RN0043 Prazo de 48 h do orçamento | UC07 | `POST /orcamentos/{id}/decisao` | `Orcamento.PrazoAprovacao`, `ExpiracaoOrcamentoJob` | `RN0043PrazoDeOrcamentoTests`, `UC07AtendimentoTests.Conclusao_com_orcamento_pendente_e_recusada` |
| RN0051 Solicitação de avaliação | UC07, UC08 | efeito de `POST /atendimentos/{id}/conclusao` | evento `AtendimentoConcluido` → `INotificador.SolicitarAvaliacaoAsync` | `RN0051SolicitacaoDeAvaliacaoTests`, `JornadaCompletaTests.Cada_mudanca_de_status_gera_notificacao_ao_cliente` |
| RN0052 Prazo de avaliação | UC08 | `POST /chamados/{id}/avaliacao` | `Avaliacao.DentroDoPrazo`, `RegistrarAvaliacaoHandler` | `RN0052PrazoDeAvaliacaoTests`, `UC08AvaliacaoTests.Avaliacao_fora_do_prazo_de_15_dias_e_recusada` |
| RN0061 Granularidade mensal | UC09 | `GET /analises/chamados` | `AnalisarChamadosHandler` (agrupamento por ano/mês) | `UC09AnaliseTests.Analise_agrupa_por_mes_e_devolve_uma_serie_por_categoria` |
| RN0062 Intervalo de 1 a 24 meses | UC09 | `GET /analises/chamados` | `FiltroDeAnaliseValidator` (piso e teto pela distância entre as datas) | `UC09AnaliseTests.Intervalo_maior_que_24_meses_e_recusado`, `.Intervalo_de_24_meses_e_aceito` |
| RN0063 Cancelados fora da análise | UC09 | `GET /analises/chamados` | `AnalisarChamadosHandler` (filtro de status) | `UC09AnaliseTests.Chamado_cancelado_nao_e_contabilizado` |
| RN0071 Composição da fatura | UC11, UC12 | efeito de `POST /atendimentos/{id}/conclusao` | `Fatura.CalcularValorTotal`, `ConcluirAtendimentoHandler.GerarFaturaAsync` | `RN0071ComposicaoDaFaturaTests`, `UC12GarantiaTests.Chamado_de_garantia_nao_gera_fatura` |
| RN0072 Prazo e condições da garantia | UC12 | `POST /atendimentos/{id}/garantia/acionamento` | `Garantia.PodeSerAcionada`, `AcionarGarantiaHandler` | `RN0072GarantiaTests`, `UC12GarantiaTests.Acionamento_fora_do_prazo_de_90_dias_e_recusado`, `.Acionamento_com_fatura_em_aberto_e_recusado` |

---

## 3. Requisitos não funcionais

| Requisito | Caso de uso | Endpoint | Classe/método | Teste |
|---|---|---|---|---|
| RNF0011 Resposta em até 1 s | todos | todas as consultas | `MiddlewareDeTempoDeResposta` (log acima de 1 s), `ContextoDeLeitura` (AsNoTracking), índices da migration | `RNF0011TempoDeRespostaTests` |
| RNF0012 Log de transação | todos | toda escrita | `InterceptorDeAuditoria` → tabela `log_transacao` | `JornadaCompletaTests` (conta as linhas de `log_transacao` geradas pela jornada) |
| RNF0021 Senha forte | UC01 | `POST /clientes`, `POST /auth/alterar-senha` | `PoliticaDeSenhaForte` | `UC01ClientesTests.Cadastro_recusa_senha_fora_da_politica`, `AutenticacaoTests.Alterar_senha_recusa_senha_fraca` |
| RNF0022 Senha criptografada | UC01 | `POST /auth/login` | `ServicoDeHashBCrypt` (work factor 12), `Usuario.Autenticar` | `RNF0022AutenticacaoTests`, `AutenticacaoTests.Nenhuma_resposta_expoe_a_senha_ou_o_hash` |
| RNF0023 Código de cliente | UC01 | `POST /clientes` | `GeradorDeSequencias.ProximoCodigoDeClienteAsync` (`seq_codigo_cliente`) | `UC01ClientesTests.Cadastro_cria_cliente_com_imovel_e_codigo_unico` |
| RNF0031 Código de técnico | UC02 | `POST /tecnicos` | `GeradorDeSequencias.ProximoCodigoDeTecnicoAsync` (`seq_codigo_tecnico`) | `UC02TecnicosTests.Cadastro_cria_tecnico_com_codigo_especialidade_e_area` |
| RNF0032 Anexo de documentação | UC02 | `POST /tecnicos/{id}/documentos` | `Anexo.ParaTecnico` (PDF, PNG, JPEG) | `RNF0032DocumentoDoTecnicoTests`, `UC02TecnicosTests.Documento_em_pdf_e_aceito_e_docx_e_recusado` |
| RNF0041 Notificação de mudança de status | UC04, UC05, UC06, UC07, UC12 | todos que mudam status | evento `StatusDoChamadoAlterado` → `DespachanteDeEventos` → `NotificadorPorLog` (`notificacao_enviada`) | `JornadaCompletaTests.Cada_mudanca_de_status_gera_notificacao_ao_cliente` |
| RNF0042 Número único de chamado | UC04, UC12 | `POST /chamados` | `GeradorDeSequencias.ProximoNumeroDeChamadoAsync` (`seq_numero_chamado`) | `UC04ChamadosTests.Abertura_cria_chamado_aberto_com_numero_sequencial` |
| RNF0043 Limite de anexos | UC04, UC07 | `POST /chamados`, `.../anexos`, `.../conclusao` | `Chamado.AdicionarAnexo` (5 por chamado), `Anexo.TamanhoMaximoEmBytes` | `RNF0043LimiteDeAnexosTests`, `UC04ChamadosTests.Sexto_anexo_do_chamado_e_recusado` |
| RNF0051 Gráfico de linhas | UC09 | `GET /analises/chamados` | `AnaliseDeChamadosDto` (eixo X, séries e legendas prontas) | `UC09AnaliseTests.Analise_agrupa_por_mes_e_devolve_uma_serie_por_categoria` |
| RNF0061 Tokenização do cartão | UC10, UC11 | `POST /clientes/{id}/formas-pagamento`, `POST /faturas/{id}/pagamentos` | `IGatewayPagamento.TokenizarCartaoAsync`, `CartaoCredito` | `UC10FormasPagamentoTests.Cartao_e_tokenizado_e_o_numero_completo_nao_volta_na_resposta`, `.Numero_completo_do_cartao_nao_e_persistido` |

---

## 4. Cobertura de testes

| Suíte | Testes | Resultado |
|---|---|---|
| `ChamadosManutencao.UnitTests` | 248 | verdes |
| `ChamadosManutencao.IntegrationTests` | 156 | verdes |
| **Total** | **404** | **verdes** |

Cobertura da camada `Domain` medida com `coverlet.collector`: **87,2% de linhas e 87,0% de
ramos**, acima do mínimo de 85% exigido pela seção 13 da especificação.

```bash
dotnet test tests/ChamadosManutencao.UnitTests --collect:"XPlat Code Coverage"
```

Os testes de autorização (`AutorizacaoTests`) cobrem `401` sem token e `403` para papel errado
nos endpoints restritos, e `JornadaCompletaTests` percorre o ciclo inteiro: cadastro do cliente,
abertura do chamado, triagem, atribuição, agendamento, confirmação, início do atendimento,
orçamento, aprovação, conclusão, fatura, pagamento, avaliação e acionamento da garantia.
