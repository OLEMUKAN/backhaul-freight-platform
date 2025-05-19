using System.Threading.Tasks;

namespace UserService.API.Services
{
    public interface IEventPublisher
    {
        Task PublishAsync<T>(T eventMessage) where T : class;
    }
} 