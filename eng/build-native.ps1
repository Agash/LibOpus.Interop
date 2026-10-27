#!/usr/bin/env pwsh
# Builds the native library (libopus with the interop entry points, native/CMakeLists.txt) for one
# runtime from the source pinned in generate/inputs.json, into artifacts/native/<rid>/native, which the
# project packs under runtimes/<rid>/native and copies beside a consumer's output for this machine.
#
#   ./eng/build-native.ps1                      this machine's runtime
#   ./eng/build-native.ps1 -Runtime win-arm64   a runtime this machine's toolchain can target
#
# Windows needs Visual Studio's C toolset, Linux and macOS a C compiler; all need CMake 3.21 or later.
[CmdletBinding()]
param(
    [string] $Runtime = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier,
    [string] $Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$opus = (Get-Content (Join-Path $root 'generate/inputs.json') -Raw | ConvertFrom-Json).opus

# The pinned source, verified by its SHA-256, extracted once per version.
$cache = Join-Path $root "artifacts/obj/opus-$($opus.version)"
$source = Join-Path $cache "opus-$($opus.version)"
if (-not (Test-Path (Join-Path $source 'CMakeLists.txt'))) {
    New-Item -ItemType Directory -Force $cache | Out-Null
    $archive = Join-Path $cache 'opus.tar.gz'
    Invoke-WebRequest $opus.url -OutFile $archive -MaximumRetryCount 4 -RetryIntervalSec 3
    $actual = (Get-FileHash $archive -Algorithm SHA256).Hash
    if ($actual -ne $opus.sha256) {
        throw "$($opus.url) hashed to $actual; generate/inputs.json pins $($opus.sha256)."
    }
    # .NET's tar reader, the same on every host: Windows' tar and Git's differ on paths.
    $file = [System.IO.File]::OpenRead($archive)
    try {
        $gzip = [System.IO.Compression.GZipStream]::new($file, [System.IO.Compression.CompressionMode]::Decompress)
        [System.Formats.Tar.TarFile]::ExtractToDirectory($gzip, $cache, $true)
    }
    finally { $file.Dispose() }
    Remove-Item $archive
}

$build = Join-Path $root "artifacts/obj/native/$Runtime"
$configure = @('-S', (Join-Path $root 'native'), '-B', $build, "-DOPUS_SOURCE_DIR=$source",
    "-DCMAKE_BUILD_TYPE=$Configuration")
$architecture = $Runtime.Split('-')[-1]
if ($Runtime.StartsWith('win-')) {
    # The C runtime linked in, so the library needs no Visual C++ redistributable.
    $configure += @('-A', @{ x64 = 'x64'; arm64 = 'ARM64'; x86 = 'Win32' }[$architecture],
        '-DOPUS_STATIC_RUNTIME=ON')
}
elseif ($Runtime.StartsWith('osx-')) {
    $configure += @("-DCMAKE_OSX_ARCHITECTURES=$(@{ arm64 = 'arm64'; x64 = 'x86_64' }[$architecture])",
        '-DCMAKE_OSX_DEPLOYMENT_TARGET=12.0')
}

cmake @configure
if ($LASTEXITCODE -ne 0) { throw 'CMake could not configure the native build.' }
cmake --build $build --config $Configuration --parallel
if ($LASTEXITCODE -ne 0) { throw 'The native build failed.' }

$name = if ($Runtime.StartsWith('win-')) { 'opus-interop.dll' }
    elseif ($Runtime.StartsWith('osx-')) { 'libopus-interop.dylib' }
    else { 'libopus-interop.so' }
$built = Get-ChildItem $build -Recurse -File -Filter $name | Select-Object -First 1
if (-not $built) { throw "The build produced no $name." }
$output = Join-Path $root "artifacts/native/$Runtime/native"
New-Item -ItemType Directory -Force $output | Out-Null
Copy-Item $built.FullName $output -Force
Copy-Item (Join-Path $source 'COPYING') (Join-Path $root 'artifacts/native/LICENSE.opus.txt') -Force
Write-Host "$Runtime -> $(Join-Path $output $name)"
