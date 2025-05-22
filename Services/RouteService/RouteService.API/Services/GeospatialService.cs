using NetTopologySuite.Geometries;
using RouteService.API.Services.Interfaces;

namespace RouteService.API.Services
{
    /// <summary>
    /// Service for handling geospatial operations
    /// </summary>
    public class GeospatialService : IGeospatialService
    {
        private readonly GeometryFactory _geometryFactory;
        
        public GeospatialService()
        {
            // Create factory with SRID 4326 (WGS84, standard for GPS)
            _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        }

        /// <inheritdoc />
        public double CalculateDistanceInKilometers(Point point1, Point point2)
        {
            if (point1 == null || point2 == null)
                throw new ArgumentNullException(nameof(point1), "Points cannot be null");

            // Haversine formula for calculating great-circle distance between two points on a sphere
            const double earthRadiusKm = 6371.0;
            
            var lat1 = point1.Y * Math.PI / 180;
            var lon1 = point1.X * Math.PI / 180;
            var lat2 = point2.Y * Math.PI / 180;
            var lon2 = point2.X * Math.PI / 180;
            
            var dLat = lat2 - lat1;
            var dLon = lon2 - lon1;
            
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1) * Math.Cos(lat2) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                    
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            
            return earthRadiusKm * c;
        }

        /// <inheritdoc />
        public int CalculateEstimatedDurationInMinutes(Point point1, Point point2, double averageSpeedKph = 70)
        {
            if (averageSpeedKph <= 0)
                throw new ArgumentException("Average speed must be greater than zero", nameof(averageSpeedKph));
                
            // Calculate distance
            var distanceKm = CalculateDistanceInKilometers(point1, point2);
            
            // Calculate time in hours: distance / speed
            var timeHours = distanceKm / averageSpeedKph;
            
            // Convert to minutes and round to nearest integer
            return (int)Math.Round(timeHours * 60);
        }

        /// <inheritdoc />
        public LineString CreateLineString(IEnumerable<Point> points)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));
                
            var pointArray = points.ToArray();
            
            if (pointArray.Length < 2)
                throw new ArgumentException("At least two points are required to create a LineString", nameof(points));
                
            return _geometryFactory.CreateLineString(pointArray);
        }

        /// <inheritdoc />
        public bool ValidatePoint(Point point)
        {
            if (point == null)
                return false;
                
            // Check if coordinates are within valid ranges
            // Longitude: -180 to 180
            // Latitude: -90 to 90
            return point.X >= -180 && point.X <= 180 && 
                   point.Y >= -90 && point.Y <= 90;
        }

        /// <inheritdoc />
        public Point CreatePoint(double longitude, double latitude)
        {
            // Validate coordinates
            if (longitude < -180 || longitude > 180)
                throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180");
                
            if (latitude < -90 || latitude > 90)
                throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90");
                
            // Create point with correct axis order (X=longitude, Y=latitude)
            return _geometryFactory.CreatePoint(new Coordinate(longitude, latitude));
        }

        /// <inheritdoc />
        public double[] PointToCoordinateArray(Point point)
        {
            if (point == null)
                throw new ArgumentNullException(nameof(point));
                
            // Return as [longitude, latitude]
            return new double[] { point.X, point.Y };
        }
    }
}
