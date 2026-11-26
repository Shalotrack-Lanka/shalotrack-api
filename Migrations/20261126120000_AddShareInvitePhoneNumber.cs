using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShaloTrack_API.Migrations
{
    /// <summary>
    /// Vehicle share invites are addressed to a phone number instead of an account, so the invite form no longer
    /// reveals whether a number has a ShaloTrack account.
    ///
    /// Additive and non-destructive:
    /// - VehicleShares.InvitedPhoneNumber (varchar 20, NOT NULL DEFAULT ''), then filled for every existing row
    ///   from the invited customer's current number, as +94XXXXXXXXX. No row is deleted or re-pointed.
    /// - VehicleShares.SharedWithCustomerId becomes nullable (set when an invite is accepted). Dropping NOT NULL
    ///   is a metadata change on PostgreSQL: no rewrite, no long lock.
    /// - An index on InvitedPhoneNumber for the recipient's "invites for you" lookup.
    /// </summary>
    public partial class AddShareInvitePhoneNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SharedWithCustomerId",
                table: "VehicleShares",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "InvitedPhoneNumber",
                table: "VehicleShares",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Existing rows: the number of the customer they were sent to. Sri Lankan mobiles become +947XXXXXXXX
            // (the same form PhoneNumberNormalizer produces); anything else keeps its digits with a leading +.
            migrationBuilder.Sql(@"
UPDATE ""VehicleShares"" AS s
SET ""InvitedPhoneNumber"" = CASE
        WHEN length(d.digits) >= 9 AND substr(right(d.digits, 9), 1, 1) = '7' THEN '+94' || right(d.digits, 9)
        ELSE left('+' || d.digits, 20)
    END
FROM (
    SELECT c.""CustomerId"", regexp_replace(c.""PhoneNumber"", '\D', '', 'g') AS digits
    FROM ""Customers"" AS c
) AS d
WHERE d.""CustomerId"" = s.""SharedWithCustomerId""
  AND s.""InvitedPhoneNumber"" = '';
");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleShares_InvitedPhoneNumber",
                table: "VehicleShares",
                column: "InvitedPhoneNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VehicleShares_InvitedPhoneNumber",
                table: "VehicleShares");

            migrationBuilder.DropColumn(
                name: "InvitedPhoneNumber",
                table: "VehicleShares");

            // Restoring NOT NULL fails, on purpose, while any invite is still waiting for an account
            // (SharedWithCustomerId is null). Nothing is deleted to make it pass: decide what to do with those
            // rows first.
            migrationBuilder.AlterColumn<Guid>(
                name: "SharedWithCustomerId",
                table: "VehicleShares",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
