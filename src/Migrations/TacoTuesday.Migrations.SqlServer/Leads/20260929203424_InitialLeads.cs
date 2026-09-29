using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TacoTuesday.Migrations.SqlServer.Leads
{
    /// <inheritdoc />
    public partial class InitialLeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Company = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Leads_NormalizedEmail",
                table: "Leads",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Leads");
        }
    }
}
