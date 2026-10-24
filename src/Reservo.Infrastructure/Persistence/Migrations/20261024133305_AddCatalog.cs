using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Reservo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "ru-RU-x-icu"),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "features",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "ru-RU-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_features", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "ru-RU-x-icu"),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "resources",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    location_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false, collation: "ru-RU-x-icu"),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    floor = table.Column<int>(type: "integer", nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    open_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    close_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    max_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resources", x => x.id);
                    table.CheckConstraint("ck_resources_capacity_positive", "capacity IS NULL OR capacity > 0");
                    table.CheckConstraint("ck_resources_max_duration_positive", "max_duration_minutes IS NULL OR max_duration_minutes > 0");
                    table.CheckConstraint("ck_resources_working_hours", "close_time > open_time");
                    table.ForeignKey(
                        name: "fk_resources_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_resources_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resource_features",
                columns: table => new
                {
                    resource_id = table.Column<int>(type: "integer", nullable: false),
                    feature_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_features", x => new { x.resource_id, x.feature_id });
                    table.ForeignKey(
                        name: "fk_resource_features_features_feature_id",
                        column: x => x.feature_id,
                        principalTable: "features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_resource_features_resources_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_categories_code",
                table: "categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categories_name",
                table: "categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_features_code",
                table: "features",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_features_name",
                table: "features",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_locations_name",
                table: "locations",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resource_features_feature_id",
                table: "resource_features",
                column: "feature_id");

            migrationBuilder.CreateIndex(
                name: "ix_resources_category_id",
                table: "resources",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_resources_location_id",
                table: "resources",
                column: "location_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resource_features");

            migrationBuilder.DropTable(
                name: "features");

            migrationBuilder.DropTable(
                name: "resources");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "locations");
        }
    }
}
