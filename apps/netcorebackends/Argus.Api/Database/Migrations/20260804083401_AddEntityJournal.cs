using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argus.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntityJournal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChangeSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallationId = table.Column<int>(type: "int", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Field = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OldValueId = table.Column<int>(type: "int", nullable: true),
                    NewValueId = table.Column<int>(type: "int", nullable: true),
                    ChangedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ChangedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityJournal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntityJournal_ApplicationInstallations_InstallationId",
                        column: x => x.InstallationId,
                        principalTable: "ApplicationInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntityJournal_Installation",
                table: "EntityJournal",
                columns: new[] { "InstallationId", "ChangedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntityJournal");
        }
    }
}
