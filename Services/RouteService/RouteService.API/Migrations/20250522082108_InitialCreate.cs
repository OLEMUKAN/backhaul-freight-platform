using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace RouteService.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsReturnLeg = table.Column<bool>(type: "boolean", nullable: false),
                    OriginAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OriginPoint = table.Column<Point>(type: "geometry(Point, 4326)", nullable: false),
                    DestinationAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DestinationPoint = table.Column<Point>(type: "geometry(Point, 4326)", nullable: false),
                    ViaPoints = table.Column<string>(type: "text", nullable: true),
                    GeometryPath = table.Column<LineString>(type: "geometry(LineString, 4326)", nullable: true),
                    DepartureTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArrivalTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CapacityAvailableKg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CapacityAvailableM3 = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    TotalCapacityKg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TotalCapacityM3 = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    EstimatedDistanceKm = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    EstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Routes_DestinationPoint",
                table: "Routes",
                column: "DestinationPoint")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_IsReturnLeg",
                table: "Routes",
                column: "IsReturnLeg");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_OriginPoint",
                table: "Routes",
                column: "OriginPoint")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_OwnerId",
                table: "Routes",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_TruckId",
                table: "Routes",
                column: "TruckId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Routes");
        }
    }
}
