<#
.SYNOPSIS
  Backs up the Dashboard's LiteDB file safely for a production IIS deployment.

.DESCRIPTION
  Same reasoning as Coon.Meeting's own backend/scripts/backup-db.ps1: LiteDB holds an exclusive
  lock on its file while the app runs, so this stops the app pool, copies the file, and restarts
  it, rather than risking a mid-write snapshot from a live copy.

.PARAMETER AppPoolName
  The IIS Application Pool this API runs under.

.PARAMETER DbFilePath
  Absolute path to the LiteDB file - must match this app's own Database:FilePath /
  Database__FilePath, pointed at a path outside the deployment folder so a redeploy doesn't
  wipe it.

.PARAMETER BackupDir
  Where dated backup copies are written - ideally a different physical location/drive than
  DbFilePath.

.PARAMETER RetentionDays
  Backups older than this are deleted after a successful new backup. Default 30.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$AppPoolName,

    [Parameter(Mandatory = $true)]
    [string]$DbFilePath,

    [Parameter(Mandatory = $true)]
    [string]$BackupDir,

    [int]$RetentionDays = 30
)

$ErrorActionPreference = "Stop"
Import-Module WebAdministration

if (-not (Test-Path $DbFilePath)) {
    throw "Database file not found at '$DbFilePath' - check DbFilePath matches this app's actual Database__FilePath."
}

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$destination = Join-Path $BackupDir "dashboard-$timestamp.db"

Write-Host "Stopping app pool '$AppPoolName'..."
Stop-WebAppPool -Name $AppPoolName

try {
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-WebAppPoolState -Name $AppPoolName).Value -ne "Stopped" -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
    }

    Copy-Item -Path $DbFilePath -Destination $destination -ErrorAction Stop
    Write-Host "Backed up to '$destination'."
}
finally {
    Write-Host "Starting app pool '$AppPoolName'..."
    Start-WebAppPool -Name $AppPoolName
}

$cutoff = (Get-Date).AddDays(-$RetentionDays)
Get-ChildItem -Path $BackupDir -Filter "dashboard-*.db" |
    Where-Object { $_.LastWriteTime -lt $cutoff } |
    Remove-Item -Force

<#
.REGISTER AS A SCHEDULED TASK (run once, as an administrator, to set this up)

$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument `
    '-NoProfile -ExecutionPolicy Bypass -File "C:\path\to\backup-db.ps1" -AppPoolName "DashboardApiPool" -DbFilePath "D:\CoonMeetingData\dashboard.db" -BackupDir "D:\CoonMeetingBackups"'
$trigger = New-ScheduledTaskTrigger -Daily -At 3:05am
Register-ScheduledTask -TaskName "Coon.Meeting Dashboard DB Backup" -Action $action -Trigger $trigger -RunLevel Highest -User "SYSTEM"
#>
