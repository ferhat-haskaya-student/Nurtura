using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nurtura.Data.Migrations
{
    /// <summary>
    /// One-off data fix (F, 5 Nov 2026): Sunday is a day off. Closes every open Sunday slot that has
    /// no active (requested or confirmed) appointment. Sunday slots with an active appointment are left open.
    /// </summary>
    public partial class CloseSundaySlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "AvailabilitySlots" AS s
                SET "IsOpen" = FALSE, "UpdatedAt" = now()
                WHERE s."IsOpen"
                  AND EXTRACT(DOW FROM s."Date") = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM "Appointments" AS a
                      WHERE a."AvailabilitySlotId" = s."Id"
                        AND a."Status" IN ('Requested', 'Confirmed'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Which Sunday hours were open before is not kept, so there is nothing to undo.
        }
    }
}
