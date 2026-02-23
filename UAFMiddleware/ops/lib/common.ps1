Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:OpsLogRoot = 'C:\UAF-Auto\logs\ops'

function Ensure-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -Path $Path -ItemType Directory -Force | Out-Null
    }
}

function New-LogFilePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    Ensure-Directory -Path $script:OpsLogRoot
    return Join-Path $script:OpsLogRoot $Name
}

function Write-OpsLog {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,

        [ValidateSet('INFO', 'WARN', 'ERROR')]
        [string]$Level = 'INFO',

        [string]$LogFile
    )

    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $line = "[$timestamp] [$Level] $Message"
    Write-Host $line

    if (-not [string]::IsNullOrWhiteSpace($LogFile)) {
        Ensure-Directory -Path (Split-Path -Parent $LogFile)
        Add-Content -Path $LogFile -Value $line
    }
}

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-Admin {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,

        [string[]]$Arguments = @()
    )

    if (Test-IsAdmin) {
        return
    }

    throw "Administrator privileges are required. Run the corresponding .cmd wrapper or start PowerShell as Administrator."
}

function Get-ServiceInfoByNames {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Names
    )

    foreach ($name in $Names) {
        $service = Get-Service -Name $name -ErrorAction SilentlyContinue
        if ($null -ne $service) {
            return [pscustomobject]@{
                Exists  = $true
                Name    = $service.Name
                Status  = $service.Status.ToString()
                Service = $service
            }
        }
    }

    return [pscustomobject]@{
        Exists  = $false
        Name    = $Names[0]
        Status  = 'Missing'
        Service = $null
    }
}

function Ensure-ServiceRunning {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [int]$TimeoutSeconds = 30
    )

    $service = Get-Service -Name $Name -ErrorAction Stop
    if ($service.Status -eq 'Running') {
        return $true
    }

    Start-Service -Name $Name -ErrorAction Stop

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Seconds 1
        $service.Refresh()
    } while ($service.Status -ne 'Running' -and (Get-Date) -lt $deadline)

    return $service.Status -eq 'Running'
}

function Stop-ServiceSafe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [int]$TimeoutSeconds = 30
    )

    $service = Get-Service -Name $Name -ErrorAction Stop
    if ($service.Status -eq 'Stopped') {
        return $true
    }

    Stop-Service -Name $Name -ErrorAction Stop

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Seconds 1
        $service.Refresh()
    } while ($service.Status -ne 'Stopped' -and (Get-Date) -lt $deadline)

    return $service.Status -eq 'Stopped'
}

function Invoke-ReadinessCheck {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri,

        [int]$Retries = 6,

        [int]$DelaySeconds = 5,

        [int]$TimeoutSeconds = 10,

        [string]$ExpectedStatus = 'ready',

        [string]$LogFile
    )

    for ($attempt = 1; $attempt -le $Retries; $attempt++) {
        try {
            $response = Invoke-RestMethod -Uri $Uri -TimeoutSec $TimeoutSeconds

            if ([string]::IsNullOrWhiteSpace($ExpectedStatus)) {
                Write-OpsLog -Message "Health check succeeded for $Uri on attempt $attempt" -LogFile $LogFile
                return $true
            }

            if ($response.status -eq $ExpectedStatus) {
                Write-OpsLog -Message "Health check succeeded for $Uri (status=$ExpectedStatus) on attempt $attempt" -LogFile $LogFile
                return $true
            }

            Write-OpsLog -Message "Health check attempt $attempt for $Uri returned status '$($response.status)'" -Level 'WARN' -LogFile $LogFile
        }
        catch {
            $statusCode = $null
            $statusDescription = $null
            try {
                if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
                    $statusCode = [int]$_.Exception.Response.StatusCode
                    $statusDescription = $_.Exception.Response.StatusDescription
                }
            }
            catch {
                # best effort
            }

            if ($null -ne $statusCode) {
                $statusText = if ([string]::IsNullOrWhiteSpace($statusDescription)) { '' } else { " $statusDescription" }
                Write-OpsLog -Message "Health check attempt $attempt failed for ${Uri}: HTTP $statusCode$statusText" -Level 'WARN' -LogFile $LogFile
            }
            else {
                Write-OpsLog -Message "Health check attempt $attempt failed for ${Uri}: $($_.Exception.Message)" -Level 'WARN' -LogFile $LogFile
            }
        }

        if ($attempt -lt $Retries) {
            Start-Sleep -Seconds $DelaySeconds
        }
    }

    return $false
}

function Get-ServiceExecutablePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServiceName
    )

    $qc = sc.exe qc $ServiceName 2>$null
    if (-not $qc) {
        return $null
    }

    $line = $qc | Select-String -Pattern 'BINARY_PATH_NAME\s*:\s*(.+)$' | Select-Object -First 1
    if ($null -eq $line) {
        return $null
    }

    $rawPath = $line.Matches[0].Groups[1].Value.Trim()

    if ($rawPath.StartsWith('"')) {
        $trimmed = $rawPath.TrimStart('"')
        $quoteIndex = $trimmed.IndexOf('"')
        if ($quoteIndex -ge 0) {
            return $trimmed.Substring(0, $quoteIndex)
        }

        return $trimmed
    }

    return ($rawPath -split '\s+')[0]
}

function Ensure-ScheduledTaskStartupVerifier {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TaskName,

        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,

        [string]$LogFile
    )

    $escapedScriptPath = '"' + $ScriptPath + '"'
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File $escapedScriptPath"
    $trigger = New-ScheduledTaskTrigger -AtStartup
    $trigger.Delay = 'PT2M'
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest -User 'SYSTEM' -Force | Out-Null
    Write-OpsLog -Message "Scheduled task '$TaskName' registered/updated" -LogFile $LogFile
}

function Get-OrCreateLocalSettings {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path -LiteralPath $Path) {
        $json = Get-Content -Path $Path -Raw -Encoding UTF8
        if (-not [string]::IsNullOrWhiteSpace($json)) {
            return $json | ConvertFrom-Json
        }
    }

    return [pscustomobject]@{
        Sage = [pscustomobject]@{
            Username = ''
            Password = ''
            Company = 'TST'
        }
        Api = [pscustomobject]@{
            ApiKey = ''
        }
    }
}

function Ensure-Property {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName,

        [Parameter(Mandatory = $true)]
        [object]$DefaultValue
    )

    if (-not ($Object.PSObject.Properties.Name -contains $PropertyName)) {
        $Object | Add-Member -NotePropertyName $PropertyName -NotePropertyValue $DefaultValue
    }
}

function Save-LocalSettings {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Config,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $backupPath = "$Path.bak"
    if (Test-Path -LiteralPath $Path) {
        Copy-Item -Path $Path -Destination $backupPath -Force
    }

    $json = $Config | ConvertTo-Json -Depth 20
    Set-Content -Path $Path -Value $json -Encoding UTF8
}

function Convert-SecureStringToPlainText {
    param(
        [Parameter(Mandatory = $true)]
        [Security.SecureString]$SecureString
    )

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}
