using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChiTieu.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CheckedInAt",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Transactions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationAccuracy",
                table: "Transactions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationName",
                table: "Transactions",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Transactions",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_GroupId_Latitude_Longitude",
                table: "Transactions",
                columns: new[] { "GroupId", "Latitude", "Longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_GroupId_Latitude_Longitude",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CheckedInAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "LocationAccuracy",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "LocationName",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Transactions");
        }
    }
}
