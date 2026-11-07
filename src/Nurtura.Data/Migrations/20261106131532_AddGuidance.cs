using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nurtura.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuidanceCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidanceCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuidanceContents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GuidanceCategoryId = table.Column<int>(type: "integer", nullable: false),
                    Audience = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    RelatedTopic = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BabyAgeFromMonths = table.Column<int>(type: "integer", nullable: true),
                    BabyAgeToMonths = table.Column<int>(type: "integer", nullable: true),
                    GetHelpHeading = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GetHelpItems = table.Column<List<string>>(type: "text[]", nullable: false),
                    EmergencyHeading = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmergencyItems = table.Column<List<string>>(type: "text[]", nullable: false),
                    Language = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LastChecked = table.Column<DateOnly>(type: "date", nullable: false),
                    NextCheck = table.Column<DateOnly>(type: "date", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidanceContents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuidanceContents_GuidanceCategories_GuidanceCategoryId",
                        column: x => x.GuidanceCategoryId,
                        principalTable: "GuidanceCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentKeywordTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuidanceContentId = table.Column<int>(type: "integer", nullable: false),
                    Keyword = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentKeywordTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentKeywordTags_GuidanceContents_GuidanceContentId",
                        column: x => x.GuidanceContentId,
                        principalTable: "GuidanceContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuidanceSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuidanceContentId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidanceSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuidanceSources_GuidanceContents_GuidanceContentId",
                        column: x => x.GuidanceContentId,
                        principalTable: "GuidanceContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedGuidances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MotherProfileId = table.Column<int>(type: "integer", nullable: false),
                    GuidanceContentId = table.Column<int>(type: "integer", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedGuidances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedGuidances_GuidanceContents_GuidanceContentId",
                        column: x => x.GuidanceContentId,
                        principalTable: "GuidanceContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedGuidances_MotherProfiles_MotherProfileId",
                        column: x => x.MotherProfileId,
                        principalTable: "MotherProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentKeywordTags_GuidanceContentId_Keyword",
                table: "ContentKeywordTags",
                columns: new[] { "GuidanceContentId", "Keyword" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceCategories_Name",
                table: "GuidanceCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceContents_GuidanceCategoryId",
                table: "GuidanceContents",
                column: "GuidanceCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceContents_Slug",
                table: "GuidanceContents",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceSources_GuidanceContentId",
                table: "GuidanceSources",
                column: "GuidanceContentId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedGuidances_GuidanceContentId",
                table: "SavedGuidances",
                column: "GuidanceContentId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedGuidances_MotherProfileId_GuidanceContentId",
                table: "SavedGuidances",
                columns: new[] { "MotherProfileId", "GuidanceContentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentKeywordTags");

            migrationBuilder.DropTable(
                name: "GuidanceSources");

            migrationBuilder.DropTable(
                name: "SavedGuidances");

            migrationBuilder.DropTable(
                name: "GuidanceContents");

            migrationBuilder.DropTable(
                name: "GuidanceCategories");
        }
    }
}
