using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgingWms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSlotSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentStep",
                table: "WarehouseSlots",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdateTime",
                table: "WarehouseSlots",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentStep",
                table: "WarehouseSlots");

            migrationBuilder.DropColumn(
                name: "LastUpdateTime",
                table: "WarehouseSlots");
        }
    }
}
