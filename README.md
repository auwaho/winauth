# Windows Authenticator

This is a patched build of the archived [WinAuth](https://github.com/winauth/winauth). The program is still the original WinForms application on .NET Framework. Nothing here is a rewrite. The Steam login, authenticator, and trade calls were updated so this old client can talk to Steam again.

The difference from the original is in the repository header, and in full in the [compare view](https://github.com/winauth/winauth/compare/master...auwaho:winauth:build/net48).

> [!WARNING]
> I recommend a maintained alternative if you need a Steam authenticator for the long term. This fork only repairs the Steam login, authenticator, and trade calls in an obsolete codebase.

## What works again

- The solution builds on .NET Framework 4.8. The .NET 3.5 target, ClickOnce publishing, and the old project site are gone.
- Signing in to an existing Steam account uses the current Steam client login. The old mobile-login endpoints are no longer used.
- Adding a new Steam Guard authenticator works again. A phone number is not required: Steam sends the activation code by email when the account has no phone, and by SMS when it does.
- Trade confirmations use the current community session and request format, so accepting and cancelling a trade works again.
- A saved session is restored from its access token. Steam no longer has the old token-refresh call, so an expired saved login has to be entered again.
- The built-in update check no longer contacts the old update server.

Existing `%AppData%\WinAuth\winauth.xml` files still open.

GitHub Actions builds and tests the release on pushes to `master`. The zip contains only `WinAuth.exe`, with its bundled translations. Download it from the [latest release](https://github.com/auwaho/winauth/releases/latest).

Running it requires [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48). To build from source, open `WinAuth.sln`. See [COMPILING.md](COMPILING.md).
