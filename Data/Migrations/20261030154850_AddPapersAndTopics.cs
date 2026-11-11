using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseOfAlchemists.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPapersAndTopics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TopicId",
                table: "Resources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaperId",
                table: "Chapters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Papers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Papers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Papers_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Topics_Chapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 30, 15, 48, 45, 91, DateTimeKind.Utc).AddTicks(7495));

            migrationBuilder.CreateIndex(
                name: "IX_Resources_TopicId",
                table: "Resources",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_PaperId",
                table: "Chapters",
                column: "PaperId");

            migrationBuilder.CreateIndex(
                name: "IX_Papers_SubjectId",
                table: "Papers",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_ChapterId",
                table: "Topics",
                column: "ChapterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Chapters_Papers_PaperId",
                table: "Chapters",
                column: "PaperId",
                principalTable: "Papers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Resources_Topics_TopicId",
                table: "Resources",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chapters_Papers_PaperId",
                table: "Chapters");

            migrationBuilder.DropForeignKey(
                name: "FK_Resources_Topics_TopicId",
                table: "Resources");

            migrationBuilder.DropTable(
                name: "Papers");

            migrationBuilder.DropTable(
                name: "Topics");

            migrationBuilder.DropIndex(
                name: "IX_Resources_TopicId",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Chapters_PaperId",
                table: "Chapters");

            migrationBuilder.DropColumn(
                name: "TopicId",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "PaperId",
                table: "Chapters");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 30, 14, 25, 8, 408, DateTimeKind.Utc).AddTicks(6224));
        }
    }
}
