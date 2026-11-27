/*
 * Copyright (C) 2013 Colin Mackie.
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Web;
using System.Windows.Forms;

using MetroFramework;
using MetroFramework.Forms;

using NLog;

using WinAuth.Resources;
using System.Security;
using System.Net;

namespace WinAuth
{
	public partial class WinAuthForm : ResourceForm
{
		/// <summary>
		/// Load the current config into WinAuth
		/// </summary>
		/// <param name="password">optional password to decrypt config</param>
		/// <param name="configFile">optional explicit config file</param>
		private void loadConfig(string password)
		{
			string configFile = _startupConfigFile;

			loadingPanel.Visible = true;
			passwordPanel.Visible = false;
			yubiPanel.Visible = false;

			Task.Factory.StartNew<Tuple<WinAuthConfig, Exception>>(() =>
			{
				try
				{
					// use previous config if we have one
					WinAuthConfig config = WinAuthHelper.LoadConfig(this, configFile, password);
					return new Tuple<WinAuthConfig, Exception>(config, null);
				}
				catch (Exception ex)
				{
					return new Tuple<WinAuthConfig, Exception>(null, ex);
				}
			}).ContinueWith((configTask) =>
			{
				Exception ex = configTask.Result.Item2;
				if (ex is WinAuthInvalidNewerConfigException)
				{
					MessageBox.Show(this, ex.Message, WinAuthMain.APPLICATION_TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);
					System.Diagnostics.Process.GetCurrentProcess().Kill();
					return;
				}
				else if (ex is EncryptedSecretDataException)
				{
					loadingPanel.Visible = false;
					passwordPanel.Visible = true;
					yubiPanel.Visible = false;

					this.passwordButton.Focus();
					this.passwordField.Focus();

					return;
				}
				else if (ex is BadYubiKeyException)
				{
					loadingPanel.Visible = false;
					passwordPanel.Visible = false;
					yubiPanel.Visible = true;
					this.yubiLabel.Text = strings.YubikeyInsert;
					return;
				}
				else if (ex is BadPasswordException)
				{
					loadingPanel.Visible = false;
					yubiPanel.Visible = false;
					passwordPanel.Visible = true;
					this.passwordErrorLabel.Text = strings.InvalidPassword;
					this.passwordErrorLabel.Tag = DateTime.Now.AddSeconds(3);
					// oddity with MetroFrame controls in have to set focus away and back to field to make it stick
					this.Invoke((MethodInvoker)delegate { this.passwordButton.Focus(); this.passwordField.Focus(); });
					this.passwordTimer.Enabled = true;
					return;
				}
				else if (ex is Exception)
				{
					if (ErrorDialog(this, strings.UnknownError + ": " + ex.Message, ex, MessageBoxButtons.RetryCancel) == System.Windows.Forms.DialogResult.Cancel)
					{
						this.Close();
						return;
					}
					loadConfig(password);
					return;
				}

				WinAuthConfig config = configTask.Result.Item1;
				if (config == null)
				{
					System.Diagnostics.Process.GetCurrentProcess().Kill();
					return;
				}

				// check for a v2 config file if this is a new config
				if (config.Count == 0 && string.IsNullOrEmpty(config.Filename) == true)
				{
					_existingv2Config = WinAuthHelper.GetLastV2Config();
				}

				this.Config = config;
				this.Config.OnConfigChanged += new ConfigChangedHandler(OnConfigChanged);

				if (config.Upgraded == true)
				{
					SaveConfig(true);
					// display warning
					WinAuthForm.ErrorDialog(this, string.Format(strings.ConfigUpgraded, WinAuthConfig.CURRENTVERSION));
				}

				InitializeForm();
			}, TaskScheduler.FromCurrentSynchronizationContext());

    }

    /// <summary>
    /// Import authenticators from a file
    /// 
    /// *.xml = WinAuth v2
    /// *.txt = plain text with KeyUriFormat per line (https://code.google.com/p/google-authenticator/wiki/KeyUriFormat)
    /// *.zip = encrypted zip, containing import file
    /// *.pgp = PGP encrypted, containing import file
    /// 
    /// </summary>
    /// <param name="authenticatorFile">name import file</param>
    private void importAuthenticator(string authenticatorFile)
		{
			// call legacy import for v2 xml files
			if (string.Compare(Path.GetExtension(authenticatorFile), ".xml", true) == 0)
			{
				importAuthenticatorFromV2(authenticatorFile);
				return;
			}

			List<WinAuthAuthenticator> authenticators = null;
			bool retry;
			do
			{
				retry = false;
				try
				{
					authenticators = WinAuthHelper.ImportAuthenticators(this, authenticatorFile);
				}
				catch (ImportException ex)
				{
					if (ErrorDialog(this, ex.Message, ex.InnerException, MessageBoxButtons.RetryCancel) == System.Windows.Forms.DialogResult.Cancel)
					{
						return;
					}
					retry = true;
				}
			} while (retry);
			if (authenticators == null)
			{
				return;
			}

			// save all the new authenticators
			foreach (var authenticator in authenticators)
			{
				//sync
				authenticator.Sync();

				// make sure there isn't a name clash
				int rename = 0;
				string importedName = authenticator.Name;
				while (this.Config.Where(a => a.Name == importedName).Count() != 0)
				{
					importedName = authenticator.Name + " " + (++rename);
				}
				authenticator.Name = importedName;

				// save off any new authenticators as a backup
				WinAuthHelper.SaveToRegistry(this.Config, authenticator);

				// first time we prompt for protection and set out main settings from imported config
				if (this.Config.Count == 0)
				{
					ChangePasswordForm form = new ChangePasswordForm();
					form.PasswordType = Authenticator.PasswordTypes.Explicit;
					if (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
					{
						this.Config.Yubi = form.Yubikey;
						this.Config.PasswordType = form.PasswordType;
						if ((this.Config.PasswordType & Authenticator.PasswordTypes.Explicit) != 0 && string.IsNullOrEmpty(form.Password) == false)
						{
							this.Config.Password = form.Password;
						}
					}
				}

				// add to main list
				this.Config.Add(authenticator);
			}

			SaveConfig(true);
			loadAuthenticatorList();

			// reset UI
			setAutoSize();
			introLabel.Visible = (this.Config.Count == 0);

			// reset hotkeys
			HookHotkeys();
		}

		/// <summary>
		/// Import a v2 authenticator from an existing file name
		/// </summary>
		/// <param name="authenticatorFile">name of v2 xml file</param>
		private void importAuthenticatorFromV2(string authenticatorFile)
		{
			bool retry = false;
			string password = null;
			bool needPassword = false;
			bool invalidPassword = false;
			do
			{
				try
				{
					WinAuthConfig config = WinAuthHelper.LoadConfig(this, authenticatorFile, password);
					if (config.Count == 0)
					{
						return;
					}

					// get the actual authenticator and ensure it is synced
					List<WinAuthAuthenticator> imported = new List<WinAuthAuthenticator>();
					foreach (var importedAuthenticator in config)
					{
						importedAuthenticator.Sync();

						// make sure there isn't a name clash
						int rename = 0;
						string importedName = importedAuthenticator.Name;
						while (this.Config.Where(a => a.Name == importedName).Count() != 0)
						{
							importedName = importedAuthenticator.Name + " (" + (++rename) + ")";
						}
						importedAuthenticator.Name = importedName;

						imported.Add(importedAuthenticator);
					}

					// first time we prompt for protection and set out main settings from imported config
					if (this.Config.Count == 0)
					{
						this.Config.StartWithWindows = config.StartWithWindows;
						this.Config.UseTrayIcon = config.UseTrayIcon;
						this.Config.AlwaysOnTop = config.AlwaysOnTop;

						ChangePasswordForm form = new ChangePasswordForm();
						form.PasswordType = Authenticator.PasswordTypes.Explicit;
						if (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
						{
							this.Config.Yubi = form.Yubikey;
							this.Config.PasswordType = form.PasswordType;
							if ((this.Config.PasswordType & Authenticator.PasswordTypes.Explicit) != 0 && string.IsNullOrEmpty(form.Password) == false)
							{
								this.Config.Password = form.Password;
							}
						}
					}

					foreach (var auth in imported)
					{
						// save off any new authenticators as a backup
						WinAuthHelper.SaveToRegistry(this.Config, auth);

						// add to main list
						this.Config.Add(auth);
						loadAuthenticatorList(auth);
					}
					SaveConfig(true);

					// reset UI
					setAutoSize();
					introLabel.Visible = (this.Config.Count == 0);

					// reset hotkeys
					HookHotkeys();

					needPassword = false;
					retry = false;
				}
				catch (EncryptedSecretDataException)
				{
					needPassword = true;
					invalidPassword = false;
				}
				catch (BadYubiKeyException)
				{
					needPassword = true;
					invalidPassword = false;
				}
				catch (BadPasswordException)
				{
					needPassword = true;
					invalidPassword = true;
				}
				catch (Exception ex)
				{
					if (ErrorDialog(this, strings.UnknownError + ": " + ex.Message, ex, MessageBoxButtons.RetryCancel) == System.Windows.Forms.DialogResult.Cancel)
					{
						return;
					}
					needPassword = false;
					invalidPassword = false;
					retry = true;
				}

				if (needPassword == true)
				{
					GetPasswordForm form = new GetPasswordForm();
					form.InvalidPassword = invalidPassword;
					var result = form.ShowDialog(this);
					if (result == DialogResult.Cancel)
					{
						return;
					}
					password = form.Password;
					retry = true;
				}
			} while (retry == true);
		}

		/// <summary>
		/// Save the current config immediately or delay it for a few seconds so we can make more changes
		/// </summary>
		private void SaveConfig(bool immediate = false)
		{
			if (immediate == true || (_saveConfigTime != null && _saveConfigTime <= DateTime.Now))
			{
				_saveConfigTime = null;
				lock (this.Config)
				{
					WinAuthHelper.SaveConfig(this.Config);
				}
			}
			else
			{
				// save it in a few seconds so we can batch up saves
				_saveConfigTime = DateTime.Now.AddSeconds(1);
			}
		}
}
}
