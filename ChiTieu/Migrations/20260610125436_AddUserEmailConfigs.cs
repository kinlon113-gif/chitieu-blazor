using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChiTieu.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEmailConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserEmailConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    GmailAddress = table.Column<string>(type: "text", nullable: false),
                    GmailAppPassword = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastFetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEmailConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserEmailConfigs_UserId_GroupId",
                table: "UserEmailConfigs",
                columns: new[] { "UserId", "GroupId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserEmailConfigs");
        }
    }
}
