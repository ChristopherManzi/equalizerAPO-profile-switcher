param([switch]$Test)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
}
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 is required.' }
$output = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
& $compiler /nologo /warnaserror+ /target:winexe /optimize+ "/win32manifest:$PSScriptRoot/src/app.manifest" "/out:$output/EqualizerProfileSwitcher.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
if ($Test) {
    & $compiler /nologo /warnaserror+ /target:exe "/out:$output/ProfileService.Tests.exe" (Join-Path $PSScriptRoot 'src/ProfileService.cs') (Join-Path $PSScriptRoot 'tests/ProfileServiceTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & "$output/ProfileService.Tests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    & $compiler /nologo /warnaserror+ /target:exe /main:UiSmokeTests "/out:$output/UiSmokeTests.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll $sources (Join-Path $PSScriptRoot 'tests/UiSmokeTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'UI test compilation failed.' }
    & "$output/UiSmokeTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'UI smoke tests failed.' }
}
Write-Output "Built $output/EqualizerProfileSwitcher.exe"
