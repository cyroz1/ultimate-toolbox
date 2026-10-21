# Captures screenshots of the installed app for the repo README / releases.
# Run on a logged-in Windows desktop (GitHub hosted runners are session 0, so
# the CI screenshots job is best effort; a self-hosted or local run gives real
# captures). Usage:
#   powershell -ExecutionPolicy Bypass -File tools\screenshot.ps1 [-OutDir screenshots]
param(
    [string]$OutDir = "screenshots",
    [string]$Exe = "$env:ProgramFiles\UltimateWinUI\UltimateWinUI.exe"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Capture([string]$name) {
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $path = Join-Path $OutDir $name
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $gfx.Dispose(); $bmp.Dispose()
    Write-Host "saved $path"
}

$pages = @(
    @{ Name = "01-home.png";     Arg = "home" },
    @{ Name = "02-windows.png";  Arg = "category:Windows" },
    @{ Name = "03-graphics.png"; Arg = "category:Graphics" },
    @{ Name = "04-advanced.png"; Arg = "category:Advanced" },
    @{ Name = "05-logs.png";     Arg = "logs" },
    @{ Name = "06-settings.png"; Arg = "settings" }
)

foreach ($page in $pages) {
    $proc = Start-Process -FilePath $Exe -ArgumentList "--page", $page.Arg -PassThru
    Start-Sleep -Seconds 6
    Capture $page.Name
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}
