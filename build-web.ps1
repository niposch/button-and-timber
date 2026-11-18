param(
    [switch]$Setup,
    [string]$Toolchain = (Join-Path $PSScriptRoot '.godot/web-toolchain')
)

$ErrorActionPreference = 'Stop'
$toolchainRoot = [IO.Path]::GetFullPath($Toolchain)
$sdk = Join-Path $toolchainRoot 'dotnet'
$engineRoot = Join-Path $toolchainRoot 'godot/Godot_v4.7.2-stable_mono_web_export_win64'
$engine = Join-Path $engineRoot 'Godot_v4.7.2-stable_mono_web_export_win64_console.exe'
$stage = Join-Path $PSScriptRoot '.godot/web-project'
$output = Join-Path $PSScriptRoot 'builds/web'

if ($Setup) {
    New-Item -ItemType Directory -Force -Path $toolchainRoot | Out-Null
    $downloads = @(
        @{
            File = 'godot-web.zip'; Directory = (Join-Path $toolchainRoot 'godot'); Algorithm = 'SHA256'
            Hash = '607506c10bdb9f0c3ef92329799275688712991b90073a725a9877e203a242a6'
            Url = 'https://github.com/ComplexRobot/godot-dotnet-web-export/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_web_export_win64.zip'
        },
        @{
            File = 'dotnet-sdk.zip'; Directory = $sdk; Algorithm = 'SHA512'
            Hash = '663d866ad213602d8e267ea5eb575e79bf3f4b920b29f246017021551e93e895e1b2a1f0abee08ec26030eb95b818792a2a7a5835fbada90798ad66eb5c7b168'
            Url = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/9.0.319/dotnet-sdk-9.0.319-win-x64.zip'
        }
    )
    foreach ($download in $downloads) {
        $archive = Join-Path $toolchainRoot $download.File
        if (!(Test-Path -LiteralPath $archive)) { Invoke-WebRequest $download.Url -OutFile $archive }
        if ((Get-FileHash -LiteralPath $archive -Algorithm $download.Algorithm).Hash -ne $download.Hash) {
            throw "Checksum mismatch: $archive"
        }
        if (!(Test-Path -LiteralPath $download.Directory)) {
            Expand-Archive -LiteralPath $archive -DestinationPath $download.Directory
        }
    }
    New-Item -ItemType File -Force -Path (Join-Path $engineRoot '_sc_') | Out-Null
    $templates = Join-Path $engineRoot 'editor_data/export_templates/4.7.2.stable.mono'
    New-Item -ItemType Directory -Force -Path $templates | Out-Null
    foreach ($template in @('web_debug.zip', 'web_release.zip')) {
        Copy-Item -LiteralPath (Join-Path $engineRoot $template) -Destination $templates -Force
    }
}

if (!(Test-Path -LiteralPath $engine) -or !(Test-Path -LiteralPath (Join-Path $sdk 'dotnet.exe'))) {
    throw 'Run build-web.ps1 -Setup to install the web toolchain.'
}

$environment = @{
    PATH = "$sdk;$env:PATH"
    DOTNET_ROOT = $sdk
    DOTNET_CLI_HOME = (Join-Path $PSScriptRoot '.godot/web-cli')
    NUGET_PACKAGES = (Join-Path $toolchainRoot 'packages')
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    ButtonAndTimberWeb = 'true'
}
$previous = @{}
foreach ($key in $environment.Keys) {
    $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
    [Environment]::SetEnvironmentVariable($key, $environment[$key], 'Process')
}

