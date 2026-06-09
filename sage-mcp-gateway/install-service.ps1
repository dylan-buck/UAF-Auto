param(
  [string]$ServiceName = "UAFSageMcpGateway",
  [string]$InstallPath = (Resolve-Path ".").Path,
  [string]$NodePath = "",
  [string]$WinSWPath = "",
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

if ($EnableCreateTools -and [string]::IsNullOrWhiteSpace($CreateApiKey)) {
  throw "EnableCreateTools requires CreateApiKey."
}

if ($EnableFinanceTools -and [string]::IsNullOrWhiteSpace($FinanceApiKey)) {
  throw "EnableFinanceTools requires FinanceApiKey."
}

Push-Location $InstallPath
try {
  npm ci
  npm run build
} finally {
  Pop-Location
}

$wrapperPath = Join-Path $InstallPath "$ServiceName.exe"
$configPath = Join-Path $InstallPath "$ServiceName.xml"
$logPath = Join-Path $InstallPath "logs"
New-Item -ItemType Directory -Path $logPath -Force | Out-Null

if (-not (Test-Path $wrapperPath)) {
  if (-not [string]::IsNullOrWhiteSpace($WinSWPath)) {
    Copy-Item $WinSWPath $wrapperPath
  } else {
    $release = Invoke-RestMethod "https://api.github.com/repos/winsw/winsw/releases/latest"
    $asset = $release.assets | Where-Object { $_.name -eq "WinSW-x64.exe" } | Select-Object -First 1
    if (-not $asset) {
      throw "Could not locate WinSW-x64.exe in the latest WinSW release. Download WinSW manually and pass -WinSWPath."
    }
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $wrapperPath
  }
}

$entryPoint = Join-Path $InstallPath "dist\index.js"

function Escape-Xml([string]$Value) {
  return [System.Security.SecurityElement]::Escape($Value)
}

$envXml = @(
  @{ name = "MCP_HOST"; value = $McpHost },
  @{ name = "MCP_PORT"; value = "$McpPort" },
  @{ name = "MCP_ALLOWED_HOSTS"; value = $AllowedHosts },
  @{ name = "UAF_SAGE_API_URL"; value = $SageApiUrl },
  @{ name = "UAF_SAGE_READ_API_KEY"; value = $ReadApiKey },
  @{ name = "UAF_SAGE_CREATE_API_KEY"; value = $CreateApiKey },
  @{ name = "UAF_SAGE_FINANCE_API_KEY"; value = $FinanceApiKey },
  @{ name = "ENABLE_CREATE_TOOLS"; value = "$($EnableCreateTools.IsPresent)".ToLowerInvariant() },
  @{ name = "ENABLE_FINANCE_TOOLS"; value = "$($EnableFinanceTools.IsPresent)".ToLowerInvariant() },
  @{ name = "MCP_SHARED_SECRET"; value = $McpSharedSecret }
) | ForEach-Object {
  "  <env name=""$(Escape-Xml $_.name)"" value=""$(Escape-Xml $_.value)"" />"
}

@"
<service>
  <id>$(Escape-Xml $ServiceName)</id>
  <name>UAF Sage MCP Gateway</name>
  <description>Streamable HTTP MCP gateway for the UAF Sage 100 middleware.</description>
  <executable>$(Escape-Xml $NodePath)</executable>
  <arguments>"$(Escape-Xml $entryPoint)"</arguments>
  <workingdirectory>$(Escape-Xml $InstallPath)</workingdirectory>
  <logpath>$(Escape-Xml $logPath)</logpath>
  <log mode="roll-by-size">
    <sizeThreshold>10485760</sizeThreshold>
    <keepFiles>8</keepFiles>
  </log>
  <onfailure action="restart" delay="5 sec" />
  <onfailure action="restart" delay="15 sec" />
  <onfailure action="restart" delay="30 sec" />
$($envXml -join "`r`n")
</service>
"@ | Set-Content -Path $configPath -Encoding UTF8

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
  & $wrapperPath stop
  & $wrapperPath uninstall
}

& $wrapperPath install
& $wrapperPath start

Write-Host "Installed and started $ServiceName on http://$McpHost`:$McpPort/mcp"
