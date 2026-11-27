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
#region Options menu

		/// <summary>
		/// Load the menu items for the options menu
		/// </summary>
		private void loadOptionsMenu(ContextMenuStrip menu)
		{
			ToolStripMenuItem menuitem;

			menu.Items.Clear();

			if (this.Config == null || this.Config.IsReadOnly == false)
			{
				menuitem = new ToolStripMenuItem(strings.MenuChangeProtection + "...");
				menuitem.Name = "changePasswordOptionsMenuItem";
				menuitem.Click += changePasswordOptionsMenuItem_Click;
				menu.Items.Add(menuitem);
				menu.Items.Add(new ToolStripSeparator() { Name = "changePasswordOptionsSeparatorItem" });
			}

			if (this.Config != null && this.Config.IsPortable == false)
			{
				menuitem = new ToolStripMenuItem(strings.MenuStartWithWindows);
				menuitem.Name = "startWithWindowsOptionsMenuItem";
				menuitem.Click += startWithWindowsOptionsMenuItem_Click;
				menu.Items.Add(menuitem);
			}

			menuitem = new ToolStripMenuItem(strings.MenuAlwaysOnTop);
			menuitem.Name = "alwaysOnTopOptionsMenuItem";
			menuitem.Click += alwaysOnTopOptionsMenuItem_Click;
			menu.Items.Add(menuitem);

			menuitem = new ToolStripMenuItem(strings.MenuUseSystemTrayIcon);
			menuitem.Name = "useSystemTrayIconOptionsMenuItem";
			menuitem.Click += useSystemTrayIconOptionsMenuItem_Click;
			menu.Items.Add(menuitem);

			menuitem = new ToolStripMenuItem(strings.MenuAutoSize);
			menuitem.Name = "autoSizeOptionsMenuItem";
			menuitem.Click += autoSizeOptionsMenuItem_Click;
			menu.Items.Add(menuitem);

			menu.Items.Add(new ToolStripSeparator());

			menuitem = new ToolStripMenuItem(strings.MenuExport);
			menuitem.Name = "exportOptionsMenuItem";
			menuitem.Click += exportOptionsMenuItem_Click;
			menu.Items.Add(menuitem);

			menu.Items.Add(new ToolStripSeparator());

			menuitem = new ToolStripMenuItem(strings.MenuAbout + "...");
			menuitem.Name = "aboutOptionsMenuItem";
			menuitem.Click += aboutOptionMenuItem_Click;
			menu.Items.Add(menuitem);

			menu.Items.Add(new ToolStripSeparator());

			menuitem = new ToolStripMenuItem(strings.MenuExit);
			menuitem.Name = "exitOptionsMenuItem";
			menuitem.ShortcutKeys = Keys.F4 | Keys.Alt;
			menuitem.Click += exitOptionMenuItem_Click;
			menu.Items.Add(menuitem);
		}

		/// <summary>
		/// Load the menu items for the notify menu
		/// </summary>
		private void loadNotifyMenu(ContextMenuStrip menu)
		{
			ToolStripMenuItem menuitem;
			ToolStripMenuItem subitem;

			menu.Items.Clear();

			menuitem = new ToolStripMenuItem(strings.MenuOpen);
			menuitem.Name = "openOptionsMenuItem";
			menuitem.Click += openOptionsMenuItem_Click;
			menu.Items.Add(menuitem);
			menu.Items.Add(new ToolStripSeparator() { Name = "openOptionsSeparatorItem" });

			if (this.Config != null && this.Config.Count != 0)
			{
				// because of window size, we only show first 30.
				// @todo change to MRU
				var index = 1;
				foreach (var auth in this.Config.Take(30))
				{
					menuitem = new ToolStripMenuItem(index.ToString() + ". " + auth.Name);
					menuitem.Name = "authenticatorOptionsMenuItem_" + index;
					menuitem.Tag = auth;
					menuitem.ShortcutKeyDisplayString = (auth.HotKey != null ? auth.HotKey.ToString() : null);
					menuitem.Click += authenticatorOptionsMenuItem_Click;
					menu.Items.Add(menuitem);
					index++;
				}
				var separator = new ToolStripSeparator();
				separator.Name = "authenticatorOptionsSeparatorItem";
				menu.Items.Add(separator);

				menuitem = new ToolStripMenuItem(strings.DefaultAction);
				menuitem.Name = "defaultActionOptionsMenuItem";
				menu.Items.Add(menuitem);
				subitem = new ToolStripMenuItem(strings.DefaultActionNotification);
				subitem.Name = "defaultActionNotificationOptionsMenuItem";
				subitem.Click += defaultActionNotificationOptionsMenuItem_Click;
				menuitem.DropDownItems.Add(subitem);
				subitem = new ToolStripMenuItem(strings.DefaultActionCopyToClipboard);
				subitem.Name = "defaultActionCopyToClipboardOptionsMenuItem";
				subitem.Click += defaultActionCopyToClipboardOptionsMenuItem_Click;
				menuitem.DropDownItems.Add(subitem);
				subitem = new ToolStripMenuItem(strings.DefaultActionHotkey);
				subitem.Name = "defaultActionHotkeyOptionsMenuItem";
				subitem.Click += defaultActionHotkeyOptionsMenuItem_Click;
				menuitem.DropDownItems.Add(subitem);
				menu.Items.Add(menuitem);

				separator = new ToolStripSeparator();
				separator.Name = "authenticatorActionOptionsSeparatorItem";
				menu.Items.Add(separator);
			}

			//if (this.Config != null)
			//{
			//	menuitem = new ToolStripMenuItem(strings.MenuUseSystemTrayIcon);
			//	menuitem.Name = "useSystemTrayIconOptionsMenuItem";
			//	menuitem.Click += useSystemTrayIconOptionsMenuItem_Click;
			//	menu.Items.Add(menuitem);

			//	menu.Items.Add(new ToolStripSeparator());
			//}

			menuitem = new ToolStripMenuItem(strings.MenuAbout + "...");
			menuitem.Name = "aboutOptionsMenuItem";
			menuitem.Click += aboutOptionMenuItem_Click;
			menu.Items.Add(menuitem);

			menu.Items.Add(new ToolStripSeparator());

			menuitem = new ToolStripMenuItem(strings.MenuExit);
			menuitem.Name = "exitOptionsMenuItem";
			menuitem.ShortcutKeys = Keys.F4 | Keys.Alt;
			menuitem.Click += exitOptionMenuItem_Click;
			menu.Items.Add(menuitem);
		}

		/// <summary>
		/// Set the state of the items when opening the Options menu
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void optionsMenu_Opening(object sender, CancelEventArgs e)
		{
			OpeningOptionsMenu(this.optionsMenu, e);
		}

		/// <summary>
		/// Set the state of the items when opening the notify menu
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void notifyMenu_Opening(object sender, CancelEventArgs e)
		{
			OpeningNotifyMenu(this.notifyMenu, e);
		}

		/// <summary>
		/// Set state of menuitems when opening the Options menu
		/// </summary>
		/// <param name="menu"></param>
		/// <param name="e"></param>
		private void OpeningOptionsMenu(ContextMenuStrip menu, CancelEventArgs e)
		{
			ToolStripItem item;
			ToolStripMenuItem menuitem;

			if (this.Config == null)
			{
				return;
			}

			menuitem = menu.Items.Cast<ToolStripItem>().Where(t => t.Name == "changePasswordOptionsMenuItem").FirstOrDefault() as ToolStripMenuItem;
			if (menuitem != null)
			{
				menuitem.Enabled = (this.Config != null && this.Config.Count != 0);
			}

			menuitem = menu.Items.Cast<ToolStripItem>().Where(t => t.Name == "openOptionsMenuItem").FirstOrDefault() as ToolStripMenuItem;
			if (menuitem != null)
			{
				menuitem.Visible = (this.Config.UseTrayIcon == true && this.Visible == false);
			}
			item = menu.Items.Cast<ToolStripItem>().Where(t => t.Name == "openOptionsSeparatorItem").FirstOrDefault();
			if (item != null)
			{
				item.Visible = (this.Config.UseTrayIcon == true && this.Visible == false);
			}

			menuitem = menu.Items.Cast<ToolStripItem>().Where(t => t.Name == "startWithWindowsOptionsMenuItem").FirstOrDefault() as ToolStripMenuItem;
			if (menuitem != null)
			{
				menuitem.Checked = this.Config.StartWithWindows;
			}

			menuitem = menu.Items.Cast<ToolStripItem>().Where(t => t.Name == "alwaysOnTopOptionsMenuItem").FirstOrDefault() as ToolStripMenuItem;
			if (menuitem != null)
			{
				menuitem.Checked = this.Config.AlwaysOnTop;
			}

			menuitem = menu.Items.Cast<ToolStripItem>().Where(t => t.Name == "useSystemTrayIconOptionsMenuItem").FirstOrDefault() as ToolStripMenuItem;
			if (menuitem != null)
			{
				menuitem.Checked = this.Config.UseTrayIcon;
			}

			menuitem = menu.Items.Cast<ToolStripItem>().Where(t => t.Name == "autoSizeOptionsMenuItem").FirstOrDefault() as ToolStripMenuItem;
			if (menuitem != null)
			{
				menuitem.Checked = this.Config.AutoSize;
			}

			menuitem = menu.Items.Cast<ToolStripItem>().Where(t => t.Name == "autoSizeOptionsMenuItem").FirstOrDefault() as ToolStripMenuItem;
			if (menuitem != null)
			{
				menuitem.Checked = this.Config.AutoSize;
			}
		}

		/// <summary>
		/// Set state of menuitemns when opening the notify menu
		/// </summary>
		/// <param name="menu"></param>
		/// <param name="e"></param>
		private void OpeningNotifyMenu(ContextMenuStrip menu, CancelEventArgs e)
		{
			ToolStripItem item;
			ToolStripMenuItem menuitem;

			if (this.Config == null)
			{
				return;
			}

			menuitem = menu.Items.Cast<ToolStripItem>().Where(t => t.Name == "changePasswordOptionsMenuItem").FirstOrDefault() as ToolStripMenuItem;
			if (menuitem != null)
			{
				menuitem.Enabled = (this.Config != null && this.Config.Count != 0);
			}

			menuitem = menu.Items.Cast<ToolStripItem>().Where(t => t.Name == "openOptionsMenuItem").FirstOrDefault() as ToolStripMenuItem;
			if (menuitem != null)
			{
				menuitem.Visible = (this.Config.UseTrayIcon == true && this.Visible == false);
			}
			item = menu.Items.Cast<ToolStripItem>().Where(t => t.Name == "openOptionsSeparatorItem").FirstOrDefault();
			if (item != null)
			{
				item.Visible = (this.Config.UseTrayIcon == true && this.Visible == false);
			}

			menuitem = menu.Items.Cast<ToolStripItem>().Where(t => t.Name == "defaultActionOptionsMenuItem").FirstOrDefault() as ToolStripMenuItem;
			if (menuitem != null)
			{
				var subitem = menuitem.DropDownItems.Cast<ToolStripItem>().Where(t => t.Name == "defaultActionNotificationOptionsMenuItem").FirstOrDefault() as ToolStripMenuItem;
				subitem.Checked = (this.Config.NotifyAction == WinAuthConfig.NotifyActions.Notification);

				subitem = menuitem.DropDownItems.Cast<ToolStripItem>().Where(t => t.Name == "defaultActionCopyToClipboardOptionsMenuItem").FirstOrDefault() as ToolStripMenuItem;
				subitem.Checked = (this.Config.NotifyAction == WinAuthConfig.NotifyActions.CopyToClipboard);

				subitem = menuitem.DropDownItems.Cast<ToolStripItem>().Where(t => t.Name == "defaultActionHotkeyOptionsMenuItem").FirstOrDefault() as ToolStripMenuItem;
				subitem.Checked = (this.Config.NotifyAction == WinAuthConfig.NotifyActions.HotKey);
			}

			//menuitem = menu.Items.Cast<ToolStripItem>().Where(t => t.Name == "useSystemTrayIconOptionsMenuItem").FirstOrDefault() as ToolStripMenuItem;
			//if (menuitem != null)
			//{
			//	menuitem.Checked = this.Config.UseTrayIcon;
			//}
		}

		/// <summary>
		/// Click the Change Password item of the Options menu
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void changePasswordOptionsMenuItem_Click(object sender, EventArgs e)
		{
			// confirm current password
			if ((this.Config.PasswordType & Authenticator.PasswordTypes.Explicit) != 0)
			{
				bool invalidPassword = false;
				while (true)
				{
					GetPasswordForm checkform = new GetPasswordForm();
					checkform.InvalidPassword = invalidPassword;
					var result = checkform.ShowDialog(this);
					if (result == DialogResult.Cancel)
					{
						return;
					}
					if (this.Config.IsPassword(checkform.Password) == true)
					{
						break;
					}
					invalidPassword = true;
				}
			}

			ChangePasswordForm form = new ChangePasswordForm();
			form.PasswordType = this.Config.PasswordType;
			form.HasPassword = ((this.Config.PasswordType & Authenticator.PasswordTypes.Explicit) != 0);
			if (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
			{
				bool retry;
				var retrypasswordtype = this.Config.PasswordType;
				do
				{
					retry = false;

					this.Config.PasswordType = form.PasswordType;
					if ((this.Config.PasswordType & (Authenticator.PasswordTypes.YubiKeySlot1 | Authenticator.PasswordTypes.YubiKeySlot2)) != 0 && form.Yubikey != null)
					{
						this.Config.Yubi = form.Yubikey;
					}
					if ((this.Config.PasswordType & Authenticator.PasswordTypes.Explicit) != 0 && string.IsNullOrEmpty(form.Password) == false)
					{
						this.Config.Password = form.Password;
					}

					try
					{
						SaveConfig(true);
					}
					catch (InvalidEncryptionException)
					{
						var result = WinAuthForm.ConfirmDialog(this, "Decryption test failed. Retry?", MessageBoxButtons.YesNo);
						if (result == DialogResult.Yes)
						{
							retry = true;
							continue;
						}
						this.Config.PasswordType = retrypasswordtype;
					}
					catch (ChallengeResponseException)
					{
						var result = WinAuthForm.ConfirmDialog(this, "YubiKey Challenge/Response failed. Retry?", MessageBoxButtons.YesNo);
						if (result == DialogResult.Yes)
						{
							retry = true;
							continue;
						}
						this.Config.PasswordType = retrypasswordtype;
					}
				} while (retry);
			}
		}

		/// <summary>
		/// Click the Open item of the Options menu
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void openOptionsMenuItem_Click(object sender, EventArgs e)
		{
			// show the main form
			BringToFront();
			Show();
			WindowState = FormWindowState.Normal;
			Activate();
		}

		/// <summary>
		/// Click one of the context menu authenticators
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void authenticatorOptionsMenuItem_Click(object sender, EventArgs e)
		{
			ToolStripMenuItem menuitem = (ToolStripMenuItem)sender;
			WinAuthAuthenticator auth = menuitem.Tag as WinAuthAuthenticator;
			var item = authenticatorList.Items.Cast<AuthenticatorListitem>().Where(i => i.Authenticator == auth).FirstOrDefault();
			if (item != null)
			{
				RunAction(auth, this.Config.NotifyAction);

				//string code = authenticatorList.GetItemCode(item);
				//if (code != null)
				//{
				//	if (auth.CopyOnCode)
				//	{
				//		auth.CopyCodeToClipboard(this, code);
				//	}
				//	if (code.Length > 5)
				//	{
				//		code = code.Insert(code.Length / 2, " ");
				//	}
				//	notifyIcon.ShowBalloonTip(10000, auth.Name, code, ToolTipIcon.Info);
				//}
			}
		}

		/// <summary>
		/// Click the Start With Windows menu item
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void startWithWindowsOptionsMenuItem_Click(object sender, EventArgs e)
		{
			this.Config.StartWithWindows = !this.Config.StartWithWindows;
		}

		/// <summary>
		/// Click the Always On Top menu item
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void alwaysOnTopOptionsMenuItem_Click(object sender, EventArgs e)
		{
			this.Config.AlwaysOnTop = !this.Config.AlwaysOnTop;
		}

		/// <summary>
		/// Click the Use Tray Icon menu item
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void useSystemTrayIconOptionsMenuItem_Click(object sender, EventArgs e)
		{
			this.Config.UseTrayIcon = !this.Config.UseTrayIcon;
		}
		
		/// <summary>
		/// Click the default action options menu item
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void defaultActionNotificationOptionsMenuItem_Click(object sender, EventArgs e)
		{
			this.Config.NotifyAction = WinAuthConfig.NotifyActions.Notification;
		}

		/// <summary>
		/// Click the default action options menu item
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void defaultActionCopyToClipboardOptionsMenuItem_Click(object sender, EventArgs e)
		{
			this.Config.NotifyAction = WinAuthConfig.NotifyActions.CopyToClipboard;
		}

		/// <summary>
		/// Click the default action options menu item
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void defaultActionHotkeyOptionsMenuItem_Click(object sender, EventArgs e)
		{
			this.Config.NotifyAction = WinAuthConfig.NotifyActions.HotKey;
		}

		/// <summary>
		/// Click the Auto Size menu item
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void autoSizeOptionsMenuItem_Click(object sender, EventArgs e)
		{
			this.Config.AutoSize = !this.Config.AutoSize;
		}

		/// <summary>
		/// Click the Export menu
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void exportOptionsMenuItem_Click(object sender, EventArgs e)
		{
			// confirm current password
			if ((this.Config.PasswordType & Authenticator.PasswordTypes.Explicit) != 0)
			{
				bool invalidPassword = false;
				while (true)
				{
					GetPasswordForm checkform = new GetPasswordForm();
					checkform.InvalidPassword = invalidPassword;
					var result = checkform.ShowDialog(this);
					if (result == DialogResult.Cancel)
					{
						return;
					}
					if (this.Config.IsPassword(checkform.Password) == true)
					{
						break;
					}
					invalidPassword = true;
				}
			}

			ExportForm exportform = new ExportForm();
			if (exportform.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
			{
				WinAuthHelper.ExportAuthenticators(this, this.Config, exportform.ExportFile, exportform.Password, exportform.PGPKey);
			}
		}

		/// <summary>
		/// Click the Updates menu item
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void aboutUpdatesMenuItem_Click(object sender, EventArgs e)
		{
			ShowUpdaterForm();
		}

		/// <summary>
		/// Click the About menu item
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void aboutOptionMenuItem_Click(object sender, EventArgs e)
		{
			AboutForm form = new AboutForm();
			form.Config = this.Config;
			form.ShowDialog(this);
		}

		/// <summary>
		/// Click the Exit menu item
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void exitOptionMenuItem_Click(object sender, EventArgs e)
		{
			m_explictClose = true;
			this.Close();
		}

#endregion
}
}
