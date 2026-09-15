param(
    [Parameter(Mandatory=$true)][string]$EsapiDllDirectory,
    [string]$OutputDirectory = (Join-Path $env:TEMP "ESAPI-Lattice-Build")
)
$ErrorActionPreference = "Stop"
$source = Join-Path $PSScriptRoot "..\src\LatticeSphereGenerator.cs"
$framework = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$compiler = Join-Path $framework "csc.exe"
$references = @(
    (Join-Path $EsapiDllDirectory "VMS.TPS.Common.Model.API.dll"),
    (Join-Path $EsapiDllDirectory "VMS.TPS.Common.Model.Types.dll"),
    (Join-Path $framework "System.dll"),
    (Join-Path $framework "System.Core.dll"),
    (Join-Path $framework "System.Xaml.dll"),
    (Join-Path $framework "WPF\WindowsBase.dll"),
    (Join-Path $framework "WPF\PresentationCore.dll"),
    (Join-Path $framework "WPF\PresentationFramework.dll")
)
foreach ($path in @($source,$compiler)+$references) {
    if (!(Test-Path $path)) { throw "Required file missing: $path" }
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$output = Join-Path $OutputDirectory "LatticeSphereGenerator.esapi.dll"
$arguments = @("/nologo","/target:library","/platform:x64","/optimize+","/warnaserror+","/out:$output")
foreach ($reference in $references) { $arguments += "/reference:$reference" }
$arguments += $source
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "Compilation against installed Varian assemblies failed." }
Get-FileHash $source -Algorithm SHA256
Get-FileHash $output -Algorithm SHA256
Write-Output "PASS: compiled against installed Varian assemblies. Eclipse execution remains a separate check."
