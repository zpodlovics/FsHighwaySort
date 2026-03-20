$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$buildDir = Join-Path $root 'artifacts/native/win-x64'
$stageDir = Join-Path $root 'artifacts/nuget/native/runtimes/win-x64/native'

cmake -S (Join-Path $root 'src/FsFsHighwaySort.Native') -B $buildDir -G "Visual Studio 17 2022" -A x64
cmake --build $buildDir --config Release --parallel

New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
Copy-Item (Join-Path $buildDir 'Release/highway_sort_wrapper.dll') $stageDir -Force
