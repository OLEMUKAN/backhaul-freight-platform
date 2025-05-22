# Technical Debt

This document outlines known technical debt in the Backhaul Platform microservices application. Addressing these items will improve maintainability, security, and overall code quality.

## Configuration Management

The current configuration management practices across services have several areas for improvement to enhance security, reduce duplication, and improve consistency.

*   **Hardcoded `Jwt:Key`:**
    *   **Issue:** The `Jwt:Key` in `appsettings.json` for `TruckService` and `UserService` is hardcoded. This is a significant security risk as the key is checked into source control.
    *   **Recommendation:** This key should be a strong, unique, randomly generated secret. It must be stored securely (e.g., using environment variables, Azure Key Vault, HashiCorp Vault, or other secret management tools) and not checked into source control. Each service instance should fetch this key from the secure store at runtime.

*   **Duplicated `Serilog` Configuration:**
    *   **Issue:** The Serilog logging setup, including configuration in `appsettings.json` and potentially setup code in `Program.cs`, is largely duplicated across `TruckService` and `UserService`.
    *   **Recommendation:** Consider creating a shared logging library (e.g., `Common.Logging`). This library could provide an extension method (e.g., `builder.Host.AddStandardSerilog(string serviceName)`) to standardize Serilog configuration across all services. This would reduce boilerplate code, ensure consistent logging practices (e.g., enrichers, sinks), and make future updates to logging behavior easier to manage.

*   **Duplicated `RabbitMQ` Settings:**
    *   **Issue:** RabbitMQ connection details (`Host`, `Username`, `Password`, `Port`) are duplicated in the `appsettings.json` files of `TruckService` and `UserService`.
    *   **Recommendation:** These settings should be externalized from `appsettings.json`. Options include using environment variables, a centralized configuration server (like Azure App Configuration), or a secrets manager for sensitive parts like the password. This approach simplifies configuration management, especially in different environments (dev, staging, prod), and avoids hardcoding credentials.

*   **Duplicated `ServiceRegistry:Services`:**
    *   **Issue:** The entire `ServiceRegistry:Services` section, which contains the base URLs for all microservices, is duplicated in every service's `appsettings.json`. This is error-prone, difficult to manage when services are added or updated, and does not support dynamic environments well.
    *   **Recommendation:** Strongly consider moving to a proper service discovery mechanism. Options include:
        *   Dedicated service discovery tools like Consul or Eureka.
        *   Cloud-native solutions if applicable (e.g., Azure Service Fabric, Kubernetes service discovery).
        *   Leveraging a centralized configuration store like Azure App Configuration to store service addresses, which services can then query.
    This will allow for dynamic service registration and discovery, making the system more resilient and easier to scale. The current `ServiceDiscovery` library seems to implement a client-side, configuration-based discovery which is a step, but the configuration itself needs to be centralized.

*   **Inconsistent `RateLimiting` Configuration:**
    *   **Issue:** The structure and specific settings for rate limiting configuration (e.g., sections in `appsettings.json`, options classes) appear to differ between `TruckService` and `UserService`.
    *   **Recommendation:** Review the rate limiting strategies and configurations for both services. If they use the same underlying rate limiting library (e.g., `AspNetCoreRateLimit`), strive for a consistent configuration structure and approach. While specific limits might differ based on service needs, the way these limits are defined and loaded should be standardized to improve clarity and maintainability. If the needs are genuinely very different, document why the chosen configurations diverge.

## Automated Testing

*   **Lack of Unit Tests:** No dedicated unit test projects (e.g., `*.Tests.csproj`) were found for `TruckService` or `UserService`. While Postman collections exist for API testing, these do not replace unit tests for validating individual components, business logic, and edge cases in C# code.
*   **Recommendation:** Implement comprehensive unit testing for all services. Start by adding unit tests for the newly created shared libraries: `Common.Middleware` (testing the `ExceptionHandlingMiddleware`) and `Common.Messaging` (testing the `EventPublisher`). Subsequently, add tests for controllers, services, and other logic within `TruckService.API` and `UserService.API`.
*   **Benefits:** Unit tests will improve code quality, reduce regressions, facilitate safer refactoring, and provide living documentation for component behavior.
