using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShaloTrack_API.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionReminderLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionReminderLogs",
                columns: table => new
                {
                    ReminderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImeiNumber = table.Column<string>(type: "text", nullable: false),
                    Milestone = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionReminderLogs", x => x.ReminderId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionReminderLogs_ImeiNumber_Milestone_ExpiresAt",
                table: "SubscriptionReminderLogs",
                columns: new[] { "ImeiNumber", "Milestone", "ExpiresAt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionReminderLogs");
        }
    }
}
