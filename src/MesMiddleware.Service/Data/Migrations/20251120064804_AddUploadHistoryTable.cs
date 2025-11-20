using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MesMiddleware.Service.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadHistoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandAcknowledgments");

            migrationBuilder.DropTable(
                name: "Commands");

            migrationBuilder.CreateTable(
                name: "UploadHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MachineNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TraceCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LotNo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PartNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ResponseMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ResponseCode = table.Column<int>(type: "INTEGER", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    InspectionDataJson = table.Column<string>(type: "TEXT", maxLength: 100000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UploadHistory_LotNo",
                table: "UploadHistory",
                column: "LotNo");

            migrationBuilder.CreateIndex(
                name: "IX_UploadHistory_MachineNumber",
                table: "UploadHistory",
                column: "MachineNumber");

            migrationBuilder.CreateIndex(
                name: "IX_UploadHistory_Status",
                table: "UploadHistory",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UploadHistory_TraceCode",
                table: "UploadHistory",
                column: "TraceCode");

            migrationBuilder.CreateIndex(
                name: "IX_UploadHistory_UploadedAt",
                table: "UploadHistory",
                column: "UploadedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UploadHistory");

            migrationBuilder.CreateTable(
                name: "CommandAcknowledgments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AcknowledgedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CommandId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandAcknowledgments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Commands",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CommandType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MachineNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ParametersJson = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commands", x => x.CommandId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandAcknowledgments_AcknowledgedAt",
                table: "CommandAcknowledgments",
                column: "AcknowledgedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommandAcknowledgments_CommandId",
                table: "CommandAcknowledgments",
                column: "CommandId");

            migrationBuilder.CreateIndex(
                name: "IX_Commands_CommandType",
                table: "Commands",
                column: "CommandType");

            migrationBuilder.CreateIndex(
                name: "IX_Commands_IssuedAt",
                table: "Commands",
                column: "IssuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Commands_MachineNumber",
                table: "Commands",
                column: "MachineNumber");
        }
    }
}
