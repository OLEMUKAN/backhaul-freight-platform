namespace Common.Messaging
{
    public interface IEventPublisher
    {
        Task PublishAsync<T>(T eventMessage) where T : class;
    }
}
