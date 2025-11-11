using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MesMiddleware.Service.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QueuedUploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InspectionDataJson = table.Column<string>(type: "TEXT", maxLength: 100000, nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MachineNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TraceCodeOrLotNo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueuedUploads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QueuedUploads_MachineNumber",
                table: "QueuedUploads",
                column: "MachineNumber");

            migrationBuilder.CreateIndex(
                name: "IX_QueuedUploads_NextRetryAt",
                table: "QueuedUploads",
                column: "NextRetryAt");

            migrationBuilder.CreateIndex(
                name: "IX_QueuedUploads_QueuedAt",
                table: "QueuedUploads",
                column: "QueuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_QueuedUploads_Status",
                table: "QueuedUploads",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QueuedUploads");
        }
    }
}
