using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShaloTrack_API.Migrations
{
    /// <inheritdoc />
    public partial class AddRenewalPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RenewalPackages",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Positioning = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Months = table.Column<int>(type: "integer", nullable: false),
                    CustomerPriceLkr = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    WarrantyMonths = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenewalPackages", x => x.Code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RenewalPackages_Duration",
                table: "RenewalPackages",
                column: "Duration",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RenewalPackages");
        }
    }
}
