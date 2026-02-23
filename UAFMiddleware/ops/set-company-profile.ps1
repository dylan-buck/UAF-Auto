param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('TST', 'UAF')]
    [string]$Profile,

    [string]$Username,

    [Security.SecureString]$Password,

    [string]$ApiKey,

    [switch]$PersistEnvironment,

    [switch]$SkipRestart,

    [string]$MiddlewareServiceName = 'UAFSageMiddleware',

    [string]$LocalSettingsPath
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path $scriptRoot -Parent
. (Join-Path $scriptRoot 'lib/common.ps1')

$logFile = New-LogFilePath -Name 'set-company-profile.log'
$originalArgs = @($MyInvocation.UnboundArguments)
Ensure-Admin -ScriptPath $PSCommandPath -Arguments $originalArgs

if ([string]::IsNullOrWhiteSpace($LocalSettingsPath)) {
    $LocalSettingsPath = Join-Path $rootDir 'src\appsettings.Local.json'
}

if ([string]::IsNullOrWhiteSpace($Username)) {
    $Username = Read-Host 'Enter Sage username'
}

if ($null -eq $Password) {
    $Password = Read-Host 'Enter Sage password' -AsSecureString
}

$plainPassword = Convert-SecureStringToPlainText -SecureString $Password

if ([string]::IsNullOrWhiteSpace($Username) -or [string]::IsNullOrWhiteSpace($plainPassword)) {
    Write-OpsLog -Message 'Username and password are required' -Level 'ERROR' -LogFile $logFile
    exit 1
}

$config = Get-OrCreateLocalSettings -Path $LocalSettingsPath
Ensure-Property -Object $config -PropertyName 'Sage' -DefaultValue ([pscustomobject]@{})
Ensure-Property -Object $config -PropertyName 'Api' -DefaultValue ([pscustomobject]@{})
Ensure-Property -Object $config.Sage -PropertyName 'Username' -DefaultValue ''
Ensure-Property -Object $config.Sage -PropertyName 'Password' -DefaultValue ''
Ensure-Property -Object $config.Sage -PropertyName 'Company' -DefaultValue 'TST'
Ensure-Property -Object $config.Api -PropertyName 'ApiKey' -DefaultValue ''

$config.Sage.Username = $Username
$config.Sage.Password = $plainPassword
$config.Sage.Company = $Profile

if (-not [string]::IsNullOrWhiteSpace($ApiKey)) {
    $config.Api.ApiKey = $ApiKey
}

Save-LocalSettings -Config $config -Path $LocalSettingsPath
Write-OpsLog -Message "Updated local settings at '$LocalSettingsPath' for profile '$Profile'" -LogFile $logFile

if ($PersistEnvironment) {
    [Environment]::SetEnvironmentVariable('Sage__Username', $Username, 'Machine')
    [Environment]::SetEnvironmentVariable('Sage__Password', $plainPassword, 'Machine')
    [Environment]::SetEnvironmentVariable('Sage__Company', $Profile, 'Machine')

    if (-not [string]::IsNullOrWhiteSpace($ApiKey)) {
        [Environment]::SetEnvironmentVariable('Api__ApiKey', $ApiKey, 'Machine')
    }

    Write-OpsLog -Message 'Updated machine environment variables for Sage profile' -LogFile $logFile
}

if (-not $SkipRestart) {
    Write-OpsLog -Message "Restarting service '$MiddlewareServiceName'" -LogFile $logFile
    if (-not (Stop-ServiceSafe -Name $MiddlewareServiceName -TimeoutSeconds 45)) {
        Write-OpsLog -Message "Service '$MiddlewareServiceName' did not stop cleanly" -Level 'WARN' -LogFile $logFile
    }

    if (-not (Ensure-ServiceRunning -Name $MiddlewareServiceName -TimeoutSeconds 45)) {
        Write-OpsLog -Message "Service '$MiddlewareServiceName' failed to start" -Level 'ERROR' -LogFile $logFile
        exit 1
    }

    $healthOk = Invoke-ReadinessCheck -Uri 'http://localhost:3000/health/ready' -Retries 6 -DelaySeconds 5 -LogFile $logFile
    if (-not $healthOk) {
        Write-OpsLog -Message 'Profile switch completed but readiness check failed' -Level 'ERROR' -LogFile $logFile
        exit 1
    }
}

Write-OpsLog -Message "Profile '$Profile' applied successfully" -LogFile $logFile
exit 0
