using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GRP_03_27.Migrations
{
    /// <inheritdoc />
    public partial class AddNewFridgeRequestTableSimple : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AllocationDate",
                table: "Fridges",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NewFridgeRequests",
                columns: table => new
                {
                    NewFridgeRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    BusinessJustification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AdditionalNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    EstimatedQuantity = table.Column<int>(type: "int", nullable: true),
                    ApprovedQuantity = table.Column<int>(type: "int", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovalNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AllocationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AllocatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AllocationNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    RequestedById = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewFridgeRequests", x => x.NewFridgeRequestId);
                    table.ForeignKey(
                        name: "FK_NewFridgeRequests_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewFridgeRequests_CustomerId",
                table: "NewFridgeRequests",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewFridgeRequests");

            migrationBuilder.DropColumn(
                name: "AllocationDate",
                table: "Fridges");
        }
    }
}
