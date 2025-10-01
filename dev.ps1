# Development Server Launcher
# Runs backend and frontend concurrently with real-time log output

Write-Host "Starting InfiniteGPU Development Servers..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Function to handle process output
function Start-DevServer {
    param (
        [string]$Name,
        [string]$WorkingDirectory,
        [string]$Command,
        [string]$Color
    )
    
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "cmd.exe"
    $psi.Arguments = "/c $Command"
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    
    # Event handlers for output
    $outputHandler = {
        if (-not [string]::IsNullOrEmpty($EventArgs.Data)) {
            Write-Host "[$($Event.MessageData.Name)] " -ForegroundColor $Event.MessageData.Color -NoNewline
            Write-Host $EventArgs.Data
        }
    }
    
    $errorHandler = {
        if (-not [string]::IsNullOrEmpty($EventArgs.Data)) {
            Write-Host "[$($Event.MessageData.Name)] " -ForegroundColor Red -NoNewline
            Write-Host $EventArgs.Data -ForegroundColor Red
        }
    }
    
    $messageData = @{
        Name = $Name
        Color = $Color
    }
    
    Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action $outputHandler -MessageData $messageData | Out-Null
    Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived -Action $errorHandler -MessageData $messageData | Out-Null
    
    $process.Start() | Out-Null
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()
    
    return $process
}

# Start Backend
Write-Host "Starting Backend (dotnet watch run)..." -ForegroundColor Green
$backendPath = Join-Path $PSScriptRoot "backend\InfiniteGPU.Backend"
$backendProcess = Start-DevServer -Name "BACKEND" -WorkingDirectory $backendPath -Command "dotnet watch run" -Color "Green"

# Wait a moment before starting frontend
Start-Sleep -Seconds 2

# Start Frontend
Write-Host "Starting Frontend (npm run dev)..." -ForegroundColor Blue
$frontendPath = Join-Path $PSScriptRoot "frontend"
$frontendProcess = Start-DevServer -Name "FRONTEND" -WorkingDirectory $frontendPath -Command "npm run dev" -Color "Blue"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Both servers are starting..." -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop all servers" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Handle Ctrl+C
$null = Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action {
    Write-Host "`n`nShutting down servers..." -ForegroundColor Yellow
    
    if ($backendProcess -and -not $backendProcess.HasExited) {
        $backendProcess.Kill()
        Write-Host "Backend stopped" -ForegroundColor Green
    }
    
    if ($frontendProcess -and -not $frontendProcess.HasExited) {
        $frontendProcess.Kill()
        Write-Host "Frontend stopped" -ForegroundColor Blue
    }
    
    Write-Host "All servers stopped" -ForegroundColor Cyan
}

# Keep script running and monitor processes
try {
    while ($true) {
        if ($backendProcess.HasExited -and $frontendProcess.HasExited) {
            Write-Host "`nBoth processes have exited" -ForegroundColor Red
            break
        }
        
        if ($backendProcess.HasExited) {
            Write-Host "`nBackend process has exited unexpectedly" -ForegroundColor Red
            break
        }
        
        if ($frontendProcess.HasExited) {
            Write-Host "`nFrontend process has exited unexpectedly" -ForegroundColor Red
            break
        }
        
        Start-Sleep -Seconds 1
    }
}
finally {
    # Cleanup
    if ($backendProcess -and -not $backendProcess.HasExited) {
        $backendProcess.Kill()
    }
    
    if ($frontendProcess -and -not $frontendProcess.HasExited) {
        $frontendProcess.Kill()
    }
    
    # Unregister events
    Get-EventSubscriber | Where-Object { $_.SourceObject -eq $backendProcess -or $_.SourceObject -eq $frontendProcess } | Unregister-Event
    Get-Job | Remove-Job -Force
    
    Write-Host "`nCleanup complete" -ForegroundColor Cyan
}