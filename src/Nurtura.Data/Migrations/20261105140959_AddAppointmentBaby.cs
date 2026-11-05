using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nurtura.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentBaby : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BabyId",
                table: "Appointments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_BabyId",
                table: "Appointments",
                column: "BabyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Babies_BabyId",
                table: "Appointments",
                column: "BabyId",
                principalTable: "Babies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Babies_BabyId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_BabyId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "BabyId",
                table: "Appointments");
        }
    }
}
