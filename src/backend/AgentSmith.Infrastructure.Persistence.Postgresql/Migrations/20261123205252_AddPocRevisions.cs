using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Postgresql.Migrations
{
    /// <inheritdoc />
    public partial class AddPocRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PocRevisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PocId = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    UploadSession = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    UploadId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Actor = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: false),
                    RunId = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    ManifestJson = table.Column<string>(type: "text", nullable: true),
                    Port = table.Column<int>(type: "integer", nullable: true),
                    Entrypoint = table.Column<string>(type: "text", nullable: true),
                    BaseImage = table.Column<string>(type: "character varying(191)", maxLength: 191, nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PocRevisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PocRevisions_PocId_Number",
                table: "PocRevisions",
                columns: new[] { "PocId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PocRevisions_UploadId",
                table: "PocRevisions",
                column: "UploadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PocRevisions");
        }
    }
}
