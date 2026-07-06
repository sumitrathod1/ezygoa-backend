using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TravelManagement_DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadFunnelAndNewEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_CreatedByUserId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_Userid",
                table: "Bookings");

            migrationBuilder.AddColumn<int>(
                name: "ConvertedBookingId",
                table: "EmailInquiries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeadStatus",
                table: "EmailInquiries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LostReason",
                table: "EmailInquiries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextFollowUpAt",
                table: "EmailInquiries",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuotedAmount",
                table: "EmailInquiries",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "EmailInquiries",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FollowUps",
                columns: table => new
                {
                    FollowUpId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmailInquiryId = table.Column<int>(type: "integer", nullable: true),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: false),
                    FollowUpDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    NextFollowUpAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OrgId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowUps", x => x.FollowUpId);
                    table.ForeignKey(
                        name: "FK_FollowUps_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId");
                    table.ForeignKey(
                        name: "FK_FollowUps_EmailInquiries_EmailInquiryId",
                        column: x => x.EmailInquiryId,
                        principalTable: "EmailInquiries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FollowUps_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "userId");
                });

            migrationBuilder.CreateTable(
                name: "MarketingCampaigns",
                columns: table => new
                {
                    CampaignId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Budget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Spent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LeadsGenerated = table.Column<int>(type: "integer", nullable: false),
                    BookingsConverted = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OrgId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingCampaigns", x => x.CampaignId);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    ReviewId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    CustomerNumber = table.Column<string>(type: "text", nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    TravelDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OrgId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.ReviewId);
                    table.ForeignKey(
                        name: "FK_Reviews_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailInquiries_ConvertedBookingId",
                table: "EmailInquiries",
                column: "ConvertedBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_BookingId",
                table: "FollowUps",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_CreatedByUserId",
                table: "FollowUps",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_EmailInquiryId",
                table: "FollowUps",
                column: "EmailInquiryId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_BookingId",
                table: "Reviews",
                column: "BookingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_CreatedByUserId",
                table: "Bookings",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "userId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_Userid",
                table: "Bookings",
                column: "Userid",
                principalTable: "Users",
                principalColumn: "userId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailInquiries_Bookings_ConvertedBookingId",
                table: "EmailInquiries",
                column: "ConvertedBookingId",
                principalTable: "Bookings",
                principalColumn: "BookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_CreatedByUserId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_Userid",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailInquiries_Bookings_ConvertedBookingId",
                table: "EmailInquiries");

            migrationBuilder.DropTable(
                name: "FollowUps");

            migrationBuilder.DropTable(
                name: "MarketingCampaigns");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_EmailInquiries_ConvertedBookingId",
                table: "EmailInquiries");

            migrationBuilder.DropColumn(
                name: "ConvertedBookingId",
                table: "EmailInquiries");

            migrationBuilder.DropColumn(
                name: "LeadStatus",
                table: "EmailInquiries");

            migrationBuilder.DropColumn(
                name: "LostReason",
                table: "EmailInquiries");

            migrationBuilder.DropColumn(
                name: "NextFollowUpAt",
                table: "EmailInquiries");

            migrationBuilder.DropColumn(
                name: "QuotedAmount",
                table: "EmailInquiries");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "EmailInquiries");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_CreatedByUserId",
                table: "Bookings",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "userId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_Userid",
                table: "Bookings",
                column: "Userid",
                principalTable: "Users",
                principalColumn: "userId");
        }
    }
}
