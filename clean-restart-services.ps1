# Clean and restart services
Write-Host "Stopping any running services..." -ForegroundColor Yellow
$dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
if ($dotnetProcesses) {
    $dotnetProcesses | ForEach-Object { 
        Write-Host "Stopping process with ID: $($_.Id)" -ForegroundColor Gray
        $_ | Stop-Process -Force 
    }
    Start-Sleep -Seconds 2
}

# Clean bin and obj folders
Write-Host "Cleaning solution..." -ForegroundColor Yellow
$foldersToClean = @(
    "Services\UserService",
    "Services\TruckService",
    "Services\RouteService",
    "ApiGateway",
    "SharedLibraries\Common.Middleware",
    "SharedLibraries\Common.Messaging",
    "SharedLibraries\MessageContracts",
    "SharedLibraries\ServiceDiscovery",
    "Services\Common\SharedSettings"
)

foreach ($folder in $foldersToClean) {
    $path = Join-Path $PSScriptRoot $folder
    Get-ChildItem -Path $path -Include bin,obj -Directory -Recurse | ForEach-Object {
        Write-Host "Cleaning $($_.FullName)" -ForegroundColor Gray
        Remove-Item $_.FullName -Recurse -Force
    }
}

Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore

# Run the services using the existing script
Write-Host "Starting services with fixed configuration..." -ForegroundColor Green
& .\run-services.ps1 -Services "UserService", "TruckService", "RouteService", "ApiGateway"

Write-Host "Services started. Check the logs for any errors." -ForegroundColor Green
