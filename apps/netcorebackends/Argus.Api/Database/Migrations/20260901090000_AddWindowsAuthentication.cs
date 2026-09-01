using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argus.Api.Database.Migrations
{
    /// <summary>
    /// Windows sign-in: the domain account a user may sign in with, how they last signed in, and
    /// the password columns turning nullable — a Windows-only account has no password to store.
    /// </summary>
    /// <inheritdoc />
    public partial class AddWindowsAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PasswordSalt",
                table: "ApplicationUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "ApplicationUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<string>(
                name: "WindowsAccountName",
                table: "ApplicationUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastLoginMethod",
                table: "ApplicationUsers",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            // Filtered, because SQL Server's unique index would otherwise treat every unmapped
            // user's NULL as the same value and allow only one of them.
            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_WindowsAccountName",
                table: "ApplicationUsers",
                column: "WindowsAccountName",
                unique: true,
                filter: "[WindowsAccountName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationUsers_WindowsAccountName",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "WindowsAccountName",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "LastLoginMethod",
                table: "ApplicationUsers");

            // Reverting the columns to NOT NULL cannot work while a Windows-only user exists, so
            // those rows go first: without a password they could not sign in on the old schema
            // anyway, and leaving them would only fail the migration halfway through.
            migrationBuilder.Sql("DELETE FROM [ApplicationUsers] WHERE [PasswordHash] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordSalt",
                table: "ApplicationUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "ApplicationUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }
    }
}
