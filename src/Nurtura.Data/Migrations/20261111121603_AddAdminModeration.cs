using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nurtura.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ProfessionalProfiles",
                type: "boolean",
                nullable: false,
                // Existing professionals stay active.
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenByModeratorAt",
                table: "ForumPosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedByModeratorAt",
                table: "ForumPosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenByModeratorAt",
                table: "ForumComments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ProfessionalProfiles");

            migrationBuilder.DropColumn(
                name: "HiddenByModeratorAt",
                table: "ForumPosts");

            migrationBuilder.DropColumn(
                name: "RemovedByModeratorAt",
                table: "ForumPosts");

            migrationBuilder.DropColumn(
                name: "HiddenByModeratorAt",
                table: "ForumComments");
        }
    }
}
