using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nurtura.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF scaffolded a rename here because both columns are text(30); they hold different answers,
            // so the old column is dropped and the new one added instead.
            migrationBuilder.DropColumn(
                name: "PreferredSupportType",
                table: "MotherProfiles");

            migrationBuilder.AddColumn<string>(
                name: "HelpAtHome",
                table: "MotherProfiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFirstBaby",
                table: "MotherProfiles",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BirthType",
                table: "Babies",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BirthWeek",
                table: "Babies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FeedingType",
                table: "Babies",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "MotherSupportTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MotherProfileId = table.Column<int>(type: "integer", nullable: false),
                    SupportType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotherSupportTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MotherSupportTypes_MotherProfiles_MotherProfileId",
                        column: x => x.MotherProfileId,
                        principalTable: "MotherProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MotherSupportTypes_MotherProfileId_SupportType",
                table: "MotherSupportTypes",
                columns: new[] { "MotherProfileId", "SupportType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MotherSupportTypes");

            migrationBuilder.DropColumn(
                name: "IsFirstBaby",
                table: "MotherProfiles");

            migrationBuilder.DropColumn(
                name: "BirthType",
                table: "Babies");

            migrationBuilder.DropColumn(
                name: "BirthWeek",
                table: "Babies");

            migrationBuilder.DropColumn(
                name: "FeedingType",
                table: "Babies");

            migrationBuilder.DropColumn(
                name: "HelpAtHome",
                table: "MotherProfiles");

            migrationBuilder.AddColumn<string>(
                name: "PreferredSupportType",
                table: "MotherProfiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }
    }
}
