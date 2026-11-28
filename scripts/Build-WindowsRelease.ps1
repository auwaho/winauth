#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$solution = Join-Path $PSScriptRoot '..\WinAuth.sln'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Visual Studio Installer (vswhere.exe) was not found.' }
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild was not found.' }

$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$work = New-Item -ItemType Directory -Path (Join-Path $temporaryRoot "winauth-release-$([Guid]::NewGuid())")
try {
    $build = Join-Path $work.FullName 'build'
    & $msbuild $solution /restore /p:Configuration=Release '/p:Platform=Any CPU' /p:DebugSymbols=false /p:DebugType=None "/p:OutDir=$build" /v:m /nologo
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }

    # Dependencies can copy their own symbols even when project symbols are disabled.
    Get-ChildItem -LiteralPath $build -File -Recurse -Filter '*.pdb' | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Force
    }

    $archive = Join-Path $work.FullName 'WinAuth.zip'
    Compress-Archive -Path (Join-Path $build '*') -DestinationPath $archive
    $output = New-Item -ItemType Directory -Force -Path $OutputDirectory
    Copy-Item -LiteralPath $archive -Destination (Join-Path $output.FullName 'WinAuth.zip') -Force
}
finally {
    $cleanupPath = [IO.Path]::GetFullPath($work.FullName)
    if ([IO.Path]::GetDirectoryName($cleanupPath) -ne [IO.Path]::TrimEndingDirectorySeparator($temporaryRoot) -or
        [IO.Path]::GetFileName($cleanupPath) -notmatch '^winauth-release-[0-9a-f-]{36}$') {
        throw 'Invalid release build cleanup path.'
    }
    Remove-Item -LiteralPath $cleanupPath -Recurse -Force
}
