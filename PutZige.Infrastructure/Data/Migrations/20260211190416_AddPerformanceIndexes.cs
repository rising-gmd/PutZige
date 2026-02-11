using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PutZige.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_ReceiverId_SentAt",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_SenderId_ReceiverId_SentAt",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Active",
                table: "Users",
                columns: new[] { "IsActive", "IsDeleted" },
                filter: "[IsActive] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmailVerificationToken",
                table: "Users",
                column: "EmailVerificationToken",
                filter: "[EmailVerificationToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PasswordResetToken",
                table: "Users",
                column: "PasswordResetToken",
                filter: "[PasswordResetToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Search",
                table: "Users",
                columns: new[] { "Username", "DisplayName" },
                filter: "[IsActive] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Conversation",
                table: "Messages",
                columns: new[] { "SenderId", "ReceiverId", "SentAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Conversation_Reverse",
                table: "Messages",
                columns: new[] { "ReceiverId", "SenderId", "SentAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_DeliveryStatus",
                table: "Messages",
                columns: new[] { "ReceiverId", "DeliveredAt" },
                filter: "[DeliveredAt] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReceiverId_SentAt",
                table: "Messages",
                columns: new[] { "ReceiverId", "SentAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Unread",
                table: "Messages",
                columns: new[] { "ReceiverId", "SenderId" },
                filter: "[ReadAt] IS NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Active",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_EmailVerificationToken",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PasswordResetToken",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Search",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Messages_Conversation",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_Conversation_Reverse",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_DeliveryStatus",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ReceiverId_SentAt",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_Unread",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReceiverId_SentAt",
                table: "Messages",
                columns: new[] { "ReceiverId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId_ReceiverId_SentAt",
                table: "Messages",
                columns: new[] { "SenderId", "ReceiverId", "SentAt" });
        }
    }
}
