using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShaloTrack_API.Migrations
{
    /// <inheritdoc />
    public partial class AddFirebaseUidToCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirebaseUid",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_FirebaseUid",
                table: "Customers",
                column: "FirebaseUid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_FirebaseUid",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "FirebaseUid",
                table: "Customers");
        }
    }
}