# Set NuGet Configuration for bypassing proxy
# This script creates a local NuGet.Config file for this solution

$configContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <config>
    <!-- Disable the proxy configuration -->
    <add key="http_proxy" value="" />
    <add key="https_proxy" value="" />
    <add key="no_proxy" value="localhost,127.0.0.1" />
  </config>
</configuration>
"@

# Write the NuGet.Config file to the solution directory
$configContent | Out-File -FilePath "D:\Coding\frieght\Services\RouteService\NuGet.Config" -Encoding UTF8

Write-Host "Created NuGet.Config file in the RouteService directory"

# Configure global NuGet proxy settings
dotnet nuget update source nuget.org --no-proxy

Write-Host "NuGet proxy settings updated. Try building the project again."
