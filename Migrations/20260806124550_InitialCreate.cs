using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaskFlow.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Done = table.Column<bool>(type: "INTEGER", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "normal"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    InternalNotes = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false, defaultValue: ""),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Tasks",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "Done", "DueDate", "InternalNotes", "IsArchived", "Priority", "Title" },
                values: new object[,]
                {
                    { 1, new DateTimeOffset(new DateTime(2026, 7, 12, 9, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "platform-team", true, null, "billing code OPS-114", false, "normal", "Set up the Git repo" },
                    { 2, new DateTimeOffset(new DateTime(2026, 7, 12, 9, 5, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "platform-team", true, null, "billing code OPS-114", false, "normal", "Open the first pull request" },
                    { 3, new DateTimeOffset(new DateTime(2026, 7, 12, 9, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "platform-team", false, new DateOnly(2026, 12, 1), "internal: blocked on the DTO refactor", false, "high", "Build the REST API" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Done",
                table: "Tasks",
                column: "Done");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tasks");
        }
    }
}
