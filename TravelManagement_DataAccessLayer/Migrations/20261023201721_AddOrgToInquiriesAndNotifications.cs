using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelManagement_DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgToInquiriesAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrgId",
                table: "Notifications",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "OrgId",
                table: "EmailInquiries",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OrgId_IsRead",
                table: "Notifications",
                columns: new[] { "OrgId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailInquiries_OrgId_IsConfirmed_IsRejected",
                table: "EmailInquiries",
                columns: new[] { "OrgId", "IsConfirmed", "IsRejected" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_OrgId_IsRead",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_EmailInquiries_OrgId_IsConfirmed_IsRejected",
                table: "EmailInquiries");

            migrationBuilder.DropColumn(
                name: "OrgId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "OrgId",
                table: "EmailInquiries");
        }
    }
}
