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
