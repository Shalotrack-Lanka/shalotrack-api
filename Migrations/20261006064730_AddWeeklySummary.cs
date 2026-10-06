using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShaloTrack_API.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklySummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "LastWeeklySummaryFor",
                table: "Customers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WeeklySummaryEnabled",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastWeeklySummaryFor",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "WeeklySummaryEnabled",
                table: "Customers");
        }
    }
}
