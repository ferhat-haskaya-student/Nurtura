using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nurtura.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelledBy",
                table: "Appointments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfessionalMessage",
                table: "Appointments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ProfessionalMessage",
                table: "Appointments");
        }
    }
}
