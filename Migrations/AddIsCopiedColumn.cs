using Microsoft.EntityFrameworkCore.Migrations;

namespace ScanerServer.Migrations
{
    public partial class AddIsCopiedColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCopied",
                table: "HttpRequests",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsCopied", table: "HttpRequests");
        }
    }
}
