using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LionessstaAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoriesAndProductFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "ProductImages");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "ProductImages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ProductImages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "ProductImages",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_CategoryId",
                table: "ProductImages",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            // Seed a default category and point any pre-existing products at it
            // (their old free-text Category column is being dropped above), so the
            // FK constraint below doesn't fail on rows still defaulted to CategoryId 0.
            migrationBuilder.Sql(
                "INSERT INTO [Categories] ([Name], [Slug], [Description], [CreatedAt]) " +
                "VALUES ('Uncategorized', 'uncategorized', 'Default category for products migrated from the old schema.', GETUTCDATE());");

            migrationBuilder.Sql(
                "UPDATE [ProductImages] SET [CategoryId] = (SELECT [Id] FROM [Categories] WHERE [Slug] = 'uncategorized');");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductImages_Categories_CategoryId",
                table: "ProductImages",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductImages_Categories_CategoryId",
                table: "ProductImages");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_ProductImages_CategoryId",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "ProductImages");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ProductImages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
