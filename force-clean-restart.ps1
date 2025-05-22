# Force stop any running dotnet processes and clean up build artifacts
Write-Host "Stopping all dotnet processes..." -ForegroundColor Yellow

# Kill all dotnet processes
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping process with ID: $($_.Id)" -ForegroundColor Gray
    Stop-Process -Id $_.Id -Force
}

# Give processes time to fully stop
Start-Sleep -Seconds 5

# Clean up any locked files in Common.Messaging
Write-Host "Cleaning Common.Messaging build artifacts..." -ForegroundColor Yellow
$commonMessagingPath = Join-Path $PSScriptRoot "SharedLibraries\Common.Messaging"
if (Test-Path $commonMessagingPath) {
    Get-ChildItem -Path $commonMessagingPath -Include bin,obj -Directory -Recurse | ForEach-Object {
        Write-Host "Removing $($_.FullName)" -ForegroundColor Gray
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Force clean the solution
Write-Host "Cleaning and rebuilding Common.Messaging..." -ForegroundColor Yellow
dotnet clean $commonMessagingPath\Common.Messaging.csproj --configuration Debug
dotnet restore $commonMessagingPath\Common.Messaging.csproj
dotnet build $commonMessagingPath\Common.Messaging.csproj --no-restore

# Now restart the services
Write-Host "Starting services..." -ForegroundColor Green
& .\run-services.ps1 -Services "UserService", "TruckService", "RouteService", "ApiGateway"
