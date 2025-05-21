param (
    [Parameter(ValueFromRemainingArguments=$true)]
    [string[]]$Services = @("UserService", "TruckService")
)

$ErrorActionPreference = 'Stop'

# Define service paths
$servicePaths = @{
    "UserService" = "Services\UserService\UserService.API\UserService.API.csproj"
    "TruckService" = "Services\TruckService\TruckService.API\TruckService.API.csproj"
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

# Start each service
$runningServices = @()
foreach ($service in $Services) {
    Write-Host "Starting $service..." -ForegroundColor Cyan
    $serviceInfo = Start-ServiceProcess -ServiceName $service -ProjectPath $servicePaths[$service]
    $runningServices += $serviceInfo
    
    Write-Host "$service started with PID: $($serviceInfo.Process.Id)" -ForegroundColor Green
    Write-Host "Log file: $($serviceInfo.LogFile)" -ForegroundColor Gray
    Write-Host ""
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
