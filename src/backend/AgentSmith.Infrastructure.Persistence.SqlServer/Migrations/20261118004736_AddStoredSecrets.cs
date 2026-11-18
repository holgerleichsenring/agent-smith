using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredSecrets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoredSecrets",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Ciphertext = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Nonce = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    WrappedKey = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    WrapNonce = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    KeyId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SetBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredSecrets", x => x.Name);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoredSecrets");
        }
    }
}
