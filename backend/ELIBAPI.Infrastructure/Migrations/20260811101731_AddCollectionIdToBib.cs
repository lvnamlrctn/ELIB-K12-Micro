using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionIdToBib : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CollectionId",
                schema: "PrintBook",
                table: "Bib",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bib_CollectionId",
                schema: "PrintBook",
                table: "Bib",
                column: "CollectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bib_collection_CollectionId",
                schema: "PrintBook",
                table: "Bib",
                column: "CollectionId",
                principalSchema: "Ebook",
                principalTable: "collection",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bib_collection_CollectionId",
                schema: "PrintBook",
                table: "Bib");

            migrationBuilder.DropIndex(
                name: "IX_Bib_CollectionId",
                schema: "PrintBook",
                table: "Bib");

            migrationBuilder.DropColumn(
                name: "CollectionId",
                schema: "PrintBook",
                table: "Bib");
        }
    }
}
