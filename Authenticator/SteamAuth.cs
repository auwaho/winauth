/*
 * Copyright (C) 2015 Colin Mackie.
 * This software is distributed under the terms of the GNU General Public License.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKitClient = SteamKit2.SteamClient;

namespace WinAuth
{
	/// <summary>
	/// Steam login used by the reference authenticator: a connected client session,
	/// then access token and community cookies.
	/// </summary>
	internal static class SteamAuth
	{
		internal const string MobileUserAgent = "Mozilla/5.0 (Linux; U; Android 9; en-us; Valve Steam App Version/3) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/44.0.2403.133 Mobile Safari/537.36";
		internal const string MobileApiUserAgent = "okhttp/3.12.12";

		internal sealed class LoginResult
		{
			public string SteamId;
			public string AccessToken;
			public CookieContainer Cookies;
		}

		internal sealed class GuardCodeRequiredException : Exception
		{
			public bool Email { get; private set; }
			public string EmailAddress { get; private set; }

			public GuardCodeRequiredException(bool email, string emailAddress)
				: base(email ? "Email code required" : "Authenticator code required")
			{
				Email = email;
				EmailAddress = emailAddress;
			}
		}

		/// <summary>
		/// Log in the same way as the reference project.
		/// Existing accounts use the default client session. Linking a new authenticator uses the mobile app session.
		/// </summary>
		internal static LoginResult Login(string username, string password, string deviceCode, string emailCode, bool linkAuthenticator, bool persistent)
		{
			return Task.Run(() => LoginAsync(username, password, deviceCode, emailCode, linkAuthenticator, persistent)).GetAwaiter().GetResult();
		}

		private static async Task<LoginResult> LoginAsync(string username, string password, string deviceCode, string emailCode, bool linkAuthenticator, bool persistent)
		{
			var configuration = SteamConfiguration.Create(builder =>
			{
				builder.WithProtocolTypes(ProtocolTypes.WebSocket);
			});

			var steamClient = new SteamKitClient(configuration);
			var manager = new CallbackManager(steamClient);
			var connected = new TaskCompletionSource<bool>();

			manager.Subscribe<SteamKitClient.ConnectedCallback>(callback => connected.TrySetResult(true));
			manager.Subscribe<SteamKitClient.DisconnectedCallback>(callback =>
			{
				if (connected.Task.IsCompleted == false)
				{
					connected.TrySetException(new InvalidOperationException("Disconnected from Steam"));
				}
			});

			var pump = new CancellationTokenSource();
			var pumpTask = Task.Run(() =>
			{
				while (pump.IsCancellationRequested == false)
				{
					manager.RunWaitCallbacks(TimeSpan.FromMilliseconds(500));
				}
			});

			try
			{
				steamClient.Connect();

				var connectedTask = await Task.WhenAny(connected.Task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
				if (connectedTask != connected.Task)
				{
					throw new InvalidOperationException("Timed out connecting to Steam");
				}
				await connected.Task.ConfigureAwait(false);

				var details = new AuthSessionDetails
				{
					Username = username,
					Password = password,
					IsPersistentSession = persistent,
					Authenticator = new PromptAuthenticator(deviceCode, emailCode)
				};
				if (linkAuthenticator == true)
				{
					details.PlatformType = SteamKit2.Internal.EAuthTokenPlatformType.k_EAuthTokenPlatformType_MobileApp;
					details.ClientOSType = EOSType.Android9;
				}

				var authSession = await steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(details).ConfigureAwait(false);
				var poll = await authSession.PollingWaitForResultAsync().ConfigureAwait(false);
				var steamId = authSession.SteamID.ConvertToUInt64().ToString();

				return new LoginResult
				{
					SteamId = steamId,
					AccessToken = poll.AccessToken,
					Cookies = CreateCommunityCookies(steamId, poll.AccessToken, steamClient.ID)
				};
			}
			finally
			{
				pump.Cancel();
				steamClient.Disconnect();
				try
				{
					pumpTask.Wait(TimeSpan.FromSeconds(2));
				}
				catch (Exception)
				{
				}
			}
		}

		internal static CookieContainer CreateCommunityCookies(string steamId, string accessToken, string sessionId)
		{
			if (string.IsNullOrEmpty(sessionId) == true)
			{
				sessionId = Guid.NewGuid().ToString("N");
			}

			var cookies = new CookieContainer();
			cookies.Add(new Cookie("steamid", steamId ?? string.Empty, "/", ".steamcommunity.com"));
			cookies.Add(new Cookie("steamLoginSecure", (steamId ?? string.Empty) + "%7C%7C" + (accessToken ?? string.Empty), "/", ".steamcommunity.com")
			{
				HttpOnly = true,
				Secure = true
			});
			cookies.Add(new Cookie("Steam_Language", "english", "/", ".steamcommunity.com"));
			cookies.Add(new Cookie("dob", "", "/", ".steamcommunity.com"));
			cookies.Add(new Cookie("sessionid", sessionId, "/", ".steamcommunity.com"));
			return cookies;
		}

		private sealed class PromptAuthenticator : IAuthenticator
		{
			private readonly string _deviceCode;
			private readonly string _emailCode;

			public PromptAuthenticator(string deviceCode, string emailCode)
			{
				_deviceCode = deviceCode;
				_emailCode = emailCode;
			}

			public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
			{
				if (previousCodeWasIncorrect == true || string.IsNullOrEmpty(_deviceCode) == true)
				{
					throw new GuardCodeRequiredException(false, null);
				}
				return Task.FromResult(_deviceCode);
			}

			public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
			{
				if (previousCodeWasIncorrect == true || string.IsNullOrEmpty(_emailCode) == true)
				{
					throw new GuardCodeRequiredException(true, email);
				}
				return Task.FromResult(_emailCode);
			}

			public Task<bool> AcceptDeviceConfirmationAsync()
			{
				return Task.FromResult(false);
			}
		}
	}
}
