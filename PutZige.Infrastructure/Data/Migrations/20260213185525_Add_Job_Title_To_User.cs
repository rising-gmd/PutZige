using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PutZige.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_Job_Title_To_User : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "Users");
        }
    }
}
