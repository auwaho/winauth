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
$tag = $env:GITHUB_REF_NAME
if ($tag -cnotmatch '^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$' -or
    $env:GITHUB_REF -cne "refs/tags/$tag") {
    throw 'Only a vMAJOR.MINOR.PATCH tag can publish an official release.'
}
$project = [xml](Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../WinAuth/WinAuth.csproj'))
$versions = $project.GetElementsByTagName('AssemblyVersion')
if ($versions.Count -ne 1) { throw 'WinAuth.csproj must define exactly one AssemblyVersion.' }
$title = ([version]$versions[0].InnerText).ToString(3)
if ($tag -cne "v$title") { throw "Tag $tag does not match WinAuth.csproj version $title." }
$head = git -C (Join-Path $PSScriptRoot '..') rev-parse HEAD
if ($head -ne $commit) { throw 'The checked-out commit does not match GITHUB_SHA.' }
$archive = Get-Item -LiteralPath $ArchivePath
if ($archive.Name -cne 'WinAuth.zip' -or $archive.PSIsContainer -or !$archive.Length) {
    throw 'The release asset must be a nonempty file named WinAuth.zip.'
}

# Resolve the remote tag rather than targetCommitish, which GitHub ignores for
# an existing tag. Peel annotated tags as well as lightweight tags.
function Get-TagCommit {
    $target = (gh api "repos/{owner}/{repo}/git/ref/tags/$tag" | ConvertFrom-Json).object
    while ($target.type -eq 'tag') {
        $target = (gh api "repos/{owner}/{repo}/git/tags/$($target.sha)" | ConvertFrom-Json).object
    }
    if ($target.type -ne 'commit' -or $target.sha -notmatch '^[0-9a-f]{40}$') {
        throw "Tag $tag does not resolve to a commit."
    }
    return $target.sha
}
if ((Get-TagCommit) -ne $commit) { throw "Tag $tag does not point to the build commit." }

# GraphQL finds drafts without treating a missing release or Latest as an API
# failure. Only the public, full Latest release is used for the comparison.
$query = @'
query($owner: String!, $repo: String!, $tag: String!) {
  repository(owner: $owner, name: $repo) {
    release(tagName: $tag) { isDraft }
    latestRelease { name tagName isDraft isPrerelease }
  }
}
'@
$lookup = gh api graphql -F 'owner={owner}' -F 'repo={repo}' -f "tag=$tag" -f "query=$query" | ConvertFrom-Json
$state = $lookup.data.repository
if ($lookup.errors -or !$state) { throw "Could not inspect releases in $env:GH_REPO." }
if ($state.release -and !$state.release.isDraft) {
    Write-Host "$tag was already published."
    return
}
$previous = $state.latestRelease
$hasPrevious = $previous -and !$previous.isDraft -and !$previous.isPrerelease -and $previous.tagName -and $previous.tagName -cne $tag

$compare = ''
if ($hasPrevious) {
    $previousTag = $previous.tagName
    $previousName = if ($previous.name) { $previous.name } else { $previousTag }
    $compareUrl = 'https://github.com/{0}/compare/{1}...{2}' -f $env:GH_REPO,
        [Uri]::EscapeDataString($previousTag), [Uri]::EscapeDataString($tag)
    $compare = "[View changes since $previousName]($compareUrl)"
}
$footer = 'This release was built and published automatically by GitHub Actions from commit `{0}` at tag `{1}`.' -f
    $commit.Substring(0, 7), $tag
$overview = 'WinAuth {0} for Windows. Download `WinAuth.zip`, extract the entire archive into one folder, and run `WinAuth.exe` from that folder. Keep the supporting DLL and config files alongside the executable. Requires .NET Framework 4.8.' -f $title
$notes = (@($overview, $compare, $footer) | Where-Object { $_ }) -join "`n`n"

if ($state.release) {
    $draft = gh release view $tag --json isDraft,tagName,body,assets | ConvertFrom-Json
    if (!$draft.isDraft) {
        Write-Host "$tag was already published."
        return
    }
    if ($draft.tagName -cne $tag) { throw "The draft does not belong to tag $tag." }
    if (@($draft.assets | Where-Object { $_.name -cne 'WinAuth.zip' }).Count) {
        throw "The draft for $tag contains unexpected assets."
    }
    # Retain edited notes on a retry. Refresh only our provenance footer and
    # supply the comparison if the draft's notes do not already contain it.
    $body = [regex]::Replace([string]$draft.body,
        '(?m)^This release was built and published automatically by GitHub Actions from commit `[0-9a-f]{7,40}` (?:on `master`|at tag `[^`\r\n]+`)\.\s*$', '').TrimEnd()
    if ($body) {
        if ($compare -and !$body.Contains($compareUrl)) { $body += "`n`n$compare" }
        $notes = "$body`n`n$footer"
    }
}

$notesPath = [IO.Path]::GetTempFileName()
try {
    [IO.File]::WriteAllText($notesPath, $notes, [Text.UTF8Encoding]::new($false))
    if ($state.release) {
        if ($draft.body -cne $notes) { gh release edit $tag --notes-file $notesPath }
        # Replacements are permitted only in the verified private draft.
        gh release upload $tag $archive.FullName --clobber
    } else {
        gh release create $tag $archive.FullName --draft --verify-tag --target $commit --title $title --notes-file $notesPath
    }
} finally {
    Remove-Item -LiteralPath $notesPath -Force
}

$draft = gh release view $tag --json isDraft,tagName,assets | ConvertFrom-Json
$digest = 'sha256:' + (Get-FileHash -LiteralPath $archive.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
if (!$draft.isDraft -or $draft.tagName -cne $tag -or @($draft.assets).Count -ne 1 -or
    $draft.assets[0].name -cne $archive.Name -or $draft.assets[0].state -ne 'uploaded' -or
    $draft.assets[0].digest -ne $digest) {
    throw "The draft for $tag does not contain the verified WinAuth.zip for this commit."
}

if ((Get-TagCommit) -ne $commit) { throw "Tag $tag moved during upload; leaving the release in draft." }
gh release edit $tag --draft=false --prerelease=false --latest
