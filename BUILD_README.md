# Build Scripts for Backhaul Freight Matching Platform

This document explains how to use the build scripts to compile the microservices in the correct order and optionally run them.

## Available Scripts

### 1. PowerShell Script (`build-services.ps1`) - Recommended

The PowerShell script provides more features and better error handling.

**Basic Usage:**
```powershell
# Build only
.\build-services.ps1

# Build and run services
.\build-services.ps1 -RunAfterBuild
```

**Advanced Usage:**
```powershell
# Build in Release mode
.\build-services.ps1 -Configuration Release

# Skip the clean step (faster builds)
.\build-services.ps1 -SkipClean

# Show verbose output including warnings
.\build-services.ps1 -Verbose

# Build and run services in Release mode
.\build-services.ps1 -Configuration Release -RunAfterBuild

# Combine all options
.\build-services.ps1 -Configuration Release -SkipClean -Verbose -RunAfterBuild
```

### 2. Batch Script (`build-services.bat`) - Alternative

Use this if PowerShell execution is restricted on your system.

**Basic Usage:**
```cmd
# Build only
build-services.bat

# Build and run services
build-services.bat Debug run
```

**With Configuration:**
```cmd
# Build in Release mode
build-services.bat Release

# Build in Release mode and run services
build-services.bat Release run
```

## What the Scripts Do

1. **Stop Dotnet Processes**: Terminates any running `dotnet.exe` processes to avoid file locks
2. **Create Log Structure**: Ensures the `Logs/` directory structure exists
3. **Build in Dependency Order**:
   - **Shared Libraries** (built first as dependencies):
     - MessageContracts
     - Common.Messaging
     - Common.Middleware
     - ServiceDiscovery
   - **Core Services** (depend on shared libraries):
     - UserService
     - TruckService
     - RouteService
   - **API Gateway** (built last, depends on services)

4. **Individual Logging**: Each service creates its own timestamped log file
5. **Optional Service Startup**: Run services after successful build with predefined ports

## Service Ports

When services are started, they use these default ports:

- **UserService**: http://localhost:5001
- **TruckService**: http://localhost:5002  
- **RouteService**: http://localhost:5003
- **ApiGateway**: http://localhost:5000

## Log Files

Build and runtime logs are now organized individually:

```
Logs/
├── SharedLibraries/
│   ├── MessageContracts_build_YYYY-MM-DD_HH-mm-ss.log
│   ├── Common.Messaging_build_YYYY-MM-DD_HH-mm-ss.log
│   ├── Common.Middleware_build_YYYY-MM-DD_HH-mm-ss.log
│   └── ServiceDiscovery_build_YYYY-MM-DD_HH-mm-ss.log
├── Services/
│   ├── UserService_build_YYYY-MM-DD_HH-mm-ss.log
│   ├── TruckService_build_YYYY-MM-DD_HH-mm-ss.log
│   └── RouteService_build_YYYY-MM-DD_HH-mm-ss.log
├── ApiGateway/
│   └── ApiGateway_build_YYYY-MM-DD_HH-mm-ss.log
└── Runtime/
    ├── UserService_runtime_YYYY-MM-DD_HH-mm-ss.log
    ├── TruckService_runtime_YYYY-MM-DD_HH-mm-ss.log
    ├── RouteService_runtime_YYYY-MM-DD_HH-mm-ss.log
    └── ApiGateway_runtime_YYYY-MM-DD_HH-mm-ss.log
```

## Build Order Rationale

The build order prevents common issues:

1. **Shared Libraries First**: All services depend on common contracts and utilities
2. **Services Second**: Each service can reference shared libraries
3. **API Gateway Last**: May reference service assemblies or shared libraries

## Running Services

### PowerShell Script
When using `-RunAfterBuild`, the script will:
1. Start each service in sequence with a 3-second delay between services
2. Display the running services with their URLs and Process IDs
3. Keep monitoring the services
4. Stop all services when you press `Ctrl+C`

### Batch Script
When using the `run` parameter:
1. Starts each service in a minimized window
2. Shows the service URLs
3. Services continue running in background
4. Use Task Manager or `taskkill` to stop services manually

## Troubleshooting

### PowerShell Execution Policy Error

If you get an execution policy error, run this in an elevated PowerShell:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Or run the script with bypass:
```powershell
powershell -ExecutionPolicy Bypass -File .\build-services.ps1
```

