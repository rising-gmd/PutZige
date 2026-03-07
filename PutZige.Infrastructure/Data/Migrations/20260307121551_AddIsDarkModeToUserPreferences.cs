using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PutZige.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDarkModeToUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Preferences",
                table: "UserSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{\"TimeZoneId\":\"UTC\",\"Theme\":\"system\",\"Language\":\"en\",\"IsDarkMode\":false}",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "{\"TimeZoneId\":\"UTC\",\"Theme\":\"system\",\"Language\":\"en\"}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Preferences",
                table: "UserSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{\"TimeZoneId\":\"UTC\",\"Theme\":\"system\",\"Language\":\"en\"}",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "{\"TimeZoneId\":\"UTC\",\"Theme\":\"system\",\"Language\":\"en\",\"IsDarkMode\":false}");
        }
    }
}
