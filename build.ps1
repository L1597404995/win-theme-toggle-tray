#requires -Version 5.1
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcPath = Join-Path $root 'src\Program.cs'
$distDir = Join-Path $root 'dist'
$buildDir = Join-Path $root 'build'
$iconPath = Join-Path $buildDir 'app.ico'
$outputPath = Join-Path $distDir 'ThemeToggleWidget.exe'

New-Item -ItemType Directory -Force -Path $distDir, $buildDir | Out-Null

Add-Type -AssemblyName System.Drawing

function Get-IconPngBytes {
    param([int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $path = $null
    $bgBrush = $null
    $moonBrush = $null
    $cutBrush = $null

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.ScaleTransform($Size / 32.0, $Size / 32.0)

        $blue = [System.Drawing.Color]::FromArgb(255, 0, 120, 212)

        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $rect = New-Object System.Drawing.RectangleF(1.0, 1.0, 30.0, 30.0)
        $radius = 8.0
        $d = $radius * 2
        $path.AddArc($rect.Left, $rect.Top, $d, $d, 180, 90)
        $path.AddArc($rect.Right - $d, $rect.Top, $d, $d, 270, 90)
        $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
        $path.AddArc($rect.Left, $rect.Bottom - $d, $d, $d, 90, 90)
        $path.CloseFigure()

        $bgBrush = New-Object System.Drawing.SolidBrush($blue)
        $graphics.FillPath($bgBrush, $path)

        $moonBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
        $graphics.FillEllipse($moonBrush, 7.5, 6.5, 15.0, 15.0)

        $cutBrush = New-Object System.Drawing.SolidBrush($blue)
        $graphics.FillEllipse($cutBrush, 12.2, 5.4, 15.0, 15.0)

        $stream = New-Object System.IO.MemoryStream
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            return ,$stream.ToArray()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        if ($cutBrush -ne $null) { $cutBrush.Dispose() }
        if ($moonBrush -ne $null) { $moonBrush.Dispose() }
        if ($bgBrush -ne $null) { $bgBrush.Dispose() }
        if ($path -ne $null) { $path.Dispose() }
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Write-IcoFile {
    param([string]$Path)

    $sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
    $images = New-Object System.Collections.ArrayList

    foreach ($size in $sizes) {
        [void]$images.Add((Get-IconPngBytes -Size $size))
    }

    $stream = [System.IO.File]::Create($Path)
    $writer = New-Object System.IO.BinaryWriter($stream)

    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$sizes.Count)

        $offset = 6 + (16 * $sizes.Count)

        for ($i = 0; $i -lt $sizes.Count; $i++) {
            $size = $sizes[$i]
            $image = $images[$i]
            $dimension = if ($size -ge 256) { 0 } else { $size }

            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$image.Length)
            $writer.Write([UInt32]$offset)

            $offset += $image.Length
        }

        foreach ($image in $images) {
            $writer.Write($image)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

Write-Host 'Generating app icon...'
Write-IcoFile -Path $iconPath

$csc = Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw 'csc.exe (.NET Framework 4.x) was not found.'
}

Write-Host 'Compiling ThemeToggleWidget.exe...'
$arguments = @(
    '/nologo'
    '/target:winexe'
    '/platform:anycpu'
    '/optimize+'
    '/codepage:65001'
    "/win32icon:$iconPath"
    "/out:$outputPath"
    '/r:System.dll'
    '/r:System.Core.dll'
    '/r:System.Drawing.dll'
    '/r:System.Windows.Forms.dll'
    $srcPath
)

& $csc $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path $outputPath)) {
    throw 'Build finished but the output file is missing.'
}

$file = Get-Item $outputPath
Write-Host ("Done: {0} ({1:N0} KB)" -f $file.FullName, ($file.Length / 1KB))
