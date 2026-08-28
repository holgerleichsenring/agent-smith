using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentSmith.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 2026-08-28-3793: deliberately empty. 2026-08-28-b883 widened the money columns on
    /// SQL Server alone — SQLite stores a decimal as text and already returns every digit it
    /// was given — so this provider has nothing to alter. The migration exists because the
    /// data archive names a schema state by its head migration's NAME: without it the two
    /// providers report different heads at the same logical schema, and every
    /// SQLite-to-SQL-Server import is refused, which is the one journey the archive exists
    /// for.
    /// </summary>
    public partial class WidenMoneyPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
