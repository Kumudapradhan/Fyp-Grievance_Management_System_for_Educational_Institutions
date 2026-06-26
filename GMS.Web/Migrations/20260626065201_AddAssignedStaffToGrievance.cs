using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedStaffToGrievance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedStaffUserId",
                table: "Grievances",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grievances_AssignedStaffUserId",
                table: "Grievances",
                column: "AssignedStaffUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Grievances_AspNetUsers_AssignedStaffUserId",
                table: "Grievances",
                column: "AssignedStaffUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Grievances_AspNetUsers_AssignedStaffUserId",
                table: "Grievances");

            migrationBuilder.DropIndex(
                name: "IX_Grievances_AssignedStaffUserId",
                table: "Grievances");

            migrationBuilder.DropColumn(
                name: "AssignedStaffUserId",
                table: "Grievances");
        }
    }
}
