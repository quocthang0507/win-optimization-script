param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [switch]$Push
)

$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
    $directory = Get-Item -LiteralPath (Get-Location)
    while ($null -ne $directory) {
        if (Test-Path -LiteralPath (Join-Path $directory.FullName '.git')) {
            return $directory.FullName
        }

        $directory = $directory.Parent
    }

    throw 'Repository root was not found.'
}

function Assert-CleanWorkingTree {
    $status = git status --porcelain
    if ($status) {
        throw "Working tree must be clean before creating a release. Commit or stash changes first.`n$status"
    }
}

function Set-ProjectVersion {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath,

        [Parameter(Mandatory)]
        [string]$Version
    )

    $fileVersion = "$Version.0"
    $content = Get-Content -LiteralPath $ProjectPath -Raw
    $content = [regex]::Replace($content, '<Version>.*?</Version>', "<Version>$Version</Version>")
    $content = [regex]::Replace($content, '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$fileVersion</AssemblyVersion>")
    $content = [regex]::Replace($content, '<FileVersion>.*?</FileVersion>', "<FileVersion>$fileVersion</FileVersion>")
    $content = [regex]::Replace($content, '<InformationalVersion>.*?</InformationalVersion>', "<InformationalVersion>$Version</InformationalVersion>")
    Set-Content -LiteralPath $ProjectPath -Value $content -Encoding UTF8
}

$root = Get-RepositoryRoot
Set-Location -LiteralPath $root

Assert-CleanWorkingTree

$projectPath = Join-Path $root 'src/app/WinOptimizationApp.csproj'
$testProjectPath = Join-Path $root 'src/WinOptimizationApp.Tests/WinOptimizationApp.Tests.csproj'
$tag = "v$Version"

if ((git tag --list $tag)) {
    throw "Git tag already exists: $tag"
}

Set-ProjectVersion -ProjectPath $projectPath -Version $Version

dotnet test $testProjectPath --configuration Release
dotnet publish $projectPath --configuration Release --runtime win-x64 --self-contained true

git add $projectPath
git commit -m "Release $tag"
git tag -a $tag -m "Release $tag"

if ($Push) {
    git push origin HEAD
    git push origin $tag
    Write-Host "Pushed release commit and tag $tag. GitHub Actions will build and publish the Windows release." -ForegroundColor Green
}
else {
    Write-Host "Release commit and tag $tag were created locally." -ForegroundColor Green
    Write-Host "Push with: git push origin HEAD; git push origin $tag"
}
