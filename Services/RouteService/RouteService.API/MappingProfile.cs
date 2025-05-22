using AutoMapper;
using NetTopologySuite.Geometries;
using RouteService.API.Dtos.Routes;
using RouteService.API.Models.Routes;
using RouteService.API.Services.Interfaces;
using System.Linq;
using System.Text.Json;

namespace RouteService.API
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // For CreateRouteRequest -> Route
            CreateMap<CreateRouteRequest, Route>()
                .ForMember(dest => dest.OriginPoint, opt => opt.MapFrom((src, dest, destMember, context) => 
                    context.Mapper.ConfigurationProvider.Host<IGeospatialService>().CreatePoint(src.OriginCoordinates[0], src.OriginCoordinates[1])))
                .ForMember(dest => dest.DestinationPoint, opt => opt.MapFrom((src, dest, destMember, context) => 
                    context.Mapper.ConfigurationProvider.Host<IGeospatialService>().CreatePoint(src.DestinationCoordinates[0], src.DestinationCoordinates[1])))
                .ForMember(dest => dest.ViaPoints, opt => opt.MapFrom(src => 
                    src.ViaPoints != null && src.ViaPoints.Any() ? JsonSerializer.Serialize(src.ViaPoints, (JsonSerializerOptions)null) : null));

            // For Route -> RouteDto
            CreateMap<Route, RouteDto>()
                .ForMember(dest => dest.OriginCoordinates, opt => opt.MapFrom((src, dest, destMember, context) => 
                    context.Mapper.ConfigurationProvider.Host<IGeospatialService>().PointToCoordinateArray(src.OriginPoint)))
                .ForMember(dest => dest.DestinationCoordinates, opt => opt.MapFrom((src, dest, destMember, context) => 
                    context.Mapper.ConfigurationProvider.Host<IGeospatialService>().PointToCoordinateArray(src.DestinationPoint)))
                .ForMember(dest => dest.ViaPoints, opt => opt.MapFrom(src => 
                    !string.IsNullOrEmpty(src.ViaPoints) ? JsonSerializer.Deserialize<IEnumerable<double[]>>(src.ViaPoints, (JsonSerializerOptions)null) : null))
                .ForMember(dest => dest.GeometryPath, opt => opt.MapFrom(src =>
                    src.GeometryPath != null ? src.GeometryPath.Coordinates.Select(c => new[] { c.X, c.Y }) : null));

            // For UpdateRouteRequest -> Route (for partial updates)
            // Null members in the source will be ignored.
            CreateMap<UpdateRouteRequest, Route>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
                
            // If specific handling is needed for points in UpdateRouteRequest:
            // CreateMap<UpdateRouteRequest, Route>()
            //     .ForMember(dest => dest.OriginPoint, opt => opt.MapFrom((src, dest, destMember, context) => 
            //         src.OriginCoordinates != null ? context.Mapper.ConfigurationProvider.Host<IGeospatialService>().CreatePoint(src.OriginCoordinates[0], src.OriginCoordinates[1]) : dest.OriginPoint))
            //     .ForMember(dest => dest.DestinationPoint, opt => opt.MapFrom((src, dest, destMember, context) => 
            //         src.DestinationCoordinates != null ? context.Mapper.ConfigurationProvider.Host<IGeospatialService>().CreatePoint(src.DestinationCoordinates[0], src.DestinationCoordinates[1]) : dest.DestinationPoint))
            //     .ForMember(dest => dest.ViaPoints, opt => opt.MapFrom((src, dest) => 
            //         src.ViaPoints != null ? JsonSerializer.Serialize(src.ViaPoints, (JsonSerializerOptions)null) : dest.ViaPoints ))
            //     .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        }
    }

    // Helper to pass IGeospatialService to AutoMapper context
    // This is a common pattern but can be tricky. A simpler way is to inject IGeospatialService
    // into the service method and perform point creation/conversion there, then map simpler properties.
    // However, the prompt implies mapping directly.
    // For AutoMapper to resolve services like IGeospatialService, it needs access to the service provider.
    // This is typically handled by AddAutoMapper(typeof(MappingProfile)) if it registers services correctly
    // or by passing services explicitly during mapping execution if using ProjectTo or similar.
    // The .ForMember(..., ctx => ctx.Mapper.ConfigurationProvider.Host<IGeospatialService>()) approach
    // relies on the service being resolvable by AutoMapper, which usually means it's registered
    // with the DI container that AutoMapper uses.
    public static class AutoMapperExtensions
    {
        public static TService Host<TService>(this IConfigurationProvider config) where TService : class
        {
            return config.ServiceCtor.Invoke(typeof(TService)) as TService;
        }
    }
}
