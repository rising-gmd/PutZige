using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PutZige.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferencesValueObject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "Theme",
                table: "UserSettings");

            migrationBuilder.AlterColumn<string>(
                name: "Preferences",
                table: "UserSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{\"TimeZoneId\":\"UTC\",\"Theme\":\"system\",\"Language\":\"en\"}",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 5000,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Preferences",
                table: "UserSettings",
                type: "nvarchar(max)",
                maxLength: 5000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "{\"TimeZoneId\":\"UTC\",\"Theme\":\"system\",\"Language\":\"en\"}");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "UserSettings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Theme",
                table: "UserSettings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
