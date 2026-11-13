using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nurtura.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationForumCommentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ForumCommentId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ForumCommentId",
                table: "Notifications",
                column: "ForumCommentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_ForumComments_ForumCommentId",
                table: "Notifications",
                column: "ForumCommentId",
                principalTable: "ForumComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_ForumComments_ForumCommentId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_ForumCommentId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ForumCommentId",
                table: "Notifications");
        }
    }
}
