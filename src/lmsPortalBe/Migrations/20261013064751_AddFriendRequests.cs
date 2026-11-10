using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lmsPortalBe.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lmsFriendRequest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SenderUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    RecipientUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UserOneId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UserTwoId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 140, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lmsFriendRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lmsFriendRequest_lmsUser_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "lmsUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lmsFriendRequest_lmsUser_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "lmsUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lmsFriendRequest_RecipientUserId",
                table: "lmsFriendRequest",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_lmsFriendRequest_SenderUserId",
                table: "lmsFriendRequest",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_lmsFriendRequest_UserOneId_UserTwoId",
                table: "lmsFriendRequest",
                columns: new[] { "UserOneId", "UserTwoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lmsFriendRequest");
        }
    }
}
