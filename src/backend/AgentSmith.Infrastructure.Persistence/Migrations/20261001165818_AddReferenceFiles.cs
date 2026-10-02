using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferenceFiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    SetId = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false),
                    Length = table.Column<long>(type: "INTEGER", nullable: false),
                    // 2026-10-01-283da: NO type argument, as for the legacy image column. Three
                    // providers run this set verbatim and "BLOB" is a SQLite word: a 64 KB blob on
                    // MySQL and no type at all on Postgres. Untyped, each provider's convention maps
                    // a byte array to its own large binary type.
                    Content = table.Column<byte[]>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceFiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceFiles_SessionId",
                table: "ReferenceFiles",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceFiles_SetId",
                table: "ReferenceFiles",
                column: "SetId");

            SeedIdentity(migrationBuilder);
        }

        /// <summary>
        /// 2026-10-01-283da: the identity starts at a billion, above every legacy image id, so a
        /// copied image keeps its number in one id space. AUTOINCREMENT takes the larger of its
        /// sequence row and the largest key, so an explicit lower key never pulls it back. In this
        /// set only SQLite generates the key at all — the Id literal carries no identity on Postgres
        /// or MySQL, for any table — so there is nothing to seed for them here.
        /// </summary>
        private static void SeedIdentity(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Microsoft.EntityFrameworkCore.Sqlite") return;
            migrationBuilder.Sql(
                "INSERT INTO sqlite_sequence (name, seq) VALUES ('ReferenceFiles', 999999999);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferenceFiles");
        }
    }
}
