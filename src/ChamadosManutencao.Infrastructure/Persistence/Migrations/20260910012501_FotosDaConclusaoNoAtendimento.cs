using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChamadosManutencao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FotosDaConclusaoNoAtendimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_anexo_dono_unico",
                table: "anexo");

            migrationBuilder.AddColumn<Guid>(
                name: "atendimento_id",
                table: "anexo",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_anexo_atendimento_id",
                table: "anexo",
                column: "atendimento_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_anexo_dono_unico",
                table: "anexo",
                sql: "num_nonnulls(chamado_id, atendimento_id, tecnico_id) = 1");

            migrationBuilder.AddForeignKey(
                name: "fk_anexo_atendimento_atendimento_id",
                table: "anexo",
                column: "atendimento_id",
                principalTable: "atendimento",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_anexo_atendimento_atendimento_id",
                table: "anexo");

            migrationBuilder.DropIndex(
                name: "ix_anexo_atendimento_id",
                table: "anexo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_anexo_dono_unico",
                table: "anexo");

            migrationBuilder.DropColumn(
                name: "atendimento_id",
                table: "anexo");

            migrationBuilder.AddCheckConstraint(
                name: "ck_anexo_dono_unico",
                table: "anexo",
                sql: "(chamado_id IS NOT NULL AND tecnico_id IS NULL) OR (chamado_id IS NULL AND tecnico_id IS NOT NULL)");
        }
    }
}
