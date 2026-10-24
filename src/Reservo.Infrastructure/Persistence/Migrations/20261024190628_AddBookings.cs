using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Reservo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    resource_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    attendees = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reminder_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.CheckConstraint("ck_bookings_attendees_positive", "attendees IS NULL OR attendees > 0");
                    table.CheckConstraint("ck_bookings_end_after_start", "end_at > start_at");
                    table.ForeignKey(
                        name: "fk_bookings_resources_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_resource_id_start_at",
                table: "bookings",
                columns: new[] { "resource_id", "start_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_status_start_at",
                table: "bookings",
                columns: new[] { "status", "start_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_user_id_start_at",
                table: "bookings",
                columns: new[] { "user_id", "start_at" });

            // Защита от двойных броней на уровне базы (PROJECT.md, 5.3). EF Core не умеет описывать такие
            // ограничения в модели, поэтому оно добавлено в миграцию вручную.
            // Смысл: среди активных броней (Pending и Confirmed) не может быть двух строк с одним resource_id,
            // у которых пересекаются интервалы [start_at, end_at). Проверку делает GiST-индекс, которому
            // нужно расширение btree_gist (подключено в миграции InitialCreate).
            // Нарушение даёт ошибку 23P01, которую ConstraintErrors превращает в 409 booking.time_conflict.
            migrationBuilder.Sql(
                """
                ALTER TABLE bookings
                    ADD CONSTRAINT ex_bookings_no_overlap
                    EXCLUDE USING gist (
                        resource_id WITH =,
                        tstzrange(start_at, end_at, '[)') WITH &&
                    )
                    WHERE (status IN ('Pending', 'Confirmed'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE bookings DROP CONSTRAINT IF EXISTS ex_bookings_no_overlap;");

            migrationBuilder.DropTable(
                name: "bookings");
        }
    }
}
