using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChamadosManutencao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PermitirVariosAtendimentosPorChamado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_atendimento_chamado_id",
                table: "atendimento");

            migrationBuilder.CreateIndex(
                name: "ix_atendimento_chamado_id",
                table: "atendimento",
                column: "chamado_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_atendimento_chamado_id",
                table: "atendimento");

            migrationBuilder.CreateIndex(
                name: "ix_atendimento_chamado_id",
                table: "atendimento",
                column: "chamado_id",
                unique: true);
        }
    }
}
