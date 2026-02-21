param(
    [string]$MiddlewareServiceName = 'UAFSageMiddleware',
    [string]$ProjectRelativePath = 'src\UAFMiddleware.csproj',
    [string]$PublishRuntime = 'win-x64',
    [string]$Configuration = 'Release',
    [switch]$PullLatest,
    [switch]$SkipTunnelCheck,
    [int]$HealthRetries = 8,
    [int]$HealthDelaySeconds = 5,
    [string]$LocalHealthUrl = 'http://localhost:3000/health/ready',
    [string]$TunnelHealthUrl = 'https://sage.uaf-automation.uk/health/ready'
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path $scriptRoot -Parent
. (Join-Path $scriptRoot 'lib/common.ps1')

$logFile = New-LogFilePath -Name 'update-middleware.log'
$originalArgs = @($MyInvocation.UnboundArguments)
Ensure-Admin -ScriptPath $PSCommandPath -Arguments $originalArgs

Write-OpsLog -Message 'Starting middleware update workflow' -LogFile $logFile

$serviceInfo = Get-ServiceInfoByNames -Names @($MiddlewareServiceName)
if (-not $serviceInfo.Exists) {
    Write-OpsLog -Message "Service '$MiddlewareServiceName' is not installed" -Level 'ERROR' -LogFile $logFile
    exit 1
}

$serviceExe = Get-ServiceExecutablePath -ServiceName $serviceInfo.Name
if ([string]::IsNullOrWhiteSpace($serviceExe)) {
    Write-OpsLog -Message "Unable to resolve service binary path for '$($serviceInfo.Name)'" -Level 'ERROR' -LogFile $logFile
    exit 1
}

$installDir = Split-Path -Parent $serviceExe
if (-not (Test-Path -LiteralPath $installDir)) {
    Write-OpsLog -Message "Install directory not found: $installDir" -Level 'ERROR' -LogFile $logFile
    exit 1
}

$projectPath = Join-Path $repoRoot $ProjectRelativePath
if (-not (Test-Path -LiteralPath $projectPath)) {
    Write-OpsLog -Message "Project file not found: $projectPath" -Level 'ERROR' -LogFile $logFile
    exit 1
}

$publishDir = Join-Path $repoRoot 'publish'
$backupRoot = Join-Path $installDir 'backups'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupDir = Join-Path $backupRoot $timestamp

Ensure-Directory -Path $backupRoot
Write-OpsLog -Message "Creating backup: $backupDir" -LogFile $logFile
$backupCode = (robocopy $installDir $backupDir /E /R:1 /W:1 /XD logs backups /NFL /NDL /NJH /NJS /NP | Out-Null; $LASTEXITCODE)
if ($backupCode -gt 7) {
    Write-OpsLog -Message "Backup failed with robocopy code $backupCode" -Level 'ERROR' -LogFile $logFile
    exit 1
}

try {
    if ($PullLatest) {
        if (Test-Path -LiteralPath (Join-Path $repoRoot '.git')) {
            Write-OpsLog -Message 'Pulling latest changes from git' -LogFile $logFile
            Push-Location $repoRoot
            try {
                git pull
            }
            finally {
                Pop-Location
            }
        }
        else {
            Write-OpsLog -Message 'PullLatest was set but repository does not contain .git metadata; skipping pull' -Level 'WARN' -LogFile $logFile
        }
    }

    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -Path $publishDir -Recurse -Force
    }

    Write-OpsLog -Message 'Publishing middleware binaries' -LogFile $logFile
    dotnet publish $projectPath -c $Configuration -r $PublishRuntime --self-contained false -o $publishDir

    Write-OpsLog -Message "Stopping service '$($serviceInfo.Name)'" -LogFile $logFile
    if (-not (Stop-ServiceSafe -Name $serviceInfo.Name -TimeoutSeconds 45)) {
        throw "Failed to stop service '$($serviceInfo.Name)'"
    }

    Write-OpsLog -Message "Deploying binaries to '$installDir'" -LogFile $logFile
    $deployCode = (robocopy $publishDir $installDir /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null; $LASTEXITCODE)
    if ($deployCode -gt 7) {
        throw "Deploy failed with robocopy code $deployCode"
    }

    Write-OpsLog -Message "Starting service '$($serviceInfo.Name)'" -LogFile $logFile
    if (-not (Ensure-ServiceRunning -Name $serviceInfo.Name -TimeoutSeconds 45)) {
        throw "Failed to start service '$($serviceInfo.Name)' after deployment"
    }

    $localHealthOk = Invoke-ReadinessCheck -Uri $LocalHealthUrl -Retries $HealthRetries -DelaySeconds $HealthDelaySeconds -LogFile $logFile
    if (-not $localHealthOk) {
        throw "Local health check failed after update: $LocalHealthUrl"
    }

    if (-not $SkipTunnelCheck) {
        $tunnelHealthOk = Invoke-ReadinessCheck -Uri $TunnelHealthUrl -Retries $HealthRetries -DelaySeconds $HealthDelaySeconds -LogFile $logFile
        if (-not $tunnelHealthOk) {
            throw "Tunnel health check failed after update: $TunnelHealthUrl"
        }
    }

    Write-OpsLog -Message 'Middleware update completed successfully' -LogFile $logFile
    exit 0
}
catch {
    Write-OpsLog -Message "Update failed: $($_.Exception.Message)" -Level 'ERROR' -LogFile $logFile

    Write-OpsLog -Message 'Attempting rollback from latest backup' -Level 'WARN' -LogFile $logFile
    try {
        if (-not (Stop-ServiceSafe -Name $serviceInfo.Name -TimeoutSeconds 30)) {
            Write-OpsLog -Message "Service '$($serviceInfo.Name)' did not stop cleanly before rollback" -Level 'WARN' -LogFile $logFile
        }

        $rollbackCode = (robocopy $backupDir $installDir /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null; $LASTEXITCODE)
        if ($rollbackCode -gt 7) {
            throw "Rollback failed with robocopy code $rollbackCode"
        }

        if (-not (Ensure-ServiceRunning -Name $serviceInfo.Name -TimeoutSeconds 45)) {
            throw "Rollback restore succeeded but service '$($serviceInfo.Name)' failed to restart"
        }

        Write-OpsLog -Message 'Rollback completed; service restored' -Level 'WARN' -LogFile $logFile
    }
    catch {
        Write-OpsLog -Message "Rollback failed: $($_.Exception.Message)" -Level 'ERROR' -LogFile $logFile
    }

    exit 1
}
