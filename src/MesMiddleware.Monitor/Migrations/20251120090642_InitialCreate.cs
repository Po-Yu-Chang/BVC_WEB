using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MesMiddleware.Monitor.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UploadHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TraceCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LotNo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RowNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    QueueItemId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UploadQueue",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TraceCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LotNo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RowNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ProcName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DevName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    WorkClass = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    InspectionTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ParamDataJson = table.Column<string>(type: "TEXT", nullable: true),
                    BenchmarksJson = table.Column<string>(type: "TEXT", nullable: true),
                    OtherDataJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastRetryAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadQueue", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UploadHistory_QueueItemId",
                table: "UploadHistory",
                column: "QueueItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadHistory_Status",
                table: "UploadHistory",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UploadHistory_UploadedAt",
                table: "UploadHistory",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UploadQueue_CreatedAt",
                table: "UploadQueue",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UploadQueue_NextRetryAt",
                table: "UploadQueue",
                column: "NextRetryAt");

            migrationBuilder.CreateIndex(
                name: "IX_UploadQueue_Status",
                table: "UploadQueue",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UploadHistory");

            migrationBuilder.DropTable(
                name: "UploadQueue");
        }
    }
}
