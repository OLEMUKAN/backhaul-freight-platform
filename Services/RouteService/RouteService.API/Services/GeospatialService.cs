using NetTopologySuite.Geometries;
using RouteService.API.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RouteService.API.Services
{
    public class GeospatialService : IGeospatialService
    {
        private readonly GeometryFactory _geometryFactory;
        private const double EarthRadiusKm = 6371.0;

        public GeospatialService()
        {
            _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326); // SRID 4326 for WGS 84
        }

        public Point CreatePoint(double longitude, double latitude)
        {
            return _geometryFactory.CreatePoint(new Coordinate(longitude, latitude));
        }

        public double[] PointToCoordinateArray(Point point)
        {
            if (point == null)
                throw new ArgumentNullException(nameof(point));

            return new[] { point.X, point.Y };
        }

        public bool ValidatePoint(Point point)
        {
            if (point == null)
                return false;

            return point.X >= -180 && point.X <= 180 && point.Y >= -90 && point.Y <= 90;
        }

        public double CalculateDistanceInKilometers(Point point1, Point point2)
        {
            if (point1 == null)
                throw new ArgumentNullException(nameof(point1));
            if (point2 == null)
                throw new ArgumentNullException(nameof(point2));

            if (!ValidatePoint(point1) || !ValidatePoint(point2))
                throw new ArgumentException("Invalid point coordinates.");

            var lat1Rad = DegreesToRadians(point1.Y);
            var lon1Rad = DegreesToRadians(point1.X);
            var lat2Rad = DegreesToRadians(point2.Y);
            var lon2Rad = DegreesToRadians(point2.X);

            var deltaLat = lat2Rad - lat1Rad;
            var deltaLon = lon2Rad - lon1Rad;

            var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusKm * c;
        }

        public int CalculateEstimatedDurationInMinutes(Point point1, Point point2, double averageSpeedKph = 70)
        {
            if (averageSpeedKph <= 0)
                throw new ArgumentOutOfRangeException(nameof(averageSpeedKph), "Average speed must be positive.");

            var distanceKm = CalculateDistanceInKilometers(point1, point2);
            var durationHours = distanceKm / averageSpeedKph;
            return (int)Math.Round(durationHours * 60);
        }

        public LineString CreateLineString(IEnumerable<Point> points)
        {
            if (points == null || !points.Any())
                // Or return _geometryFactory.CreateLineString((Coordinate[])null); if empty linestring is preferred
                throw new ArgumentNullException(nameof(points), "Points collection cannot be null or empty.");

            var coordinates = points.Select(p =>
            {
                if (p == null) throw new ArgumentException("Point collection contains null points.");
                return p.Coordinate;
            }).ToArray();
            
            return _geometryFactory.CreateLineString(coordinates);
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
