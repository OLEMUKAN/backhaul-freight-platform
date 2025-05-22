# Common.Messaging

This library provides common messaging components, such as a generic event publisher, for use across different services.

## Features

*   **Generic Event Publisher (`IEventPublisher`, `EventPublisher`):** A generic interface and implementation for publishing events to a message broker (e.g., RabbitMQ via MassTransit). It allows publishing any event type that inherits from a base class (or is a `class`) without needing specific publish methods for each event.

## Usage

1.  **Register the Event Publisher** in your service's `Program.cs` (or dependency injection setup):
    ```csharp
    // Program.cs
    using Common.Messaging; // Or the correct namespace if different

    builder.Services.AddScoped<IEventPublisher, EventPublisher>();
    // Ensure MassTransit and other necessary dependencies for IPublishEndpoint are also registered.
    ```

2.  **Inject `IEventPublisher`** into your services/classes where you need to publish events:
    ```csharp
    public class MyService
    {
        private readonly IEventPublisher _eventPublisher;

        public MyService(IEventPublisher eventPublisher)
        {
            _eventPublisher = eventPublisher;
        }

        public async Task DoSomethingAndPublishEventAsync(MyEventData data)
        {
            var anEvent = new MySpecificEvent
            {
                // ... populate event properties ...
                Timestamp = DateTime.UtcNow
            };
            await _eventPublisher.PublishAsync<MySpecificEvent>(anEvent);
        }
    }
    ```
Make sure your event classes (e.g., `MySpecificEvent`) are defined, typically in a shared library like `MessageContracts`.
