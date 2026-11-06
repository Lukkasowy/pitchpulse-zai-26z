using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PitchPulse.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    JerseyNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Series",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: true),
                    MetricType = table.Column<int>(type: "integer", nullable: false),
                    MinValue = table.Column<double>(type: "double precision", nullable: false),
                    MaxValue = table.Column<double>(type: "double precision", nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Icon = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                    table.CheckConstraint("CK_Series_ValueRange", "\"MinValue\" < \"MaxValue\"");
                    table.ForeignKey(
                        name: "FK_Series_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Measurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeriesId = table.Column<int>(type: "integer", nullable: false),
                    SensorId = table.Column<int>(type: "integer", nullable: true),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Measurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Measurements_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Players",
                columns: new[] { "Id", "FirstName", "JerseyNumber", "LastName" },
                values: new object[,]
                {
                    { 1, "Adam", 7, "Nowak" },
                    { 2, "Michał", 9, "Kowalski" },
                    { 3, "Piotr", 10, "Wiśniewski" }
                });

            migrationBuilder.InsertData(
                table: "Series",
                columns: new[] { "Id", "Color", "Icon", "MaxValue", "MetricType", "MinValue", "Name", "PlayerId", "Unit" },
                values: new object[,]
                {
                    { 1, "#1f77b4", "circle", 220.0, 1, 40.0, "Tętno — zawodnik nr 7", 1, "bpm" },
                    { 2, "#ff7f0e", "square", 220.0, 1, 40.0, "Tętno — zawodnik nr 9", 2, "bpm" },
                    { 3, "#2ca02c", "diamond", 220.0, 1, 40.0, "Tętno — zawodnik nr 10", 3, "bpm" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Measurements_SeriesId_Timestamp",
                table: "Measurements",
                columns: new[] { "SeriesId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Measurements_Timestamp",
                table: "Measurements",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Series_PlayerId",
                table: "Series",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Measurements");

            migrationBuilder.DropTable(
                name: "Series");

            migrationBuilder.DropTable(
                name: "Players");
        }
    }
}
