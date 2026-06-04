param(
  [string]$ServiceName = "UAFSageMcpGateway",
  [string]$InstallPath = (Resolve-Path ".").Path,
  [string]$NodePath = "",
  [string]$McpHost = "127.0.0.1",
  [int]$McpPort = 8787,
  [string]$AllowedHosts = "127.0.0.1,localhost",
  [string]$SageApiUrl = "http://localhost:3000",
  [Parameter(Mandatory = $true)]
  [string]$ReadApiKey,
  [string]$CreateApiKey = "",
  [string]$FinanceApiKey = "",
  [string]$McpSharedSecret = "",
  [switch]$EnableCreateTools,
  [switch]$EnableFinanceTools
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
  throw "Run this script from an elevated PowerShell prompt."
}

if ([string]::IsNullOrWhiteSpace($NodePath)) {
  $NodePath = (Get-Command node.exe).Source
}

Push-Location $InstallPath
try {
  npm ci
  npm run build
} finally {
  Pop-Location
}

[Environment]::SetEnvironmentVariable("MCP_HOST", $McpHost, "Machine")
[Environment]::SetEnvironmentVariable("MCP_PORT", "$McpPort", "Machine")
[Environment]::SetEnvironmentVariable("MCP_ALLOWED_HOSTS", $AllowedHosts, "Machine")
[Environment]::SetEnvironmentVariable("UAF_SAGE_API_URL", $SageApiUrl, "Machine")
[Environment]::SetEnvironmentVariable("UAF_SAGE_READ_API_KEY", $ReadApiKey, "Machine")
[Environment]::SetEnvironmentVariable("UAF_SAGE_CREATE_API_KEY", $CreateApiKey, "Machine")
[Environment]::SetEnvironmentVariable("UAF_SAGE_FINANCE_API_KEY", $FinanceApiKey, "Machine")
[Environment]::SetEnvironmentVariable("ENABLE_CREATE_TOOLS", "$($EnableCreateTools.IsPresent)".ToLowerInvariant(), "Machine")
[Environment]::SetEnvironmentVariable("ENABLE_FINANCE_TOOLS", "$($EnableFinanceTools.IsPresent)".ToLowerInvariant(), "Machine")
[Environment]::SetEnvironmentVariable("MCP_SHARED_SECRET", $McpSharedSecret, "Machine")

$entryPoint = Join-Path $InstallPath "dist\index.js"
$binPath = "`"$NodePath`" `"$entryPoint`""

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
  Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue
  sc.exe delete $ServiceName | Out-Null
  Start-Sleep -Seconds 2
}

sc.exe create $ServiceName binPath= $binPath start= delayed-auto DisplayName= "UAF Sage MCP Gateway" | Out-Null
sc.exe failure $ServiceName reset= 60 actions= restart/5000/restart/15000/restart/30000 | Out-Null
Start-Service -Name $ServiceName

Write-Host "Installed and started $ServiceName on http://$McpHost`:$McpPort/mcp"
