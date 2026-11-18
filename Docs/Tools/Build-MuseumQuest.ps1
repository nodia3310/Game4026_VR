param(
    [string] $OutputPath = 'Builds/Android/Museum-Quest3.apk'
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$output = [IO.Path]::GetFullPath($OutputPath, $project)
if ([IO.Path]::GetExtension($output) -ne '.apk') {
    throw 'Specify an .apk output path.'
}
$log = [IO.Path]::ChangeExtension($output, '.build.log')
$lock = Get-Content -LiteralPath (Join-Path $project 'Packages/packages-lock.json') -Raw | ConvertFrom-Json
$gltfPackage = 'com.unity.cloud.gltfast'
$gltfVersion = $lock.dependencies.$gltfPackage.version
$packageCache = Join-Path $project 'Library/PackageCache'
$packages = @(Get-ChildItem -LiteralPath $packageCache -Directory -Filter "$gltfPackage@*" | Where-Object {
    (Get-Content -LiteralPath (Join-Path $_.FullName 'package.json') -Raw | ConvertFrom-Json).version -eq $gltfVersion
})
if ($packages.Count -ne 1) {
    throw "Expected one cached copy of glTFast $gltfVersion. Open the main project in Unity to resolve its packages."
}

$stage = Join-Path $project "Library/MuseumQ/$([guid]::NewGuid().ToString('N').Substring(0, 8))"
Write-Host "Quest build project: $stage"

function Copy-ProjectFolder([string] $Source, [string] $Destination) {
    & robocopy.exe $Source $Destination /E /XJ /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) {
        throw "Could not copy $Source to $Destination (robocopy exit $LASTEXITCODE)."
    }
}

foreach ($folder in 'Assets', 'Packages', 'ProjectSettings', 'Library/PackageCache') {
    Copy-ProjectFolder (Join-Path $project $folder) (Join-Path $stage $folder)
}

$embedded = Join-Path $stage "Packages/$gltfPackage"
Copy-ProjectFolder $packages[0].FullName $embedded
$editorScripts = Join-Path $embedded 'Editor/Scripts'
$importer = Join-Path $editorScripts 'GltfImporter.cs'
$source = (Get-Content -LiteralPath $importer -Raw).Replace("`r`n", "`n")
$load = '            var success = AsyncHelpers.RunSync(() => m_Gltf.Load(ctx.assetPath, importSettings));'
$addTexture = '                            AddObjectToAsset(ctx, $"textures/{texture.name}", texture);'
if (!$source.Contains($load) -or !$source.Contains($addTexture)) {
    throw "glTFast $gltfVersion changed its importer. Update the Quest texture hook before building."
}
$source = $source.Replace($load, @'
            bool compressForQuest = MuseumQuestTextures.AppliesTo(ctx);
            bool originallyReadable = importSettings.TexturesReadable;
            bool success;
            try
            {
                if (compressForQuest)
                    importSettings.TexturesReadable = true;
                success = AsyncHelpers.RunSync(() => m_Gltf.Load(ctx.assetPath, importSettings));
            }
            finally
            {
                importSettings.TexturesReadable = originallyReadable;
            }
'@)
$source = $source.Replace($addTexture, @'
                            if (compressForQuest)
                                MuseumQuestTextures.Compress(ctx, texture, originallyReadable);
                            AddObjectToAsset(ctx, $"textures/{texture.name}", texture);
'@)
[IO.File]::WriteAllText($importer, $source)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'MuseumQuestTextures.cs') -Destination $editorScripts

$temp = New-Item -ItemType Directory -Path (Join-Path $project 'Library/MuseumAndroidTemp') -Force
$gradle = New-Item -ItemType Directory -Path (Join-Path $project 'Library/MuseumAndroidGradle') -Force
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
$previousGradle = $env:GRADLE_USER_HOME
try {
    $env:TEMP = $temp.FullName
    $env:TMP = $temp.FullName
    $env:GRADLE_USER_HOME = $gradle.FullName
    & unity build $stage --target Android --execute-method Game4026.Editor.MuseumAndroidBuild.Build `
        --output-path $output --log-file $log --android-export-type apk --args '-museumQuestOptimized' `
        --timeout 3600 --no-tail --format json
    if ($LASTEXITCODE -ne 0) {
        throw "Museum Quest build failed. See $log."
    }
}
finally {
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
    $env:GRADLE_USER_HOME = $previousGradle
}
