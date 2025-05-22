using NetTopologySuite.Geometries;

namespace RouteService.API.Services.Interfaces
{
    /// <summary>
    /// Interface for geospatial operations
    /// </summary>
    public interface IGeospatialService
    {
        /// <summary>
        /// Calculates the distance between two points in kilometers
        /// </summary>
        /// <param name="point1">First point</param>
        /// <param name="point2">Second point</param>
        /// <returns>Distance in kilometers</returns>
        double CalculateDistanceInKilometers(Point point1, Point point2);
        
        /// <summary>
        /// Calculates an estimated duration between two points in minutes
        /// </summary>
        /// <param name="point1">First point</param>
        /// <param name="point2">Second point</param>
        /// <param name="averageSpeedKph">Average speed in km/h (default: 70)</param>
        /// <returns>Duration in minutes</returns>
        int CalculateEstimatedDurationInMinutes(Point point1, Point point2, double averageSpeedKph = 70);
        
        /// <summary>
        /// Creates a line string from a collection of points (route path)
        /// </summary>
        /// <param name="points">Collection of points</param>
        /// <returns>LineString representing the route path</returns>
        LineString CreateLineString(IEnumerable<Point> points);
        
        /// <summary>
        /// Validates if a point falls within valid coordinate ranges
        /// </summary>
        /// <param name="point">The point to validate</param>
        /// <returns>True if valid, otherwise false</returns>
        bool ValidatePoint(Point point);
        
        /// <summary>
        /// Converts a coordinate pair to a Point
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        /// <returns>Point object</returns>
        Point CreatePoint(double longitude, double latitude);
        
        /// <summary>
        /// Converts a Point to a coordinate array [longitude, latitude]
        /// </summary>
        /// <param name="point">The Point to convert</param>
        /// <returns>Array with [longitude, latitude]</returns>
        double[] PointToCoordinateArray(Point point);
    }
}
