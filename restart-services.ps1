# Stop any existing dotnet processes
Write-Host "Stopping any running services..." -ForegroundColor Yellow
$dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
if ($dotnetProcesses) {
    $dotnetProcesses | ForEach-Object { 
        Write-Host "Stopping process with ID: $($_.Id)" -ForegroundColor Gray
        $_ | Stop-Process -Force 
    }
    Start-Sleep -Seconds 2
}

# Run the services using the existing script
Write-Host "Starting services with fixed configuration..." -ForegroundColor Green
& .\run-services.ps1 -Services "UserService", "TruckService", "RouteService", "ApiGateway"

Write-Host "Services started. Check the logs for any errors." -ForegroundColor Green 