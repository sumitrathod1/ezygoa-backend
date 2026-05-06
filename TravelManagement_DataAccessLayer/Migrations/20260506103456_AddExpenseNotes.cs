using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelManagement_DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "vehicleExpences",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "vehicleExpences");
        }
    }
}
