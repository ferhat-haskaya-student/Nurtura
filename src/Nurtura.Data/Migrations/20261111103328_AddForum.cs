using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Nurtura.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddForum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Hour",
                table: "Notifications",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "Date",
                table: "Notifications",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<int>(
                name: "AppointmentId",
                table: "Notifications",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ForumPostId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostTitle",
                table: "Notifications",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ForumCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ForumPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuthorProfileId = table.Column<int>(type: "integer", nullable: true),
                    ForumCategoryId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByAuthorAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForumPosts_ForumCategories_ForumCategoryId",
                        column: x => x.ForumCategoryId,
                        principalTable: "ForumCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForumPosts_MotherProfiles_AuthorProfileId",
                        column: x => x.AuthorProfileId,
                        principalTable: "MotherProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ForumComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ForumPostId = table.Column<int>(type: "integer", nullable: false),
                    AuthorProfileId = table.Column<int>(type: "integer", nullable: true),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForumComments_ForumPosts_ForumPostId",
                        column: x => x.ForumPostId,
                        principalTable: "ForumPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ForumComments_MotherProfiles_AuthorProfileId",
                        column: x => x.AuthorProfileId,
                        principalTable: "MotherProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HiddenPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MotherProfileId = table.Column<int>(type: "integer", nullable: false),
                    ForumPostId = table.Column<int>(type: "integer", nullable: false),
                    HiddenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiddenPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HiddenPosts_ForumPosts_ForumPostId",
                        column: x => x.ForumPostId,
                        principalTable: "ForumPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HiddenPosts_MotherProfiles_MotherProfileId",
                        column: x => x.MotherProfileId,
                        principalTable: "MotherProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReporterProfileId = table.Column<int>(type: "integer", nullable: false),
                    ForumPostId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostReports_ForumPosts_ForumPostId",
                        column: x => x.ForumPostId,
                        principalTable: "ForumPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostReports_MotherProfiles_ReporterProfileId",
                        column: x => x.ReporterProfileId,
                        principalTable: "MotherProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MotherProfileId = table.Column<int>(type: "integer", nullable: false),
                    ForumPostId = table.Column<int>(type: "integer", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedPosts_ForumPosts_ForumPostId",
                        column: x => x.ForumPostId,
                        principalTable: "ForumPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedPosts_MotherProfiles_MotherProfileId",
                        column: x => x.MotherProfileId,
                        principalTable: "MotherProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommentReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReporterProfileId = table.Column<int>(type: "integer", nullable: false),
                    ForumCommentId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommentReports_ForumComments_ForumCommentId",
                        column: x => x.ForumCommentId,
                        principalTable: "ForumComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommentReports_MotherProfiles_ReporterProfileId",
                        column: x => x.ReporterProfileId,
                        principalTable: "MotherProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ForumCategories",
                columns: new[] { "Id", "Key" },
                values: new object[,]
                {
                    { 1, "EmotionalWellbeing" },
                    { 2, "SleepAndTiredness" },
                    { 3, "BabyFeeding" },
                    { 4, "BabySleep" },
                    { 5, "Nutrition" },
                    { 6, "RecoveryAfterBirth" },
                    { 7, "GeneralSupport" },
                    { 8, "ConsultationExperiences" },
                    { 9, "Other" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ForumPostId",
                table: "Notifications",
                column: "ForumPostId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentReports_ForumCommentId",
                table: "CommentReports",
                column: "ForumCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentReports_ReporterProfileId_ForumCommentId",
                table: "CommentReports",
                columns: new[] { "ReporterProfileId", "ForumCommentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentReports_Status_CreatedAt",
                table: "CommentReports",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ForumCategories_Key",
                table: "ForumCategories",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumComments_AuthorProfileId",
                table: "ForumComments",
                column: "AuthorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumComments_ForumPostId_CreatedAt",
                table: "ForumComments",
                columns: new[] { "ForumPostId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ForumPosts_AuthorProfileId",
                table: "ForumPosts",
                column: "AuthorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumPosts_CreatedAt",
                table: "ForumPosts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ForumPosts_ForumCategoryId_CreatedAt",
                table: "ForumPosts",
                columns: new[] { "ForumCategoryId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HiddenPosts_ForumPostId",
                table: "HiddenPosts",
                column: "ForumPostId");

            migrationBuilder.CreateIndex(
                name: "IX_HiddenPosts_MotherProfileId_ForumPostId",
                table: "HiddenPosts",
                columns: new[] { "MotherProfileId", "ForumPostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostReports_ForumPostId",
                table: "PostReports",
                column: "ForumPostId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReports_ReporterProfileId_ForumPostId",
                table: "PostReports",
                columns: new[] { "ReporterProfileId", "ForumPostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostReports_Status_CreatedAt",
                table: "PostReports",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedPosts_ForumPostId",
                table: "SavedPosts",
                column: "ForumPostId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedPosts_MotherProfileId_ForumPostId",
                table: "SavedPosts",
                columns: new[] { "MotherProfileId", "ForumPostId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_ForumPosts_ForumPostId",
                table: "Notifications",
                column: "ForumPostId",
                principalTable: "ForumPosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_ForumPosts_ForumPostId",
                table: "Notifications");

            migrationBuilder.DropTable(
                name: "CommentReports");

            migrationBuilder.DropTable(
                name: "HiddenPosts");

            migrationBuilder.DropTable(
                name: "PostReports");

            migrationBuilder.DropTable(
                name: "SavedPosts");

            migrationBuilder.DropTable(
                name: "ForumComments");

            migrationBuilder.DropTable(
                name: "ForumPosts");

            migrationBuilder.DropTable(
                name: "ForumCategories");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_ForumPostId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ForumPostId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "PostTitle",
                table: "Notifications");

            migrationBuilder.AlterColumn<int>(
                name: "Hour",
                table: "Notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "Date",
                table: "Notifications",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AppointmentId",
                table: "Notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
