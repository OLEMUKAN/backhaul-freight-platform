using Xunit;
using NetTopologySuite.Geometries;
using RouteService.API.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RouteService.Tests.ServiceTests
{
    public class GeospatialServiceTests
    {
        private readonly GeospatialService _geospatialService;

        public GeospatialServiceTests()
        {
            _geospatialService = new GeospatialService();
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(-180, -90)]
        [InlineData(180, 90)]
        [InlineData(74.0060, 40.7128)] // New York City
        public void CreatePoint_ValidCoordinates_ReturnsPoint(double longitude, double latitude)
        {
            var point = _geospatialService.CreatePoint(longitude, latitude);
            Assert.NotNull(point);
            Assert.Equal(longitude, point.X);
            Assert.Equal(latitude, point.Y);
            Assert.Equal(4326, point.SRID);
        }

        [Fact]
        public void PointToCoordinateArray_ValidPoint_ReturnsCorrectArray()
        {
            var point = _geospatialService.CreatePoint(10.0, 20.0);
            var coordinates = _geospatialService.PointToCoordinateArray(point);
            Assert.Equal(new[] { 10.0, 20.0 }, coordinates);
        }

        [Fact]
        public void PointToCoordinateArray_NullPoint_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _geospatialService.PointToCoordinateArray(null));
        }

        [Theory]
        [InlineData(0, 0, true)]
        [InlineData(-180, -90, true)]
        [InlineData(180, 90, true)]
        [InlineData(-180.1, 0, false)]
        [InlineData(180.1, 0, false)]
        [InlineData(0, -90.1, false)]
        [InlineData(0, 90.1, false)]
        public void ValidatePoint_Coordinates_ReturnsExpectedValidationResult(double longitude, double latitude, bool expected)
        {
            var point = _geospatialService.CreatePoint(longitude, latitude);
            Assert.Equal(expected, _geospatialService.ValidatePoint(point));
        }
        
        [Fact]
        public void ValidatePoint_NullPoint_ReturnsFalse()
        {
            Assert.False(_geospatialService.ValidatePoint(null));
        }

        [Fact]
        public void CalculateDistanceInKilometers_KnownCoordinates_ReturnsCorrectDistance()
        {
            // Paris to London
            var paris = _geospatialService.CreatePoint(2.3522, 48.8566); 
            var london = _geospatialService.CreatePoint(-0.1276, 51.5074); 
            // Expected distance ~344 km (approx, depends on exact coordinates and earth radius model)
            var distance = _geospatialService.CalculateDistanceInKilometers(paris, london);
            Assert.InRange(distance, 340, 350); 
        }

        [Fact]
        public void CalculateDistanceInKilometers_NullPoint1_ThrowsArgumentNullException()
        {
            var point2 = _geospatialService.CreatePoint(0,0);
            Assert.Throws<ArgumentNullException>(() => _geospatialService.CalculateDistanceInKilometers(null, point2));
        }

        [Fact]
        public void CalculateDistanceInKilometers_NullPoint2_ThrowsArgumentNullException()
        {
            var point1 = _geospatialService.CreatePoint(0,0);
            Assert.Throws<ArgumentNullException>(() => _geospatialService.CalculateDistanceInKilometers(point1, null));
        }
        
        [Fact]
        public void CalculateDistanceInKilometers_InvalidPoint_ThrowsArgumentException()
        {
            var validPoint = _geospatialService.CreatePoint(0, 0);
            var invalidPoint = _geospatialService.CreatePoint(200, 0); // Invalid longitude
            Assert.Throws<ArgumentException>(() => _geospatialService.CalculateDistanceInKilometers(validPoint, invalidPoint));
        }

        [Fact]
        public void CalculateEstimatedDurationInMinutes_KnownDistanceAndSpeed_ReturnsCorrectDuration()
        {
            var point1 = _geospatialService.CreatePoint(0, 0);
            var point2 = _geospatialService.CreatePoint(0.89821, 0); // Approx 100km at equator
            // Distance should be ~100km
            // Speed 50 kph
            // Duration = 100km / 50kph = 2 hours = 120 minutes
            var duration = _geospatialService.CalculateEstimatedDurationInMinutes(point1, point2, 50);
            Assert.Equal(120, duration);
        }
        
        [Fact]
        public void CalculateEstimatedDurationInMinutes_DefaultSpeed_ReturnsCorrectDuration()
        {
            // Paris (2.3522, 48.8566) to Amsterdam (4.8952, 52.3702)
            // Distance is approx 430 km.
            // Default speed 70 kph.
            // Duration = 430 / 70 = ~6.14 hours = ~369 minutes
            var paris = _geospatialService.CreatePoint(2.3522, 48.8566);
            var amsterdam = _geospatialService.CreatePoint(4.8952, 52.3702);
            var duration = _geospatialService.CalculateEstimatedDurationInMinutes(paris, amsterdam); // Uses default 70 kph
            Assert.InRange(duration, 365, 375); // Allowing some variance
        }

        [Fact]
        public void CalculateEstimatedDurationInMinutes_ZeroSpeed_ThrowsArgumentOutOfRangeException()
        {
            var point1 = _geospatialService.CreatePoint(0, 0);
            var point2 = _geospatialService.CreatePoint(1, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => _geospatialService.CalculateEstimatedDurationInMinutes(point1, point2, 0));
        }
        
        [Fact]
        public void CalculateEstimatedDurationInMinutes_NegativeSpeed_ThrowsArgumentOutOfRangeException()
        {
            var point1 = _geospatialService.CreatePoint(0, 0);
            var point2 = _geospatialService.CreatePoint(1, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => _geospatialService.CalculateEstimatedDurationInMinutes(point1, point2, -10));
        }

        [Fact]
        public void CreateLineString_ValidPoints_ReturnsLineString()
        {
            var points = new List<Point>
            {
                _geospatialService.CreatePoint(0, 0),
                _geospatialService.CreatePoint(1, 1),
                _geospatialService.CreatePoint(2, 2)
            };
            var lineString = _geospatialService.CreateLineString(points);
            Assert.NotNull(lineString);
            Assert.Equal(3, lineString.Coordinates.Length);
            Assert.Equal(points.Select(p => p.Coordinate), lineString.Coordinates);
            Assert.Equal(4326, lineString.SRID);
        }

        [Fact]
        public void CreateLineString_EmptyListOfPoints_ThrowsArgumentNullException()
        {
             var points = new List<Point>();
             Assert.Throws<ArgumentNullException>(() => _geospatialService.CreateLineString(points));
        }
        
        [Fact]
        public void CreateLineString_NullInput_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _geospatialService.CreateLineString(null));
        }

        [Fact]
        public void CreateLineString_ListWithNullPoint_ThrowsArgumentException()
        {
            var points = new List<Point>
            {
                _geospatialService.CreatePoint(0, 0),
                null,
                _geospatialService.CreatePoint(2, 2)
            };
            Assert.Throws<ArgumentException>(() => _geospatialService.CreateLineString(points));
        }
    }
}
