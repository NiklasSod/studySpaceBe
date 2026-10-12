using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lmsPortalBe.Migrations
{
    /// <inheritdoc />
    public partial class AddUserResourceProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lmsUserResourceProgress",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ResourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProgressStatus = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Current"),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lmsUserResourceProgress", x => new { x.UserId, x.ResourceId });
                    table.CheckConstraint("CK_lmsUserResourceProgress_UserProgressStatus", "\"UserProgressStatus\" IN ('Current', 'CheckLater', 'Done', 'Archived')");
                    table.ForeignKey(
                        name: "FK_lmsUserResourceProgress_lmsResource_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "lmsResource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lmsUserResourceProgress_lmsUser_UserId",
                        column: x => x.UserId,
                        principalTable: "lmsUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lmsUserResourceProgress_ResourceId",
                table: "lmsUserResourceProgress",
                column: "ResourceId");

            migrationBuilder.Sql(
                """
                INSERT INTO "lmsUserResourceProgress"
                    ("UserId", "ResourceId", "UserProgressStatus", "IsFavorite")
                SELECT "CreatorId", "Id", 'Current', 0
                FROM "lmsResource";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lmsUserResourceProgress");
        }
    }
}
