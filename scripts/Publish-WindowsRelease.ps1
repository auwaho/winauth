#requires -Version 7.4
param(
    [Parameter(Mandatory)]
    [string]$ArchivePath
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$commit = $env:GITHUB_SHA
if (!$env:GH_REPO -or !$env:GH_TOKEN -or $commit -notmatch '^[0-9a-f]{40}$') {
    throw 'Publishing requires GH_REPO, GH_TOKEN, and a full commit SHA in GITHUB_SHA.'
}
$archive = Get-Item -LiteralPath $ArchivePath
if ($archive.Name -cne 'WinAuth.zip' -or $archive.PSIsContainer -or !$archive.Length) {
    throw 'The release asset must be a nonempty file named WinAuth.zip.'
}

$tag = "build-$commit"
# GraphQL can find both published releases and drafts with a pending tag.
$query = @'
query($owner: String!, $repo: String!, $tag: String!, $ref: String!) {
  repository(owner: $owner, name: $repo) {
    master: ref(qualifiedName: "refs/heads/master") { target { oid } }
    buildTag: ref(qualifiedName: $ref) { target { oid } }
    release(tagName: $tag) { isDraft }
  }
}
'@
$lookup = gh api graphql -F 'owner={owner}' -F 'repo={repo}' -f "tag=$tag" -f "ref=refs/tags/$tag" -f "query=$query"
$state = ($lookup | ConvertFrom-Json).data.repository
if (!$state.master.target.oid) { throw "Could not resolve master in $env:GH_REPO." }
if ($state.master.target.oid -ne $commit) {
    Write-Host 'Skipping publication because master has advanced.'
    return
}
if ($state.buildTag -and $state.buildTag.target.oid -ne $commit) {
    throw "The existing tag $tag points to a different object."
}
if ($state.release -and !$state.release.isDraft) {
    Write-Host "$tag was already published."
    return
}

if ($state.release) {
    # Only this commit's private draft can contain an upload from a failed attempt.
    gh release upload $tag $archive.FullName --clobber
} else {
    $project = [xml](Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../WinAuth/WinAuth.csproj'))
    $title = ([version]$project.GetElementsByTagName('AssemblyVersion')[0].InnerText).ToString(3)
    $notes = @"
GitHub Actions built this release from commit $commit on master.

The zip contains only WinAuth.exe. Extract it and run that file. Requires .NET Framework 4.8.
"@
    gh release create $tag $archive.FullName --draft --target $commit --title $title --notes $notes
}

$draft = gh release view $tag --json isDraft,targetCommitish,assets | ConvertFrom-Json
$digest = 'sha256:' + (Get-FileHash -LiteralPath $archive.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
if (!$draft.isDraft -or $draft.targetCommitish -ne $commit -or $draft.assets.Count -ne 1 -or
    $draft.assets[0].name -cne $archive.Name -or $draft.assets[0].state -ne 'uploaded' -or
    $draft.assets[0].digest -ne $digest) {
    throw "The draft for $tag does not contain the verified WinAuth.zip for this commit."
}

$head = gh api 'repos/{owner}/{repo}/git/ref/heads/master' --jq '.object.sha'
if (!$head) { throw "Could not resolve master in $env:GH_REPO after upload." }
if ($head -ne $commit) {
    Write-Host "Leaving $tag in draft because master advanced during upload."
    return
}
gh release edit $tag --draft=false --latest
