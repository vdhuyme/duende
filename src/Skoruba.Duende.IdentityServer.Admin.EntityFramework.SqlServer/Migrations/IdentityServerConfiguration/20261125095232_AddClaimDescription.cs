using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skoruba.Duende.IdentityServer.Admin.EntityFramework.SqlServer.Migrations.IdentityServerConfiguration
{
    /// <inheritdoc />
    public partial class AddClaimDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ClientClaims",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "ClientClaims",
                type: "nvarchar(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "ClientClaim");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "ClientClaims");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "ClientClaims");
        }
    }
}
