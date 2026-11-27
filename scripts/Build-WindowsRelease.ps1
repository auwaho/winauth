[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$solution = Join-Path $PSScriptRoot '..\WinAuth.sln'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild was not found.' }

$work = New-Item -ItemType Directory -Path (Join-Path ([IO.Path]::GetTempPath()) "winauth-release-$([Guid]::NewGuid())")
try {
    $build = Join-Path $work.FullName 'build'
    & $msbuild $solution /restore /p:Configuration=Release '/p:Platform=Any CPU' /p:EmbedWinAuthTranslations=true "/p:OutDir=$build" /v:m /nologo
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }

    $package = Join-Path $work.FullName 'ilrepack.zip'
    $tools = Join-Path $work.FullName 'tools'
    Invoke-WebRequest -Uri 'https://www.nuget.org/api/v2/package/ILRepack/2.0.33' -OutFile $package
    Expand-Archive -LiteralPath $package -DestinationPath $tools
    $ilrepack = Join-Path $tools 'tools\ILRepack.exe'

    $packed = Join-Path $work.FullName 'WinAuth.exe'
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
    $arguments = @(
        "/out:$packed"
        '/target:winexe'
        '/targetplatform:v4'
        "/lib:$build"
        "/lib:$framework"
        '/ndebug'
        '/skipconfig'
        '/parallel'
        (Join-Path $build 'WinAuth.exe')
    )
    $arguments += Get-ChildItem -LiteralPath $build -File -Filter '*.dll' | Sort-Object Name | Select-Object -ExpandProperty FullName
    $responseFile = Join-Path $work.FullName 'merge.rsp'
    [IO.File]::WriteAllLines($responseFile, [string[]] $arguments)
    & $ilrepack "@$responseFile"
    if ($LASTEXITCODE -ne 0) { throw "ILRepack failed with exit code $LASTEXITCODE." }

    $archive = Join-Path $work.FullName 'WinAuth.zip'
    Compress-Archive -LiteralPath $packed -DestinationPath $archive
    $output = New-Item -ItemType Directory -Force -Path $OutputDirectory
    Copy-Item -LiteralPath $archive -Destination (Join-Path $output.FullName 'WinAuth.zip') -Force
}
finally {
    Remove-Item -LiteralPath $work.FullName -Recurse -Force
}
