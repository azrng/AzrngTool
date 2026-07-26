$projectPath = Join-Path $PSScriptRoot "AzrngTools\AzrngTools.csproj"
$publishDir = Join-Path $PSScriptRoot "publish\win-x64"

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

# --ignore-failed-sources：私有源不可达（如 403）时仍可用本地缓存还原，保证发布链路可复现
dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDir `
    -p:PublishAot=false `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --ignore-failed-sources

# makensis installer.nsi