try {
    if ($Setup) {
        & dotnet workload install wasm-tools --skip-manifest-update --source https://api.nuget.org/v3/index.json
        if ($LASTEXITCODE) { throw 'WASM workload setup failed.' }
    }

    $revision = (git -C $PSScriptRoot rev-parse --short=12 HEAD).Trim()
    if ($LASTEXITCODE -or $revision -notmatch '^[0-9a-f]{12,40}$') { throw 'Cannot identify the Git revision.' }
    $stamp = if ($env:GITHUB_RUN_ID) { "$env:GITHUB_RUN_ID-$env:GITHUB_RUN_ATTEMPT" } else { [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff') }
    if ($stamp -notmatch '^[0-9-]+$') { throw 'Invalid build stamp.' }
    $name = "game-$revision-$stamp"

    foreach ($path in @($stage, $output)) {
        if (Test-Path -LiteralPath $path) {
            $target = Get-Item -LiteralPath $path -Force
            if ($target.FullName -ne [IO.Path]::GetFullPath($path) -or $target.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
                throw "Unexpected path for generated files: $path"
            }
            Remove-Item -LiteralPath $target.FullName -Recurse -Force
        }
        New-Item -ItemType Directory -Force -Path $path | Out-Null
    }

    foreach ($entry in @('Game', 'web', 'Main.tscn', 'project.godot', 'ButtonAndTimber.csproj')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $entry) -Destination $stage -Recurse
    }
    $art = Join-Path $stage 'Art'
    New-Item -ItemType Directory -Path $art | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Art') -Exclude Source | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $art -Recurse
    }
    '{"sdk":{"version":"9.0.319","rollForward":"disable"}}' | Set-Content (Join-Path $stage 'global.json')
    $packageSource = [Security.SecurityElement]::Escape((Join-Path $engineRoot 'nuget'))
    @"
<configuration><packageSources><clear/><add key="Godot web exporter" value="$packageSource"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>
"@ | Set-Content (Join-Path $stage 'NuGet.Config')

    $templateRoot = (Join-Path $engineRoot 'editor_data/export_templates/4.7.2.stable.mono').Replace('\', '/')
    @"
[preset.0]
name="Web"
platform="Web"
runnable=true
export_filter="all_resources"
include_filter=""
exclude_filter=""
export_path=""
script_export_mode=2

[preset.0.options]
custom_template/debug="$templateRoot/web_debug.zip"
custom_template/release="$templateRoot/web_release.zip"
variant/extensions_support=false
variant/thread_support=true
vram_texture_compression/for_desktop=true
vram_texture_compression/for_mobile=false
html/export_icon=true
html/custom_html_shell="res://web/shell.html"
html/canvas_resize_policy=2
html/focus_canvas_on_start=true
progressive_web_app/enabled=false
"@ | Set-Content (Join-Path $stage 'export_presets.cfg')

    Push-Location -LiteralPath $stage
    try {
        & dotnet new sln --name ButtonAndTimber
        if ($LASTEXITCODE) { throw 'Web solution creation failed.' }
        & dotnet sln ButtonAndTimber.sln add ButtonAndTimber.csproj
        if ($LASTEXITCODE) { throw 'Web solution setup failed.' }
        & dotnet build ButtonAndTimber.csproj --nologo
        if ($LASTEXITCODE) { throw 'Web C# build failed.' }
        & $engine --headless --editor --path $stage --import
        if ($LASTEXITCODE) { throw 'Web resource import failed.' }
        & $engine --headless --path $stage --export-release Web (Join-Path $output "$name.html") 2>&1 | Tee-Object -Variable exportLog
        if ($LASTEXITCODE -or ($exportLog -match '^ERROR:')) { throw 'Web export failed.' }
    } finally {
        Pop-Location
    }

    $pack = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes((Join-Path $output "$name.pck")))
    foreach ($entry in @('.godot/mono/publish/wasm32/ButtonAndTimber.dll', 'GodotSharp.dll', '.dotnet-publish-manifest')) {
        if (!$pack.Contains($entry)) { throw "Managed web build is missing $entry" }
    }
    Move-Item -LiteralPath (Join-Path $output "$name.html") -Destination (Join-Path $output 'index.html')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'web/isolation.js') -Destination $output
    foreach ($entry in @('LICENSE', 'THIRD_PARTY.md', 'licenses')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $entry) -Destination $output -Recurse
    }
    foreach ($notice in @('Art/Audio/License.txt', 'Art/Source/KayKit-License.txt', 'Art/Source/Quaternius-License.txt', 'Art/Fonts/LICENSE')) {
        $publishedNotice = Join-Path $output $notice
        New-Item -ItemType Directory -Force -Path (Split-Path $publishedNotice) | Out-Null
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $notice) -Destination $publishedNotice
    }
    New-Item -ItemType File -Path (Join-Path $output '.nojekyll') | Out-Null
    Write-Host "Browser build: $output"
} finally {
    foreach ($key in $previous.Keys) {
        [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process')
    }
}
