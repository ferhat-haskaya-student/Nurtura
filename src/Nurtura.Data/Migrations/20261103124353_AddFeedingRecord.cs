using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nurtura.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedingRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedingRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BabyId = table.Column<int>(type: "integer", nullable: false),
                    Time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BreastSide = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Minutes = table.Column<int>(type: "integer", nullable: true),
                    AmountMl = table.Column<int>(type: "integer", nullable: true),
                    BottleContent = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedingRecords", x => x.Id);
                    table.CheckConstraint("CK_FeedingRecords_AmountMl", "\"AmountMl\" IS NULL OR \"AmountMl\" BETWEEN 1 AND 500");
                    table.CheckConstraint("CK_FeedingRecords_Minutes", "\"Minutes\" IS NULL OR \"Minutes\" BETWEEN 1 AND 120");
                    table.CheckConstraint("CK_FeedingRecords_TypeFields", "(\"Type\" = 'Breast' AND \"BreastSide\" IS NOT NULL AND \"AmountMl\" IS NULL AND \"BottleContent\" IS NULL)\nOR (\"Type\" = 'Bottle' AND \"BottleContent\" IS NOT NULL AND \"BreastSide\" IS NULL AND \"Minutes\" IS NULL)\nOR (\"Type\" = 'Solid' AND \"BreastSide\" IS NULL AND \"Minutes\" IS NULL AND \"AmountMl\" IS NULL AND \"BottleContent\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_FeedingRecords_Babies_BabyId",
                        column: x => x.BabyId,
                        principalTable: "Babies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedingRecords_BabyId_Time",
                table: "FeedingRecords",
                columns: new[] { "BabyId", "Time" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedingRecords");
        }
    }
}
