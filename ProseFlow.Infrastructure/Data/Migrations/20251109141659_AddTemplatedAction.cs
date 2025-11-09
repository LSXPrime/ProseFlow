using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProseFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplatedAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Instruction",
                table: "History",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresSelection",
                table: "Actions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ActionPlaceholders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    InputType = table.Column<int>(type: "INTEGER", nullable: false),
                    OptionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultValue = table.Column<string>(type: "TEXT", nullable: true),
                    ValidationJson = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayConditionJson = table.Column<string>(type: "TEXT", nullable: true),
                    ActionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionPlaceholders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionPlaceholders_Actions_ActionId",
                        column: x => x.ActionId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionPlaceholders_ActionId",
                table: "ActionPlaceholders",
                column: "ActionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionPlaceholders");

            migrationBuilder.DropColumn(
                name: "Instruction",
                table: "History");

            migrationBuilder.DropColumn(
                name: "RequiresSelection",
                table: "Actions");
        }
    }
}
