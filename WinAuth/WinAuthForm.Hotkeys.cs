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
		/// Unhook the current key hook
		/// </summary>
		private void UnhookHotkeys()
		{
			// remove the hotkey hook
			if (m_hook != null)
			{
				m_hook.UnHook();
				m_hook = null;
			}
		}

		/// <summary>
		/// Hook the hot key for the authenticator
		/// </summary>
		/// <param name="config">current config settings</param>
		private void HookHotkeys()
		{
			// unhook any old hotkeys
			UnhookHotkeys();

			// hook hotkey
			List<WinAuthAuthenticator> keys = new List<WinAuthAuthenticator>();
			foreach (var auth in Config)
			{
				if (auth.HotKey != null)
				{
					keys.Add(auth);
				}
			}
			if (keys.Count != 0)
			{
				m_hook = new KeyboardHook(this, keys);
				m_hook.KeyPressed += new KeyboardHook.KeyboardHookEventHandler(Hotkey_KeyPressed);
			}
		}

		/// <summary>
		/// Timer tick for hotkey
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void hotkeyTimer_Tick(object sender, EventArgs e)
		{
			HotKeyLauncher data = hotkeyTimer.Tag as HotKeyLauncher;

			// check we don't wait forever
			if (data.Started.AddSeconds(5) < DateTime.Now)
			{
				hotkeyTimer.Enabled = false;
				return;
			}

			// wait until the modifiers are released
			if ((System.Windows.Forms.Control.ModifierKeys & Keys.Alt) != 0
				|| (System.Windows.Forms.Control.ModifierKeys & Keys.Control) != 0
				|| (System.Windows.Forms.Control.ModifierKeys & Keys.Shift) != 0)
			{
				return;
			}

			// cancel the timer
			hotkeyTimer.Enabled = false;

			// invoke the handler method in the correct context
			data.Form.Invoke((MethodInvoker)delegate { HandleHotkey(data.Authenticator); });
		}

		/// <summary>
		/// Process the pressed hotkey by performing the appropriate operation
		/// </summary>
		/// <param name="auth">Authenticator</param>
		private void HandleHotkey(WinAuthAuthenticator auth)
		{
			// get the code
			string code = null;
			try
			{
				code = auth.CurrentCode;
			}
			catch (EncryptedSecretDataException)
			{
				// if the authenticator is current protected we display the password window, get the code, and reprotect it
				// with a bit of window jiggling to make sure we get focus and then put it back

				// save the current window
				var fgwindow = WinAPI.GetForegroundWindow();
				Screen screen = Screen.FromHandle(fgwindow);
				IntPtr activewindow = IntPtr.Zero;
				if (this.Visible == true)
				{
					activewindow = WinAPI.SetActiveWindow(this.Handle);
					BringToFront();
				}

				var item = authenticatorList.Items.Cast<AuthenticatorListitem>().Where(i => i.Authenticator == auth).FirstOrDefault();
				code = authenticatorList.GetItemCode(item, screen);

				// restore active window
				if (activewindow != IntPtr.Zero)
				{
					WinAPI.SetActiveWindow(activewindow);
				}
				WinAPI.SetForegroundWindow(fgwindow);
			}
			if (code != null)
			{
				// default to sending the code to the current window
				KeyboardSender keysend = new KeyboardSender(auth.HotKey.Window);
				string command = null;
				if (auth.HotKey.Action == HotKey.HotKeyActions.Notify)
				{
					if (auth.CopyOnCode)
					{
						auth.CopyCodeToClipboard(this, code);
					}
					if (code.Length > 5)
					{
						code = code.Insert(code.Length / 2, " ");
					}
					notifyIcon.ShowBalloonTip(10000, auth.Name, code, ToolTipIcon.Info);
				}
				if (auth.HotKey.Action == HotKey.HotKeyActions.Copy)
				{
					command = "{COPY}";
				}
				else if (auth.HotKey.Action == HotKey.HotKeyActions.Advanced)
				{
					command = auth.HotKey.Advanced;
				}
				else if (auth.HotKey.Action == HotKey.HotKeyActions.Inject)
				{
					command = "{CODE}";
				}
				if (command != null)
				{
					keysend.SendKeys(this, command, code);
				}
			}
	  }

		/// <summary>
		/// Run an action on the authenticator
		/// </summary>
		/// <param name="auth">Authenticator to use</param>
		/// <param name="action">Action to perform</param>
		private void RunAction(WinAuthAuthenticator auth, WinAuthConfig.NotifyActions action)
		{
			// get the code
			string code = null;
			try
			{
				code = auth.CurrentCode;
			}
			catch (EncryptedSecretDataException)
			{
				// if the authenticator is current protected we display the password window, get the code, and reprotect it
				// with a bit of window jiggling to make sure we get focus and then put it back

				// save the current window
				var fgwindow = WinAPI.GetForegroundWindow();
				Screen screen = Screen.FromHandle(fgwindow);
				IntPtr activewindow = IntPtr.Zero;
				if (this.Visible == true)
				{
					activewindow = WinAPI.SetActiveWindow(this.Handle);
					BringToFront();
				}

				var item = authenticatorList.Items.Cast<AuthenticatorListitem>().Where(i => i.Authenticator == auth).FirstOrDefault();
				code = authenticatorList.GetItemCode(item, screen);

				// restore active window
				if (activewindow != IntPtr.Zero)
				{
					WinAPI.SetActiveWindow(activewindow);
				}
				WinAPI.SetForegroundWindow(fgwindow);
			}
			if (code != null)
			{
				KeyboardSender keysend = new KeyboardSender(auth.HotKey != null ? auth.HotKey.Window : null);
				string command = null;

				if (action == WinAuthConfig.NotifyActions.CopyToClipboard)
				{
					command = "{COPY}";
				}
				else if (action == WinAuthConfig.NotifyActions.HotKey)
				{
					command = auth.HotKey != null ? auth.HotKey.Advanced : null;
				}
				else // if (this.Config.NotifyAction == WinAuthConfig.NotifyActions.Notification)
				{
					if (code.Length > 5)
					{
						code = code.Insert(code.Length / 2, " ");
					}
					notifyIcon.ShowBalloonTip(10000, auth.Name, code, ToolTipIcon.Info);
				}
				if (command != null)
				{
					keysend.SendKeys(this, command, code);
				}
			}
		}
}
}
