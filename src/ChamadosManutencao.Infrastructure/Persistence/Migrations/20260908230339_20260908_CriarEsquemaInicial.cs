using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChamadosManutencao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _20260908_CriarEsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "seq_codigo_cliente");

            migrationBuilder.CreateSequence(
                name: "seq_codigo_tecnico");

            migrationBuilder.CreateSequence(
                name: "seq_numero_chamado");

            migrationBuilder.CreateSequence(
                name: "seq_numero_fatura");

            migrationBuilder.CreateTable(
                name: "atendimento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chamado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tecnico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_hora_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_hora_conclusao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    relato_tecnico = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    garantia_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atendimento", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "avaliacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chamado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tecnico_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nota = table.Column<int>(type: "integer", nullable: false),
                    comentario = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    data_hora_registro = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avaliacao", x => x.id);
                    table.CheckConstraint("ck_avaliacao_nota", "nota BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "categoria_servico",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descricao = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    exige_foto = table.Column<bool>(type: "boolean", nullable: false),
                    categoria_de_risco = table.Column<bool>(type: "boolean", nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categoria_servico", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chamado",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<long>(type: "bigint", nullable: false),
                    data_hora_abertura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    descricao_problema = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    urgencia = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    chamado_de_garantia = table.Column<bool>(type: "boolean", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    imovel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_servico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tecnico_id = table.Column<Guid>(type: "uuid", nullable: true),
                    chamado_original_id = table.Column<Guid>(type: "uuid", nullable: true),
                    garantia_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chamado", x => x.id);
                    table.CheckConstraint("ck_chamado_status", "status BETWEEN 1 AND 6");
                    table.CheckConstraint("ck_chamado_urgencia", "urgencia BETWEEN 1 AND 3");
                });

            migrationBuilder.CreateTable(
                name: "fatura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<long>(type: "bigint", nullable: false),
                    atendimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chamado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_emissao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_vencimento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valor_pecas = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    valor_mao_obra = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    taxa_deslocamento = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    valor_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fatura", x => x.id);
                    table.CheckConstraint("ck_fatura_status", "status BETWEEN 1 AND 4");
                    table.CheckConstraint("ck_fatura_valores", "valor_pecas >= 0 AND valor_mao_obra >= 0 AND taxa_deslocamento >= 0 AND valor_total >= 0");
                });

            migrationBuilder.CreateTable(
                name: "garantia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    atendimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_fim = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    prazo_dias = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_garantia", x => x.id);
                    table.CheckConstraint("ck_garantia_prazo", "prazo_dias > 0 AND data_fim > data_inicio");
                });

            migrationBuilder.CreateTable(
                name: "log_transacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_hora = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entidade = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    chave_primaria = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    operacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado_anterior = table.Column<string>(type: "jsonb", nullable: true),
                    estado_novo = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_log_transacao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notificacao_enviada",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_hora = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    destinatario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    canal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    assunto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    corpo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    referencia_tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    referencia_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificacao_enviada", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_completo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    tipo_usuario = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    matricula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    codigo_cliente = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    codigo_tecnico = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "orcamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    atendimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_hora_registro = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    prazo_aprovacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    data_hora_decisao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orcamento", x => x.id);
                    table.CheckConstraint("ck_orcamento_status", "status BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "fk_orcamento_atendimento_atendimento_id",
                        column: x => x.atendimento_id,
                        principalTable: "atendimento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resposta_avaliacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    avaliacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    data_hora_resposta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    administrador_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resposta_avaliacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_resposta_avaliacao_avaliacao_avaliacao_id",
                        column: x => x.avaliacao_id,
                        principalTable: "avaliacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tipo_servico",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_servico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descricao = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipo_servico", x => x.id);
                    table.ForeignKey(
                        name: "fk_tipo_servico_categoria_servico_categoria_servico_id",
                        column: x => x.categoria_servico_id,
                        principalTable: "categoria_servico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agendamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chamado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_hora_proposta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duracao_em_minutos = table.Column<int>(type: "integer", nullable: false),
                    data_hora_confirmacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    origem_proposta = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agendamento", x => x.id);
                    table.CheckConstraint("ck_agendamento_duracao", "duracao_em_minutos > 0");
                    table.CheckConstraint("ck_agendamento_origem", "origem_proposta BETWEEN 1 AND 3");
                    table.CheckConstraint("ck_agendamento_status", "status BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "fk_agendamento_chamado_chamado_id",
                        column: x => x.chamado_id,
                        principalTable: "chamado",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "historico_status",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chamado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_anterior = table.Column<int>(type: "integer", nullable: true),
                    status_novo = table.Column<int>(type: "integer", nullable: false),
                    data_hora_alteracao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usuario_responsavel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_historico_status", x => x.id);
                    table.CheckConstraint("ck_historico_status_valores", "status_novo BETWEEN 1 AND 6 AND (status_anterior IS NULL OR status_anterior BETWEEN 1 AND 6)");
                    table.ForeignKey(
                        name: "fk_historico_status_chamado_chamado_id",
                        column: x => x.chamado_id,
                        principalTable: "chamado",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "anexo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    data_hora_upload = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    caminho = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    chamado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tecnico_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anexo", x => x.id);
                    table.CheckConstraint("ck_anexo_dono_unico", "(chamado_id IS NOT NULL AND tecnico_id IS NULL) OR (chamado_id IS NULL AND tecnico_id IS NOT NULL)");
                    table.CheckConstraint("ck_anexo_origem", "origem BETWEEN 1 AND 3");
                    table.CheckConstraint("ck_anexo_tamanho", "tamanho_bytes > 0 AND tamanho_bytes <= 10485760");
                    table.ForeignKey(
                        name: "fk_anexo_chamado_chamado_id",
                        column: x => x.chamado_id,
                        principalTable: "chamado",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_anexo_tecnicos_tecnico_id",
                        column: x => x.tecnico_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "area_atendimento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tecnico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bairro = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cep_inicial = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    cep_final = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    taxa_deslocamento = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_area_atendimento", x => x.id);
                    table.CheckConstraint("ck_area_atendimento_faixa_cep", "cep_inicial <= cep_final");
                    table.ForeignKey(
                        name: "fk_area_atendimento_tecnicos_tecnico_id",
                        column: x => x.tecnico_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "forma_pagamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    apelido = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    principal = table.Column<bool>(type: "boolean", nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false),
                    tipo_forma_pagamento = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    nome_sacado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    email_envio = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    bandeira = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ultimos_quatro_digitos = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    token_operadora = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    validade = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    nome_titular = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    chave_pix = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    tipo_chave = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forma_pagamento", x => x.id);
                    table.ForeignKey(
                        name: "fk_forma_pagamento_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "imovel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    apelido = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo_imovel = table.Column<int>(type: "integer", nullable: false),
                    logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    complemento = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    bairro = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    cidade = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    estado = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imovel", x => x.id);
                    table.CheckConstraint("ck_imovel_tipo", "tipo_imovel BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "fk_imovel_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tecnico_especialidade",
                columns: table => new
                {
                    tecnico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    _especialidades_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tecnico_especialidade", x => new { x.tecnico_id, x._especialidades_id });
                    table.ForeignKey(
                        name: "fk_tecnico_especialidade_categoria_servico__especialidades_id",
                        column: x => x._especialidades_id,
                        principalTable: "categoria_servico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tecnico_especialidade_tecnicos_tecnico_id",
                        column: x => x.tecnico_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_orcamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orcamento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    valor_unitario = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_orcamento", x => x.id);
                    table.CheckConstraint("ck_item_orcamento_quantidade", "quantidade > 0");
                    table.CheckConstraint("ck_item_orcamento_tipo", "tipo BETWEEN 1 AND 2");
                    table.CheckConstraint("ck_item_orcamento_valor", "valor_unitario >= 0");
                    table.ForeignKey(
                        name: "fk_item_orcamento_orcamento_orcamento_id",
                        column: x => x.orcamento_id,
                        principalTable: "orcamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pagamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fatura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    forma_pagamento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_hora_pagamento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valor_pago = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    identificador_transacao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pagamento", x => x.id);
                    table.CheckConstraint("ck_pagamento_status", "status BETWEEN 1 AND 4");
                    table.CheckConstraint("ck_pagamento_valor", "valor_pago >= 0");
                    table.ForeignKey(
                        name: "fk_pagamento_fatura_fatura_id",
                        column: x => x.fatura_id,
                        principalTable: "fatura",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pagamento_forma_pagamento_forma_pagamento_id",
                        column: x => x.forma_pagamento_id,
                        principalTable: "forma_pagamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agendamento_chamado_id",
                table: "agendamento",
                column: "chamado_id");

            migrationBuilder.CreateIndex(
                name: "ix_agendamento_data_hora_proposta",
                table: "agendamento",
                column: "data_hora_proposta");

            migrationBuilder.CreateIndex(
                name: "ix_anexo_chamado_id",
                table: "anexo",
                column: "chamado_id");

            migrationBuilder.CreateIndex(
                name: "ix_anexo_tecnico_id",
                table: "anexo",
                column: "tecnico_id");

            migrationBuilder.CreateIndex(
                name: "ix_area_atendimento_faixa_cep",
                table: "area_atendimento",
                columns: new[] { "cep_inicial", "cep_final" });

            migrationBuilder.CreateIndex(
                name: "ix_area_atendimento_tecnico_id",
                table: "area_atendimento",
                column: "tecnico_id");

            migrationBuilder.CreateIndex(
                name: "ix_atendimento_chamado_id",
                table: "atendimento",
                column: "chamado_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_atendimento_tecnico_id",
                table: "atendimento",
                column: "tecnico_id");

            migrationBuilder.CreateIndex(
                name: "ix_avaliacao_chamado_id",
                table: "avaliacao",
                column: "chamado_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_avaliacao_tecnico_id",
                table: "avaliacao",
                column: "tecnico_id");

            migrationBuilder.CreateIndex(
                name: "ix_categoria_servico_nome",
                table: "categoria_servico",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chamado_cliente_id",
                table: "chamado",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_chamado_numero",
                table: "chamado",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chamado_status_data_hora_abertura",
                table: "chamado",
                columns: new[] { "status", "data_hora_abertura" });

            migrationBuilder.CreateIndex(
                name: "ix_chamado_tecnico_id",
                table: "chamado",
                column: "tecnico_id");

            migrationBuilder.CreateIndex(
                name: "ix_chamado_tipo_servico_id",
                table: "chamado",
                column: "tipo_servico_id");

            migrationBuilder.CreateIndex(
                name: "ix_fatura_atendimento_id",
                table: "fatura",
                column: "atendimento_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fatura_chamado_id",
                table: "fatura",
                column: "chamado_id");

            migrationBuilder.CreateIndex(
                name: "ix_fatura_cliente_id",
                table: "fatura",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_fatura_numero",
                table: "fatura",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fatura_status_data_vencimento",
                table: "fatura",
                columns: new[] { "status", "data_vencimento" });

            migrationBuilder.CreateIndex(
                name: "ix_forma_pagamento_cliente_id",
                table: "forma_pagamento",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_garantia_atendimento_id",
                table: "garantia",
                column: "atendimento_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_garantia_data_fim",
                table: "garantia",
                column: "data_fim");

            migrationBuilder.CreateIndex(
                name: "ix_historico_status_chamado_id_data_hora_alteracao",
                table: "historico_status",
                columns: new[] { "chamado_id", "data_hora_alteracao" });

            migrationBuilder.CreateIndex(
                name: "ix_imovel_cep",
                table: "imovel",
                column: "cep");

            migrationBuilder.CreateIndex(
                name: "ix_imovel_cliente_id",
                table: "imovel",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_orcamento_orcamento_id",
                table: "item_orcamento",
                column: "orcamento_id");

            migrationBuilder.CreateIndex(
                name: "ix_log_transacao_data_hora",
                table: "log_transacao",
                column: "data_hora");

            migrationBuilder.CreateIndex(
                name: "ix_log_transacao_entidade_chave",
                table: "log_transacao",
                columns: new[] { "entidade", "chave_primaria" });

            migrationBuilder.CreateIndex(
                name: "ix_notificacao_enviada_destinatario_id",
                table: "notificacao_enviada",
                column: "destinatario_id");

            migrationBuilder.CreateIndex(
                name: "ix_notificacao_enviada_referencia",
                table: "notificacao_enviada",
                columns: new[] { "referencia_tipo", "referencia_id" });

            migrationBuilder.CreateIndex(
                name: "ix_orcamento_atendimento_id",
                table: "orcamento",
                column: "atendimento_id");

            migrationBuilder.CreateIndex(
                name: "ix_orcamento_status_prazo_aprovacao",
                table: "orcamento",
                columns: new[] { "status", "prazo_aprovacao" });

            migrationBuilder.CreateIndex(
                name: "ix_pagamento_fatura_id",
                table: "pagamento",
                column: "fatura_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagamento_forma_pagamento_id",
                table: "pagamento",
                column: "forma_pagamento_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagamento_identificador_transacao",
                table: "pagamento",
                column: "identificador_transacao");

            migrationBuilder.CreateIndex(
                name: "ix_resposta_avaliacao_avaliacao_id",
                table: "resposta_avaliacao",
                column: "avaliacao_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tecnico_especialidade__especialidades_id",
                table: "tecnico_especialidade",
                column: "_especialidades_id");

            migrationBuilder.CreateIndex(
                name: "ix_tipo_servico_categoria_id",
                table: "tipo_servico",
                column: "categoria_servico_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_codigo_cliente",
                table: "usuario",
                column: "codigo_cliente",
                unique: true,
                filter: "tipo_usuario = 'Cliente'");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_codigo_tecnico",
                table: "usuario",
                column: "codigo_tecnico",
                unique: true,
                filter: "tipo_usuario = 'Tecnico'");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_cpf_tecnico",
                table: "usuario",
                column: "cpf",
                unique: true,
                filter: "tipo_usuario = 'Tecnico'");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_email_tecnico",
                table: "usuario",
                column: "email",
                unique: true,
                filter: "tipo_usuario = 'Tecnico'");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_matricula",
                table: "usuario",
                column: "matricula",
                unique: true,
                filter: "tipo_usuario = 'Administrador'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agendamento");

            migrationBuilder.DropTable(
                name: "anexo");

            migrationBuilder.DropTable(
                name: "area_atendimento");

            migrationBuilder.DropTable(
                name: "garantia");

            migrationBuilder.DropTable(
                name: "historico_status");

            migrationBuilder.DropTable(
                name: "imovel");

            migrationBuilder.DropTable(
                name: "item_orcamento");

            migrationBuilder.DropTable(
                name: "log_transacao");

            migrationBuilder.DropTable(
                name: "notificacao_enviada");

            migrationBuilder.DropTable(
                name: "pagamento");

            migrationBuilder.DropTable(
                name: "resposta_avaliacao");

            migrationBuilder.DropTable(
                name: "tecnico_especialidade");

            migrationBuilder.DropTable(
                name: "tipo_servico");

            migrationBuilder.DropTable(
                name: "chamado");

            migrationBuilder.DropTable(
                name: "orcamento");

            migrationBuilder.DropTable(
                name: "fatura");

            migrationBuilder.DropTable(
                name: "forma_pagamento");

            migrationBuilder.DropTable(
                name: "avaliacao");

            migrationBuilder.DropTable(
                name: "categoria_servico");

            migrationBuilder.DropTable(
                name: "atendimento");

            migrationBuilder.DropTable(
                name: "usuario");

            migrationBuilder.DropSequence(
                name: "seq_codigo_cliente");

            migrationBuilder.DropSequence(
                name: "seq_codigo_tecnico");

            migrationBuilder.DropSequence(
                name: "seq_numero_chamado");

            migrationBuilder.DropSequence(
                name: "seq_numero_fatura");
        }
    }
}
