param (
    [Parameter(ValueFromRemainingArguments=$true)]
    [string[]]$Services = @("UserService", "TruckService", "ApiGateway")
)

$ErrorActionPreference = 'Stop'

# Define service paths
$servicePaths = @{
    "UserService" = "Services\UserService\UserService.API\UserService.API.csproj"
    "TruckService" = "Services\TruckService\TruckService.API\TruckService.API.csproj"
    "RouteService" = "Services\RouteService\RouteService.API\RouteService.API.csproj"
    "ApiGateway" = "ApiGateway\ApiGateway.csproj"
}

# Define shared library paths
$sharedLibraries = @(
    "SharedLibraries\ServiceDiscovery\ServiceDiscovery.csproj",
    "Services\Common\SharedSettings\SharedSettings.csproj",
    "SharedLibraries\MessageContracts\MessageContracts.csproj"
)

# Default to all services if none specified
if ($Services.Count -eq 0) {
    # Start the services in the proper order - microservices first, then API gateway
    $Services = @("UserService", "TruckService", "RouteService", "ApiGateway")
}

# If all services are selected and the order wasn't explicitly specified,
# make sure they're started in the optimal order
if ($Services.Count -eq 4 -and 
    $Services -contains "UserService" -and 
    $Services -contains "TruckService" -and 
    $Services -contains "RouteService" -and 
    $Services -contains "ApiGateway") {
    $Services = @("UserService", "TruckService", "RouteService", "ApiGateway")
}

# Check if services exist
foreach ($service in $Services) {
    if (-not $servicePaths.ContainsKey($service)) {
        Write-Error "Service '$service' not found. Available services: $($servicePaths.Keys -join ', ')"
        exit 1
    }
}

# Create temp directory for logs
$logDir = Join-Path $PSScriptRoot "Logs"
if (-not (Test-Path $logDir)) {
    New-Item -Path $logDir -ItemType Directory | Out-Null
}

# Function to build shared libraries
function Build-SharedLibraries {
    Write-Host "Building shared libraries to prevent file locking issues..." -ForegroundColor Cyan
    
    foreach ($library in $sharedLibraries) {
        $fullPath = Join-Path $PSScriptRoot $library
        Write-Host "Building $library..." -ForegroundColor Gray
        
        # Build the library
        $buildOutput = dotnet build $fullPath --configuration Debug
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to build $library. Some services might fail to start."
        } else {
            Write-Host "Successfully built $library" -ForegroundColor Green
        }
        
        # Small delay to let file handles close
        Start-Sleep -Seconds 1
    }
}

# Function to run a service
function Start-ServiceProcess {
    param (
        [string]$ServiceName,
        [string]$ProjectPath
    )

    $fullPath = Join-Path $PSScriptRoot $ProjectPath
    $logFile = Join-Path $logDir "$ServiceName.log"

    $process = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", $fullPath -RedirectStandardOutput $logFile -RedirectStandardError "$logFile.error.txt" -PassThru -NoNewWindow
    
    return @{
        Name = $ServiceName
        Process = $process
        LogFile = $logFile
    }
}

# Build shared libraries first
Build-SharedLibraries

# Start each service with a delay between them
$runningServices = @()
foreach ($service in $Services) {
    Write-Host "Starting $service..." -ForegroundColor Cyan
    $serviceInfo = Start-ServiceProcess -ServiceName $service -ProjectPath $servicePaths[$service]
    $runningServices += $serviceInfo
    
    Write-Host "$service started with PID: $($serviceInfo.Process.Id)" -ForegroundColor Green
    Write-Host "Log file: $($serviceInfo.LogFile)" -ForegroundColor Gray
    Write-Host ""
    
    # Add delay between starting services to prevent file locking conflicts
    Write-Host "Waiting 5 seconds before starting next service..." -ForegroundColor Gray
    Start-Sleep -Seconds 5
}

# Wait for all services
Write-Host "All services are running. Press Ctrl+C to stop all services." -ForegroundColor Yellow

try {
    # Keep script running until user cancels
    while ($true) {
        # Check if any service has exited
        $exitedServices = $runningServices | Where-Object { $_.Process.HasExited }
        foreach ($service in $exitedServices) {
            Write-Host "$($service.Name) exited with code: $($service.Process.ExitCode)" -ForegroundColor Red
            $runningServices = $runningServices | Where-Object { $_.Name -ne $service.Name }
        }
        
        if ($runningServices.Count -eq 0) {
            Write-Host "All services have stopped." -ForegroundColor Yellow
            break
        }
        
        Start-Sleep -Seconds 1
    }
}
finally {
    # Clean up on script exit
    foreach ($service in $runningServices) {
        if (-not $service.Process.HasExited) {
            Write-Host "Stopping $($service.Name)..." -ForegroundColor Cyan
            $service.Process.Kill()
        }
    }
    
    Write-Host "All services stopped." -ForegroundColor Green
}
