using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgingWms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFactorySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Wms_WarehouseSlots",
                columns: table => new
                {
                    SlotId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SlotName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TrayBarcode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Cells = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wms_WarehouseSlots", x => x.SlotId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Wms_WarehouseSlots_LastUpdatedTime",
                table: "Wms_WarehouseSlots",
                column: "LastUpdatedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Wms_WarehouseSlots_Status",
                table: "Wms_WarehouseSlots",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Wms_WarehouseSlots");
        }
    }
}
