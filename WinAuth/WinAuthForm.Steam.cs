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
#region Steam Notifications

		/// <summary>
		/// Unhook the Steam notifications
		/// </summary>
		public void UnhookSteam()
		{
			if (this.Config == null)
			{
				return;
			}

			foreach (var auth in this.Config)
			{
				if (auth.AuthenticatorData != null && auth.AuthenticatorData is SteamAuthenticator && ((SteamAuthenticator)auth.AuthenticatorData).Client != null)
				{
					var client = ((SteamAuthenticator)auth.AuthenticatorData).GetClient();
					client.ConfirmationEvent -= SteamClient_ConfirmationEvent;
					client.ConfirmationErrorEvent -= SteamClient_ConfirmationErrorEvent;
				}
			}
		}

		/// <summary>
		/// Hook the Steam authenticators for notifications
		/// </summary>
		public void HookSteam()
		{
			UnhookSteam();
			if (this.Config == null)
			{
				return;
			}

			// do async as setting up clients can take time (Task.Factory.StartNew wait for UI so need to use new Thread(...))
			new Thread(new ThreadStart(() =>
			{
				foreach (var auth in this.Config)
				{
					if (auth.AuthenticatorData != null && auth.AuthenticatorData is SteamAuthenticator)
					{
						var client = ((SteamAuthenticator)auth.AuthenticatorData).GetClient();
						client.ConfirmationEvent += SteamClient_ConfirmationEvent;
						client.ConfirmationErrorEvent += SteamClient_ConfirmationErrorEvent;
					}
				}
			})).Start();
		}

		/// <summary>
		/// Display error message from Steam polling
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="message"></param>
		/// <param name="ex"></param>
		private void SteamClient_ConfirmationErrorEvent(object sender, string message, SteamClient.PollerAction action, Exception ex)
		{
			SteamClient steam = sender as SteamClient;
			var auth = this.Config.Cast<WinAuthAuthenticator>().Where(a => a.AuthenticatorData is SteamAuthenticator && ((SteamAuthenticator)a.AuthenticatorData).Serial == steam.Authenticator.Serial).FirstOrDefault();

			WinAuthMain.LogException(ex, true);

			if (action != SteamClient.PollerAction.SilentAutoConfirm)
			{
				// show the Notification window in the correct context
				this.Invoke(new ShowNotificationCallback(ShowNotification), new object[] {
						auth,
						auth.Name,
						message,
						false,
						0
					});
			}
		}

		/// <summary>
		/// Delegate for Steam notification
		/// </summary>
		/// <param name="auth">current Authenticator</param>
		/// <param name="title">title of notification</param>
		/// <param name="message">notification body</param>
		/// <param name="openOnClick">if can open on click</param>
		/// <param name="extraHeight">extra height (for errors)</param>
		public delegate void ShowNotificationCallback(WinAuthAuthenticator auth, string title, string message, bool openOnClick, int extraHeight);

		/// <summary>
		/// Display a new Notification for a Trading confirmation
		/// </summary>
		/// <param name="auth"></param>
		/// <param name="title"></param>
		/// <param name="message"></param>
		/// <param name="extraHeight"></param>
		public void ShowNotification(WinAuthAuthenticator auth, string title, string message, bool openOnClick, int extraHeight)
		{
			var notify = new Notification(title, message, 10000);
			if (extraHeight != 0)
			{
				notify.Height += extraHeight;
			}
			notify.Tag = auth;
			if (openOnClick == true)
			{
				notify.OnNotificationClicked += Notify_Click;
			}
			notify.Show();
		}

		/// <summary>
		/// The Notification window is clicked
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Notify_Click(object sender, EventArgs e)
		{
			WinAuthAuthenticator auth = ((Notification)sender).Tag as WinAuthAuthenticator;

			// ensure window is front
			BringToFront();
			Show();
			WindowState = FormWindowState.Normal;
			Activate();

			// show waiting
			Cursor.Current = Cursors.WaitCursor;

			// open the confirmations
			var item = authenticatorList.ContextMenuStrip.Items.Cast<ToolStripItem>().Where(i => i.Name == "showSteamTradesMenuItem").FirstOrDefault();
			authenticatorList.CurrentItem = authenticatorList.Items.Cast<AuthenticatorListitem>().Where(i => i.Authenticator == auth).FirstOrDefault();
			item.PerformClick();
		}

		/// <summary>
		/// Receive a new confirmation event from the SteamClient
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="confirmation"></param>
		/// <param name="action"></param>
		private void SteamClient_ConfirmationEvent(object sender, SteamClient.Confirmation confirmation, SteamClient.PollerAction action)
		{
			SteamClient steam = sender as SteamClient;

			var auth = this.Config.Cast<WinAuthAuthenticator>().Where(a => a.AuthenticatorData is SteamAuthenticator && ((SteamAuthenticator)a.AuthenticatorData).Serial == steam.Authenticator.Serial).FirstOrDefault();

			string title = null;
			string message = null;
			bool openOnClick = false;
			int extraHeight = 0;

			if (action == SteamClient.PollerAction.AutoConfirm || action == SteamClient.PollerAction.SilentAutoConfirm)
			{
				if (steam.ConfirmTrade(confirmation.Id, confirmation.Key, true) == true)
				{
					if (action != SteamClient.PollerAction.SilentAutoConfirm)
					{
						title = "Confirmed";
						message = string.Format("<h1>{0}</h1><table width=250 cellspacing=0 cellpadding=0 border=0><tr><td width=40><img src=\"{1}\" /></td><td width=210>{2}<br/>{3}</td></tr></table>", auth.Name, confirmation.Image, confirmation.Details, confirmation.Traded);
					}
				}
				else
				{
					title = "Confirmation Failed";
					message = string.Format("<h1>{0}</h1><table width=250 cellspacing=0 cellpadding=0 border=0><tr><td width=40><img src=\"{1}\" /></td><td width=210>{2}<br/>{3}<br/>Error: {4}</td></tr></table>", auth.Name, confirmation.Image, confirmation.Details, confirmation.Traded, steam.Error ?? "Unknown error");
					extraHeight += 20;
				}
			}
			else if (confirmation.IsNew == true) // if (action == SteamClient.PollerAction.Notify)
			{
				title = "New Confirmation";
				message = string.Format("<h1>{0}</h1><table width=250 cellspacing=0 cellpadding=0 border=0><tr valign=top><td width=40><img src=\"{1}\" /></td><td width=210>{2}<br/>{3}</td></tr></table>", auth.Name, confirmation.Image, confirmation.Details, confirmation.Traded);
				openOnClick = true;
			}

			if (title != null)
			{
				// show the Notification window in the correct context
				this.Invoke(new ShowNotificationCallback(ShowNotification), new object[] {
					auth,
					title,
					message,
					openOnClick,
					extraHeight
				});
			}
		}

#endregion
}
}
