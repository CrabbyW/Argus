using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argus.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordSalt = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppNames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppNames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppRepositories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RepositoryUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RepositoryType = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRepositories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStageNames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StageName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStageNames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DnsEndpoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DnsName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsLoadBalancer = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DnsEndpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalPaths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Path = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalPaths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessorArchitectures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArchitectureName = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessorArchitectures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RootPaths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Path = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RootPaths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TagName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationInstallations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineId = table.Column<int>(type: "int", nullable: false),
                    AppNameId = table.Column<int>(type: "int", nullable: false),
                    AppStageNameId = table.Column<int>(type: "int", nullable: false),
                    ProcessorArchitectureId = table.Column<int>(type: "int", nullable: false),
                    DnsEndpointId = table.Column<int>(type: "int", nullable: true),
                    RootPathId = table.Column<int>(type: "int", nullable: false),
                    PhysicalPathId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ValidFromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationInstallations_AppNames_AppNameId",
                        column: x => x.AppNameId,
                        principalTable: "AppNames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationInstallations_AppStageNames_AppStageNameId",
                        column: x => x.AppStageNameId,
                        principalTable: "AppStageNames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationInstallations_DnsEndpoints_DnsEndpointId",
                        column: x => x.DnsEndpointId,
                        principalTable: "DnsEndpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ApplicationInstallations_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationInstallations_PhysicalPaths_PhysicalPathId",
                        column: x => x.PhysicalPathId,
                        principalTable: "PhysicalPaths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ApplicationInstallations_ProcessorArchitectures_ProcessorArchitectureId",
                        column: x => x.ProcessorArchitectureId,
                        principalTable: "ProcessorArchitectures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationInstallations_RootPaths_RootPathId",
                        column: x => x.RootPathId,
                        principalTable: "RootPaths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstallationRepositories",
                columns: table => new
                {
                    InstallationId = table.Column<int>(type: "int", nullable: false),
                    AppRepositoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallationRepositories", x => new { x.InstallationId, x.AppRepositoryId });
                    table.ForeignKey(
                        name: "FK_InstallationRepositories_AppRepositories_AppRepositoryId",
                        column: x => x.AppRepositoryId,
                        principalTable: "AppRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstallationRepositories_ApplicationInstallations_InstallationId",
                        column: x => x.InstallationId,
                        principalTable: "ApplicationInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstallationTags",
                columns: table => new
                {
                    InstallationId = table.Column<int>(type: "int", nullable: false),
                    TagId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallationTags", x => new { x.InstallationId, x.TagId });
                    table.ForeignKey(
                        name: "FK_InstallationTags_ApplicationInstallations_InstallationId",
                        column: x => x.InstallationId,
                        principalTable: "ApplicationInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstallationTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationInstallations_AppNameId",
                table: "ApplicationInstallations",
                column: "AppNameId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationInstallations_AppStageNameId",
                table: "ApplicationInstallations",
                column: "AppStageNameId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationInstallations_DnsEndpointId",
                table: "ApplicationInstallations",
                column: "DnsEndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationInstallations_MachineId",
                table: "ApplicationInstallations",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationInstallations_PhysicalPathId",
                table: "ApplicationInstallations",
                column: "PhysicalPathId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationInstallations_ProcessorArchitectureId",
                table: "ApplicationInstallations",
                column: "ProcessorArchitectureId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationInstallations_RootPathId",
                table: "ApplicationInstallations",
                column: "RootPathId");

            migrationBuilder.CreateIndex(
                name: "UX_ApplicationInstallations_Deployment",
                table: "ApplicationInstallations",
                columns: new[] { "MachineId", "AppNameId", "AppStageNameId", "RootPathId" },
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_Username",
                table: "ApplicationUsers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AppNames_AppName",
                table: "AppNames",
                column: "AppName",
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_AppRepositories_RepositoryUrl",
                table: "AppRepositories",
                column: "RepositoryUrl",
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_AppStageNames_StageName",
                table: "AppStageNames",
                column: "StageName",
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_DnsEndpoints_DnsName",
                table: "DnsEndpoints",
                column: "DnsName",
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_InstallationRepositories_AppRepositoryId",
                table: "InstallationRepositories",
                column: "AppRepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallationTags_TagId",
                table: "InstallationTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "UX_Machines_MachineName",
                table: "Machines",
                column: "MachineName",
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_PhysicalPaths_Path",
                table: "PhysicalPaths",
                column: "Path",
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessorArchitectures_ArchitectureName",
                table: "ProcessorArchitectures",
                column: "ArchitectureName",
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_RootPaths_Path",
                table: "RootPaths",
                column: "Path",
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_Tags_TagName",
                table: "Tags",
                column: "TagName",
                unique: true,
                filter: "[IsEnabled] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationUsers");

            migrationBuilder.DropTable(
                name: "InstallationRepositories");

            migrationBuilder.DropTable(
                name: "InstallationTags");

            migrationBuilder.DropTable(
                name: "AppRepositories");

            migrationBuilder.DropTable(
                name: "ApplicationInstallations");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "AppNames");

            migrationBuilder.DropTable(
                name: "AppStageNames");

            migrationBuilder.DropTable(
                name: "DnsEndpoints");

            migrationBuilder.DropTable(
                name: "Machines");

            migrationBuilder.DropTable(
                name: "PhysicalPaths");

            migrationBuilder.DropTable(
                name: "ProcessorArchitectures");

            migrationBuilder.DropTable(
                name: "RootPaths");
        }
    }
}
