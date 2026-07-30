using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argus.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class FilterDeploymentUniqueIndexToLiveRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Installations_Deployment",
                table: "Installations");

            migrationBuilder.CreateIndex(
                name: "UX_Installations_Deployment",
                table: "Installations",
                columns: new[] { "MachineId", "ApplicationId", "AppStageId", "RootPath" },
                unique: true,
                filter: "[IsEnabled] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Installations_Deployment",
                table: "Installations");

            migrationBuilder.CreateIndex(
                name: "UX_Installations_Deployment",
                table: "Installations",
                columns: new[] { "MachineId", "ApplicationId", "AppStageId", "RootPath" },
                unique: true);
        }
    }
}
