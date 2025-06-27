using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliTrans.Migrations.Sqlite.Migrations;

/// <inheritdoc />
public partial class Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Originals",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Hash = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Content = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Originals", x => x.Id);
                table.UniqueConstraint("AK_Originals_Hash", x => x.Hash);
            }
        );

        migrationBuilder.CreateTable(
            name: "Translations",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                OriginalHash = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Content = table.Column<string>(type: "text", nullable: false),
                Language = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Translations", x => x.Id);
                table.ForeignKey(
                    name: "FK_Translations_Originals_OriginalHash",
                    column: x => x.OriginalHash,
                    principalTable: "Originals",
                    principalColumn: "Hash",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_Translations_OriginalHash",
            table: "Translations",
            column: "OriginalHash"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Translations");

        migrationBuilder.DropTable(name: "Originals");
    }
}
