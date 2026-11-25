using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Reservo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpacesAndOvernight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allows_overnight",
                table: "resources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "deactivation_reason",
                table: "resources",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pickup_info",
                table: "resources",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "seat_column",
                table: "resources",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "seat_number",
                table: "resources",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "seat_row",
                table: "resources",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "space_id",
                table: "resources",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "categories",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Room");

            // В уже заполненной базе оборудование было одной категорией с кодом equipment — она становится видом Equipment.
            // Новые категории оборудования, коворкинги и места добавляет сид только в пустую базу (PROJECT.md, 11.4).
            migrationBuilder.Sql("UPDATE categories SET kind = 'Equipment' WHERE code = 'equipment';");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "bookings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateTable(
                name: "spaces",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    location_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false, collation: "ru-RU-x-icu"),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    floor = table.Column<int>(type: "integer", nullable: true),
                    is_quiet = table.Column<bool>(type: "boolean", nullable: false),
                    rows = table.Column<int>(type: "integer", nullable: false),
                    columns = table.Column<int>(type: "integer", nullable: false),
                    aisle_after_column = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spaces", x => x.id);
                    table.CheckConstraint("ck_spaces_aisle", "aisle_after_column IS NULL OR (aisle_after_column > 0 AND aisle_after_column < columns)");
                    table.CheckConstraint("ck_spaces_layout", "rows > 0 AND columns > 0");
                    table.ForeignKey(
                        name: "fk_spaces_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_resources_space_id_seat_number",
                table: "resources",
                columns: new[] { "space_id", "seat_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resources_space_id_seat_row_seat_column",
                table: "resources",
                columns: new[] { "space_id", "seat_row", "seat_column" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_resources_seat_complete",
                table: "resources",
                sql: "(space_id IS NULL AND seat_number IS NULL AND seat_row IS NULL AND seat_column IS NULL) OR (space_id IS NOT NULL AND seat_number > 0 AND seat_row > 0 AND seat_column > 0)");

            migrationBuilder.CreateIndex(
                name: "ix_spaces_location_id",
                table: "spaces",
                column: "location_id");

            migrationBuilder.AddForeignKey(
                name: "fk_resources_spaces_space_id",
                table: "resources",
                column: "space_id",
                principalTable: "spaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_resources_spaces_space_id",
                table: "resources");

            migrationBuilder.DropTable(
                name: "spaces");

            migrationBuilder.DropIndex(
                name: "ix_resources_space_id_seat_number",
                table: "resources");

            migrationBuilder.DropIndex(
                name: "ix_resources_space_id_seat_row_seat_column",
                table: "resources");

            migrationBuilder.DropCheckConstraint(
                name: "ck_resources_seat_complete",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "allows_overnight",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "deactivation_reason",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "pickup_info",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "seat_column",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "seat_number",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "seat_row",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "space_id",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "categories");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "bookings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
