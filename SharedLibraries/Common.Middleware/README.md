# Common.Middleware

This library provides common middleware components for use across different services.

## Features

*   **ExceptionHandlingMiddleware:** A centralized middleware for consistent error handling and response formatting across APIs. It catches unhandled exceptions and transforms them into standardized JSON error responses.

## Usage

Register the middleware in your service's `Program.cs` (or `Startup.cs`):

```csharp
// Program.cs
using Common.Middleware; // Or the correct namespace if different

// ... other builder configurations ...

var app = builder.Build();

// Register the exception handling middleware
// This should typically be one of the first middleware components in the pipeline
app.UseExceptionHandling();

// ... other app configurations ...
```
