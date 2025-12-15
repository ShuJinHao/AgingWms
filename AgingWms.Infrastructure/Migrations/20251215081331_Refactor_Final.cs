using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgingWms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Refactor_Final : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WarehouseSlots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TrayBarcode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SlotName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    LastUpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExtensionDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseSlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BatteryCells",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChannelIndex = table.Column<int>(type: "int", nullable: false),
                    IsNg = table.Column<bool>(type: "bit", nullable: false),
                    WarehouseSlotId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    LastUpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExtensionDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryCells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatteryCells_WarehouseSlots_WarehouseSlotId",
                        column: x => x.WarehouseSlotId,
                        principalTable: "WarehouseSlots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatteryCells_WarehouseSlotId",
                table: "BatteryCells",
                column: "WarehouseSlotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatteryCells");

            migrationBuilder.DropTable(
                name: "WarehouseSlots");
        }
    }
}