### Common Build Issues

1. **File Lock Errors**: The script stops dotnet processes, but you may need to close Visual Studio or other IDEs
2. **Missing Dependencies**: Ensure all NuGet packages are restored:
   ```cmd
   dotnet restore
   ```
3. **Path Not Found**: Verify the project structure matches the expected layout
4. **Port Conflicts**: If ports are already in use, modify the port assignments in the script

### Service Startup Issues

1. **Port Already in Use**: Check if other applications are using the ports (5000-5003)
2. **Missing appsettings**: Ensure each service has proper configuration files
3. **Database Connections**: Verify database connection strings if services require databases

### Manual Build Order

If you need to build manually:
```cmd
# 1. Shared Libraries
dotnet build SharedLibraries/MessageContracts
dotnet build SharedLibraries/Common.Messaging
dotnet build SharedLibraries/Common.Middleware
dotnet build SharedLibraries/ServiceDiscovery

# 2. Services
dotnet build Services/UserService
dotnet build Services/TruckService
dotnet build Services/RouteService

# 3. API Gateway
dotnet build ApiGateway
```

### Manual Service Startup

If you need to start services manually:
```cmd
# Start each service in separate command windows
dotnet run --project Services/UserService --urls "http://localhost:5001"
dotnet run --project Services/TruckService --urls "http://localhost:5002"
dotnet run --project Services/RouteService --urls "http://localhost:5003"
dotnet run --project ApiGateway --urls "http://localhost:5000"
```

## Features

- ✅ Automatic process termination
- ✅ Dependency-aware build order
- ✅ Individual timestamped logging per service
- ✅ Build status reporting
- ✅ Error handling and recovery
- ✅ Configurable build mode (Debug/Release)
- ✅ Clean build option
- ✅ Verbose output option (PowerShell)
- ✅ Color-coded output (PowerShell)
- ✅ **NEW**: Automatic service startup after build
- ✅ **NEW**: Service monitoring and graceful shutdown (PowerShell)
- ✅ **NEW**: Individual log files per service
- ✅ **NEW**: Runtime logging for running services

## Example Output

### Build Only
```
╔══════════════════════════════════════════════════════════════╗
║           Backhaul Freight Matching Platform                ║
║                    Build Script                             ║
╚══════════════════════════════════════════════════════════════╝

==> Ensuring logs directory structure exists...
✓ Created directory: Logs/Runtime

==> Stopping all running dotnet processes...
✓ No dotnet processes found running

==> Starting build process (Configuration: Debug)...

==> Building MessageContracts...
  Building project...
✓ MessageContracts built successfully
  Log: Logs/SharedLibraries/MessageContracts_build_2024-01-15_14-30-45.log

==> Building UserService...
  Building project...
✓ UserService built successfully
  Log: Logs/Services/UserService_build_2024-01-15_14-30-48.log

[... continues for each project ...]

==> Build Summary
Duration: 01:23

✓ MessageContracts
✓ Common.Messaging
✓ Common.Middleware
✓ ServiceDiscovery
✓ UserService
✓ TruckService
✓ RouteService
✓ ApiGateway

🎉 All projects built successfully! (8/8)

Individual log files are available in the Logs/ directory
```

### Build and Run
```
[... build output ...]

==> Starting services...

==> Starting UserService on port 5001...
✓ UserService started (PID: 12345)
  URL: http://localhost:5001
  Log: Logs/Runtime/UserService_runtime_2024-01-15_14-32-15.log
  PID: 12345

==> Starting TruckService on port 5002...
✓ TruckService started (PID: 12346)
  URL: http://localhost:5002
  Log: Logs/Runtime/TruckService_runtime_2024-01-15_14-32-18.log
  PID: 12346

[... continues for each service ...]

==> All services started!

Services running:
  • UserService: http://localhost:5001 (PID: 12345)
  • TruckService: http://localhost:5002 (PID: 12346)
  • RouteService: http://localhost:5003 (PID: 12347)
  • ApiGateway: http://localhost:5000 (PID: 12348)

Press Ctrl+C to stop all services
```

## Quick Commands

```powershell
# Most common usage
.\build-services.ps1 -RunAfterBuild

# Fast development cycle (skip clean, run after build)
.\build-services.ps1 -SkipClean -RunAfterBuild

# Production build and run
.\build-services.ps1 -Configuration Release -RunAfterBuild
``` 