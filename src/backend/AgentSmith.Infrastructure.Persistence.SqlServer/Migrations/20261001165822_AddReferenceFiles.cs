using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1000000000, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    SetId = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(191)", maxLength: 191, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferenceFiles");
        }
    }
}
