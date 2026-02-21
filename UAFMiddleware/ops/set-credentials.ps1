param(
    [string]$Username,

    [Security.SecureString]$Password,

    [ValidateSet('TST', 'UAF')]
    [string]$Profile,

    [switch]$PersistEnvironment,

    [switch]$SkipRestart,

    [string]$MiddlewareServiceName = 'UAFSageMiddleware',

    [string]$LocalSettingsPath
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptRoot 'lib/common.ps1')

if ([string]::IsNullOrWhiteSpace($LocalSettingsPath)) {
    $rootDir = Split-Path $scriptRoot -Parent
    $LocalSettingsPath = Join-Path $rootDir 'appsettings.Local.json'
}

if ([string]::IsNullOrWhiteSpace($Profile)) {
    $config = Get-OrCreateLocalSettings -Path $LocalSettingsPath
    Ensure-Property -Object $config -PropertyName 'Sage' -DefaultValue ([pscustomobject]@{})
    Ensure-Property -Object $config.Sage -PropertyName 'Company' -DefaultValue 'TST'
    $Profile = $config.Sage.Company
}

$args = @('-Profile', $Profile)
if (-not [string]::IsNullOrWhiteSpace($Username)) { $args += @('-Username', $Username) }
if ($null -ne $Password) { $args += @('-Password', $Password) }
if ($PersistEnvironment) { $args += '-PersistEnvironment' }
if ($SkipRestart) { $args += '-SkipRestart' }
if (-not [string]::IsNullOrWhiteSpace($MiddlewareServiceName)) { $args += @('-MiddlewareServiceName', $MiddlewareServiceName) }
if (-not [string]::IsNullOrWhiteSpace($LocalSettingsPath)) { $args += @('-LocalSettingsPath', $LocalSettingsPath) }

& (Join-Path $scriptRoot 'set-company-profile.ps1') @args
exit $LASTEXITCODE
