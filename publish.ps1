$projectPath = Join-Path $PSScriptRoot "AzrngTools\AzrngTools.csproj"
$publishDir = Join-Path $PSScriptRoot "publish\win-x64"

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDir `
    -p:PublishAot=false `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false

# makensis installer.nsi
