using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliTrans.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddOptimizedFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOptimized",
                table: "Translations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOptimized",
                table: "Translations");
        }
    }
}
