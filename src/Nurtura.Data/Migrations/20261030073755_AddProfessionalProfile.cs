using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nurtura.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessionalProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfessionalProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PhotoUrl = table.Column<string>(type: "text", nullable: true),
                    ProfessionalType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Bio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessionalProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfessionalCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfessionalProfileId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessionalCategories_ProfessionalProfiles_ProfessionalPro~",
                        column: x => x.ProfessionalProfileId,
                        principalTable: "ProfessionalProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfessionalLanguages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfessionalProfileId = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalLanguages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessionalLanguages_ProfessionalProfiles_ProfessionalProf~",
                        column: x => x.ProfessionalProfileId,
                        principalTable: "ProfessionalProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCategories_ProfessionalProfileId_Category",
                table: "ProfessionalCategories",
                columns: new[] { "ProfessionalProfileId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalLanguages_ProfessionalProfileId_Language",
                table: "ProfessionalLanguages",
                columns: new[] { "ProfessionalProfileId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalProfiles_UserId",
                table: "ProfessionalProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfessionalCategories");

            migrationBuilder.DropTable(
                name: "ProfessionalLanguages");

            migrationBuilder.DropTable(
                name: "ProfessionalProfiles");
        }
    }
}
