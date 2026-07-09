param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$csprojPath = "src/Lichen/Lichen/Lichen.csproj"

$branch = git branch --show-current
if ($branch -ne "main") {
    throw "You must be on main to publish. Current branch: $branch"
}

$status = git status --porcelain
if ($status) {
    throw "Working tree is not clean. Commit or discard changes first."
}

git fetch origin

$behind = git rev-list --count HEAD..origin/main
if ([int]$behind -gt 0) {
    throw "Local main is behind origin/main. Pull before publishing."
}

[xml]$csproj = Get-Content $csprojPath
$versionNode = $csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1
$version = $versionNode.Version.Trim()

if (-not $version) {
    throw "Could not find <Version> in $csprojPath"
}

$tag = "v$version"

Write-Host "Release version: $version"
Write-Host "Git tag: $tag"

$existingTag = git tag --list $tag
if ($existingTag) {
    throw "Tag already exists locally: $tag"
}

$remoteTag = git ls-remote --tags origin $tag
if ($remoteTag) {
    throw "Tag already exists on GitHub: $tag"
}

if ($DryRun) {
    Write-Host "DRY RUN: checks passed. No tag created. Nothing pushed."
    exit 0
}

$confirm = Read-Host "Create and push tag $tag from main? This will publish to Yak. Type YES to continue"
if ($confirm -ne "YES") {
    throw "Cancelled."
}

git tag -a $tag -m "Release $tag"
git push origin $tag

Write-Host "Published tag $tag. GitHub Actions should now build and publish to Yak."