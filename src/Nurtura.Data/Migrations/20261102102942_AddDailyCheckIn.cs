using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Nurtura.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyCheckIns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MotherProfileId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Mood = table.Column<int>(type: "integer", nullable: false),
                    SleepQuality = table.Column<int>(type: "integer", nullable: false),
                    Energy = table.Column<int>(type: "integer", nullable: false),
                    Stress = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCheckIns", x => x.Id);
                    table.CheckConstraint("CK_DailyCheckIns_Energy", "\"Energy\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_DailyCheckIns_Mood", "\"Mood\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_DailyCheckIns_SleepQuality", "\"SleepQuality\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_DailyCheckIns_Stress", "\"Stress\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_DailyCheckIns_MotherProfiles_MotherProfileId",
                        column: x => x.MotherProfileId,
                        principalTable: "MotherProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeelingTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeelingTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CheckInFeelingTags",
                columns: table => new
                {
                    DailyCheckInId = table.Column<int>(type: "integer", nullable: false),
                    FeelingTagId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInFeelingTags", x => new { x.DailyCheckInId, x.FeelingTagId });
                    table.ForeignKey(
                        name: "FK_CheckInFeelingTags_DailyCheckIns_DailyCheckInId",
                        column: x => x.DailyCheckInId,
                        principalTable: "DailyCheckIns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CheckInFeelingTags_FeelingTags_FeelingTagId",
                        column: x => x.FeelingTagId,
                        principalTable: "FeelingTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "FeelingTags",
                columns: new[] { "Id", "Key" },
                values: new object[,]
                {
                    { 1, "Tired" },
                    { 2, "Anxious" },
                    { 3, "Overwhelmed" },
                    { 4, "Sad" },
                    { 5, "Lonely" },
                    { 6, "Calm" },
                    { 7, "Happy" },
                    { 8, "Supported" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckInFeelingTags_FeelingTagId",
                table: "CheckInFeelingTags",
                column: "FeelingTagId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCheckIns_MotherProfileId_Date",
                table: "DailyCheckIns",
                columns: new[] { "MotherProfileId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeelingTags_Key",
                table: "FeelingTags",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckInFeelingTags");

            migrationBuilder.DropTable(
                name: "DailyCheckIns");

            migrationBuilder.DropTable(
                name: "FeelingTags");
        }
    }
}
