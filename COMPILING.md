# How to compile WinAuth

## Prerequisites

* Visual Studio 2022 or later, with the .NET desktop development workload
* .NET Framework 4.8 targeting pack (included with that workload)

## Building

1. Before starting, back up `%AppData%\WinAuth\winauth.xml` or export your authenticators.
2. Open `WinAuth.sln` in the repository root.
3. Build the solution.
4. The executable is `bin\Debug\WinAuth.exe` or `bin\Release\WinAuth.exe`.

The YubiKey add-on is already embedded from `Authenticator\Resources\WinAuth.YubiKey.x86.dll` and `WinAuth.YubiKey.x64.dll`. The C++ projects under `WinAuth.YubiKey` are not part of `WinAuth.sln`.

## Building the release archive

From the repository root, run this command in PowerShell 7.

```powershell
./scripts/Build-WindowsRelease.ps1 -OutputDirectory ./artifacts/windows-release
```

This builds `artifacts/windows-release/WinAuth.zip` from the standard Release output of `WinAuth.sln`. It includes `WinAuth.exe`, its configuration, the dependency DLLs, and the translation folders.

GitHub Actions runs the same script on pushes to `master` and version tags.

## Checking the release archive

Run the smoke test from the repository root on a clean Windows profile.

```powershell
./scripts/Test-WindowsRelease.ps1 -ArchivePath ./artifacts/windows-release/WinAuth.zip
```

The smoke test checks the required files and the absence of PDBs, then starts WinAuth from a freshly extracted folder. For a tagged build, add `-VersionTag v3.6.4` to check the executable version.

## Publishing a version

Set the version in `WinAuth/WinAuth.csproj`, `WinAuth/Properties/AssemblyInfo.cs`, and `WinAuth/Properties/app.manifest`, then commit those changes. For version `3.6.4.0`, push the tag `v3.6.4` on that commit.

```text
git tag v3.6.4
git push origin refs/tags/v3.6.4
```

The tag workflow checks the version, builds the standard Release ZIP, and publishes it to GitHub Releases.
