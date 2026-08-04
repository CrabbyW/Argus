using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argus.Api.Database.Migrations
{
    /// <summary>
    /// Turns the hardcoded RepositoryType enum into the RepositoryTypes lookup.
    ///
    /// Hand-ordered, not as scaffolded: EF put <c>DropColumn</c> first, which would have thrown
    /// away every repository's type before anything could read it. The order below creates the
    /// table, fills it, copies the values across, and only then drops the old column.
    ///
    /// The five rows are inserted with explicit Ids matching the old enum members
    /// (Git = 1 ... Tfs = 5), which is what makes the copy a plain column assignment. The old
    /// <c>Unknown = 0</c> has no row: it becomes NULL, the same "not recorded" convention the
    /// nullable lookup columns on ApplicationInstallation already use.
    /// </summary>
    public partial class AddRepositoryTypesLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepositoryTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RepositoryTypeName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_RepositoryTypes_RepositoryTypeName",
                table: "RepositoryTypes",
                column: "RepositoryTypeName",
                unique: true,
                filter: "[IsEnabled] = 1");

            // Seeded here rather than only in DbSeeder, so a database that already exists gets the
            // rows its own data is about to point at.
            migrationBuilder.InsertData(
                table: "RepositoryTypes",
                columns: new[] { "Id", "RepositoryTypeName" },
                values: new object[,]
                {
                    { 1, "Git" },
                    { 2, "Svn" },
                    { 3, "Bitbucket" },
                    { 4, "Mercurial" },
                    { 5, "Tfs" }
                });

            migrationBuilder.AddColumn<int>(
                name: "RepositoryTypeId",
                table: "AppRepositories",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [AppRepositories] SET [RepositoryTypeId] = NULLIF([RepositoryType], 0);");

            migrationBuilder.DropColumn(
                name: "RepositoryType",
                table: "AppRepositories");

            migrationBuilder.CreateIndex(
                name: "IX_AppRepositories_RepositoryTypeId",
                table: "AppRepositories",
                column: "RepositoryTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppRepositories_RepositoryTypes_RepositoryTypeId",
                table: "AppRepositories",
                column: "RepositoryTypeId",
                principalTable: "RepositoryTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mirror image: put the numbers back before the column that holds them disappears.
            // A type added after this migration ran has no enum member to return to, so it lands
            // on 0 (Unknown) — the honest answer, and the reason going down is lossy.
            migrationBuilder.AddColumn<int>(
                name: "RepositoryType",
                table: "AppRepositories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE [AppRepositories] SET [RepositoryType] = " +
                "CASE WHEN [RepositoryTypeId] BETWEEN 1 AND 5 THEN [RepositoryTypeId] ELSE 0 END;");

            migrationBuilder.DropForeignKey(
                name: "FK_AppRepositories_RepositoryTypes_RepositoryTypeId",
                table: "AppRepositories");

            migrationBuilder.DropIndex(
                name: "IX_AppRepositories_RepositoryTypeId",
                table: "AppRepositories");

            migrationBuilder.DropColumn(
                name: "RepositoryTypeId",
                table: "AppRepositories");

            migrationBuilder.DropTable(
                name: "RepositoryTypes");
        }
    }
}
