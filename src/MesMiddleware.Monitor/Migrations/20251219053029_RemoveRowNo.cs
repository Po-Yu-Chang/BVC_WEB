using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MesMiddleware.Monitor.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRowNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowNo",
                table: "UploadQueue");

            migrationBuilder.DropColumn(
                name: "RowNo",
                table: "UploadHistory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RowNo",
                table: "UploadQueue",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RowNo",
                table: "UploadHistory",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
