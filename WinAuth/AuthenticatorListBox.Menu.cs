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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

using WinAuth.Resources;
using System.Diagnostics;

namespace WinAuth
{
	public partial class AuthenticatorListBox : ListBox
	{
		/// <summary>
		/// Preload the context menu items
		/// </summary>
		private void loadContextMenuStrip()
		{
			this.ContextMenuStrip.Items.Clear();

			ToolStripLabel label = new ToolStripLabel();
			label.Name = "contextMenuItemName";
			label.ForeColor = SystemColors.HotTrack;
			this.ContextMenuStrip.Items.Add(label);
			this.ContextMenuStrip.Items.Add(new ToolStripSeparator());
			//
			EventHandler onclick = new EventHandler(ContextMenu_Click);
			//
			ToolStripMenuItem menuitem;
			ToolStripMenuItem subitem;
			//
			menuitem = new ToolStripMenuItem(strings.SetPassword + "...");
			menuitem.Name = "setPasswordMenuItem";
			menuitem.Click += ContextMenu_Click;
			this.ContextMenuStrip.Items.Add(menuitem);
			this.ContextMenuStrip.Items.Add(new ToolStripSeparator());
			//
			menuitem = new ToolStripMenuItem(strings.ShowCode);
			menuitem.Name = "showCodeMenuItem";
			menuitem.Click += ContextMenu_Click;
			this.ContextMenuStrip.Items.Add(menuitem);
			//
			menuitem = new ToolStripMenuItem(strings.CopyCode);
			menuitem.Name = "copyCodeMenuItem";
			menuitem.Click += ContextMenu_Click;
			this.ContextMenuStrip.Items.Add(menuitem);
			//
			this.ContextMenuStrip.Items.Add(new ToolStripSeparator());
			//
			menuitem = new ToolStripMenuItem(strings.ShowSerialAndRestoreCode + "...");
			menuitem.Name = "showRestoreCodeMenuItem";
			menuitem.Click += ContextMenu_Click;
			this.ContextMenuStrip.Items.Add(menuitem);
			//
			menuitem = new ToolStripMenuItem(strings.ShowSecretKey + "...");
			menuitem.Name = "showGoogleSecretMenuItem";
			menuitem.Click += ContextMenu_Click;
			this.ContextMenuStrip.Items.Add(menuitem);
			//
			menuitem = new ToolStripMenuItem(strings.ShowSerialKeyAndDeviceId + "...");
			menuitem.Name = "showTrionSecretMenuItem";
			menuitem.Click += ContextMenu_Click;
			this.ContextMenuStrip.Items.Add(menuitem);
			//
			menuitem = new ToolStripMenuItem(strings.ShowRevocation + "...");
			menuitem.Name = "showSteamSecretMenuItem";
			menuitem.Click += ContextMenu_Click;
			this.ContextMenuStrip.Items.Add(menuitem);
			//
			this.ContextMenuStrip.Items.Add(new ToolStripSeparator { Name = "steamSeperator" });
			//
			menuitem = new ToolStripMenuItem(strings.ConfirmTrades + "...");
			menuitem.Name = "showSteamTradesMenuItem";
			menuitem.Click += ContextMenu_Click;
			this.ContextMenuStrip.Items.Add(menuitem);
			//
			this.ContextMenuStrip.Items.Add(new ToolStripSeparator());
			//
			menuitem = new ToolStripMenuItem(strings.Delete);
			menuitem.Name = "deleteMenuItem";
			menuitem.Click += ContextMenu_Click;
			this.ContextMenuStrip.Items.Add(menuitem);
			//
			menuitem = new ToolStripMenuItem(strings.Rename);
			menuitem.Name = "renameMenuItem";
			menuitem.Click += ContextMenu_Click;
			this.ContextMenuStrip.Items.Add(menuitem);
			//
			this.ContextMenuStrip.Items.Add(new ToolStripSeparator());
			//
			menuitem = new ToolStripMenuItem(strings.AutoRefresh);
			menuitem.Name = "autoRefreshMenuItem";
			menuitem.Click += ContextMenu_Click;
			this.ContextMenuStrip.Items.Add(menuitem);
			//
			menuitem = new ToolStripMenuItem(strings.CopyOnNewCode);
			menuitem.Name = "copyOnCodeMenuItem";
			menuitem.Click += ContextMenu_Click;
			this.ContextMenuStrip.Items.Add(menuitem);
			//
			menuitem = new ToolStripMenuItem(strings.Icon);
			menuitem.Name = "iconMenuItem";
			subitem = new ToolStripMenuItem();
			subitem.Text = strings.IconAuto;
			subitem.Name = "iconMenuItem_default";
			subitem.Tag = string.Empty;
			subitem.Click += ContextMenu_Click;
			menuitem.DropDownItems.Add(subitem);
			menuitem.DropDownItems.Add("-");
			this.ContextMenuStrip.Items.Add(menuitem);
			int iconindex = 1;
			var parentItem = menuitem;
			foreach (Tuple<string,string> entry in WinAuthMain.AUTHENTICATOR_ICONS)
			{
				string icon = entry.Item1;
				string iconfile = entry.Item2;
				if (iconfile.Length == 0)
				{
					parentItem.DropDownItems.Add(new ToolStripSeparator());
				}
				else if (icon.StartsWith("+") == true)
				{
					if (parentItem.Tag is ToolStripMenuItem)
					{
						parentItem = parentItem.Tag as ToolStripMenuItem;
					}

					subitem = new ToolStripMenuItem();
					subitem.Text = icon.Substring(1);
					//subitem.Name = "iconMenuItem_" + iconindex++;
					subitem.Tag = parentItem;
					subitem.Image = new Bitmap(Assembly.GetExecutingAssembly().GetManifestResourceStream("WinAuth.Resources." + iconfile));
					subitem.ImageAlign = ContentAlignment.MiddleLeft;
					subitem.ImageScaling = ToolStripItemImageScaling.SizeToFit;
					//subitem.Click += ContextMenu_Click;
					parentItem.DropDownItems.Add(subitem);
					parentItem = subitem;
				}
				else
				{
					subitem = new ToolStripMenuItem();
					subitem.Text = icon;
					subitem.Name = "iconMenuItem_" + iconindex++;
					subitem.Tag = iconfile;
					subitem.Image = new Bitmap(Assembly.GetExecutingAssembly().GetManifestResourceStream("WinAuth.Resources." + iconfile));
					subitem.ImageAlign = ContentAlignment.MiddleLeft;
					subitem.ImageScaling = ToolStripItemImageScaling.SizeToFit;
					subitem.Click += ContextMenu_Click;
					parentItem.DropDownItems.Add(subitem);
				}
			}
			menuitem.DropDownItems.Add("-");
			subitem = new ToolStripMenuItem();
			subitem.Text = strings.Other + "...";
			subitem.Name = "iconMenuItem_0";
			subitem.Tag = "OTHER";
			subitem.Click += ContextMenu_Click;
			menuitem.DropDownItems.Add(subitem);
			this.ContextMenuStrip.Items.Add(menuitem);
			//
			this.ContextMenuStrip.Items.Add(new ToolStripSeparator());
			//
			menuitem = new ToolStripMenuItem(strings.ShortcutKey + "...");
			menuitem.Name = "shortcutKeyMenuItem";
			menuitem.Click += ContextMenu_Click;
			this.ContextMenuStrip.Items.Add(menuitem);
			//
			ToolStripSeparator sepitem = new ToolStripSeparator();
			sepitem.Name = "syncMenuSep";
			this.ContextMenuStrip.Items.Add(sepitem);
			//
			menuitem = new ToolStripMenuItem(strings.SyncTime);
			menuitem.Name = "syncMenuItem";
			menuitem.Click += ContextMenu_Click;
			this.ContextMenuStrip.Items.Add(menuitem);
		}

		/// <summary>
		/// Set the options for the context menu items based on the currnet authenticator and state
		/// </summary>
		private void SetContextMenuItems()
		{
			var menu = this.ContextMenuStrip;
			var item = this.CurrentItem;
			WinAuthAuthenticator auth = null;
			if (item == null || (auth = item.Authenticator) == null || auth.AuthenticatorData == null)
			{
				return;
			}

			ToolStripLabel labelitem = menu.Items.Cast<ToolStripItem>().Where(i => i.Name == "contextMenuItemName").FirstOrDefault() as ToolStripLabel;
			labelitem.Text = item.Authenticator.Name;
			if (auth.HotKey != null)
			{
				labelitem.Text += " (" + auth.HotKey.ToString() + ")";
			}

			ToolStripItem sepitem;
			ToolStripMenuItem menuitem = menu.Items.Cast<ToolStripItem>().Where(i => i.Name == "setPasswordMenuItem").FirstOrDefault() as ToolStripMenuItem;
			menuitem.Text = (item.Authenticator.AuthenticatorData.PasswordType == Authenticator.PasswordTypes.Explicit ? strings.ChangeOrRemovePassword + "..." : strings.SetPassword + "...");

			menuitem = menu.Items.Cast<ToolStripItem>().Where(i => i.Name == "showCodeMenuItem").FirstOrDefault() as ToolStripMenuItem;
			menuitem.Visible = !auth.AutoRefresh;
			//
			menuitem = menu.Items.Cast<ToolStripItem>().Where(i => i.Name == "showRestoreCodeMenuItem").FirstOrDefault() as ToolStripMenuItem;
			menuitem.Visible = (auth.AuthenticatorData is BattleNetAuthenticator);
			//
			menuitem = menu.Items.Cast<ToolStripItem>().Where(i => i.Name == "showGoogleSecretMenuItem").FirstOrDefault() as ToolStripMenuItem;
			menuitem.Visible = (auth.AuthenticatorData is GoogleAuthenticator || auth.AuthenticatorData is HOTPAuthenticator);
			//
			menuitem = menu.Items.Cast<ToolStripItem>().Where(i => i.Name == "showTrionSecretMenuItem").FirstOrDefault() as ToolStripMenuItem;
			menuitem.Visible = (auth.AuthenticatorData is TrionAuthenticator);
			//
			menuitem = menu.Items.Cast<ToolStripItem>().Where(i => i.Name == "showSteamSecretMenuItem").FirstOrDefault() as ToolStripMenuItem;
			menuitem.Visible = (auth.AuthenticatorData is SteamAuthenticator);
			menuitem.Enabled = (auth.AuthenticatorData is SteamAuthenticator && string.IsNullOrEmpty(((SteamAuthenticator)auth.AuthenticatorData).SteamData) == false);
			//
			sepitem = menu.Items.Cast<ToolStripItem>().Where(i => i.Name == "steamSeperator").FirstOrDefault() as ToolStripItem;
			sepitem.Visible = (auth.AuthenticatorData is SteamAuthenticator);
			//
			menuitem = menu.Items.Cast<ToolStripItem>().Where(i => i.Name == "showSteamTradesMenuItem").FirstOrDefault() as ToolStripMenuItem;
			menuitem.Visible = (auth.AuthenticatorData is SteamAuthenticator);
			menuitem.Enabled = (auth.AuthenticatorData is SteamAuthenticator && string.IsNullOrEmpty(((SteamAuthenticator)auth.AuthenticatorData).SteamData) == false);
			//
			menuitem = menu.Items.Cast<ToolStripItem>().Where(i => i.Name == "autoRefreshMenuItem").FirstOrDefault() as ToolStripMenuItem;
			menuitem.Visible = !(auth.AuthenticatorData is HOTPAuthenticator);
			menuitem.CheckState = (auth.AutoRefresh == true ? CheckState.Checked : CheckState.Unchecked);
			menuitem.Enabled = (auth.AuthenticatorData.RequiresPassword == false && auth.AuthenticatorData.PasswordType != Authenticator.PasswordTypes.Explicit);
			//
			menuitem = menu.Items.Cast<ToolStripItem>().Where(i => i.Name == "copyOnCodeMenuItem").FirstOrDefault() as ToolStripMenuItem;
			menuitem.CheckState = (auth.CopyOnCode == true ? CheckState.Checked : CheckState.Unchecked);
			//
			menuitem = menu.Items.Cast<ToolStripItem>().Where(i => i.Name == "iconMenuItem").FirstOrDefault() as ToolStripMenuItem;
			ToolStripMenuItem subitem = menuitem.DropDownItems.Cast<ToolStripItem>().Where(i => i.Name == "iconMenuItem_default").FirstOrDefault() as ToolStripMenuItem;
			subitem.CheckState = CheckState.Checked;
			foreach (ToolStripItem iconitem in menuitem.DropDownItems)
			{
				if (iconitem is ToolStripMenuItem && iconitem.Tag is string)
				{
					ToolStripMenuItem iconmenuitem = (ToolStripMenuItem)iconitem;
					if (string.IsNullOrEmpty((string)iconmenuitem.Tag) && string.IsNullOrEmpty(auth.Skin) == true)
					{
						iconmenuitem.CheckState = CheckState.Checked;
					}
					else if (string.Compare((string)iconmenuitem.Tag, auth.Skin) == 0)
					{
						iconmenuitem.CheckState = CheckState.Checked;
					}
					else
					{
						iconmenuitem.CheckState = CheckState.Unchecked;
					}
				}
			}
			//
			sepitem = menu.Items.Cast<ToolStripItem>().Where(i => i.Name == "syncMenuSep").FirstOrDefault() as ToolStripItem;
			sepitem.Visible = !(auth.AuthenticatorData is HOTPAuthenticator);
			menuitem = menu.Items.Cast<ToolStripItem>().Where(i => i.Name == "syncMenuItem").FirstOrDefault() as ToolStripMenuItem;
			menuitem.Visible = !(auth.AuthenticatorData is HOTPAuthenticator);
		}

		/// <summary>
		/// Process the context menu item
		/// </summary>
		/// <param name="menuitem"></param>
		private void ProcessMenu(ToolStripItem menuitem)
		{
			var item = this.CurrentItem;
			var auth = item.Authenticator;

			// check and perform each menu
			if (menuitem.Name == "setPasswordMenuItem")
			{
				// check if the authentcated is still protected
				DialogResult wasprotected = UnprotectAuthenticator(item);
				if (wasprotected == DialogResult.Cancel)
				{
					return;
				}
				try
				{
					// show the new password form
					SetPasswordForm form = new SetPasswordForm();
					if (form.ShowDialog(this.Parent as Form) != DialogResult.OK)
					{
						return;
					}

					// set the encrpytion
					string password = form.Password;
					if (string.IsNullOrEmpty(password) == false)
					{
						auth.AuthenticatorData.SetEncryption(Authenticator.PasswordTypes.Explicit, password);
						// can't have autorefresh on
						auth.AutoRefresh = false;

						item.UnprotectCount = 0;
						item.DisplayUntil = DateTime.MinValue;
						item.LastUpdate = DateTime.MinValue;
						item.LastCode = null;
					}
					else
					{
						auth.AuthenticatorData.SetEncryption(Authenticator.PasswordTypes.None);
					}
					// make sure authenticator is saved
					auth.MarkChanged();
					RefreshCurrentItem();
				}
				finally
				{
					if (wasprotected == DialogResult.OK)
					{
						ProtectAuthenticator(item);
					}
				}
			}
			else if (menuitem.Name == "showCodeMenuItem")
			{
				// check if the authentcated is still protected
				if (UnprotectAuthenticator(item) == DialogResult.Cancel)
				{
					return;
				}

				// reduce unprotect count if already displayed
				if (item.DisplayUntil != DateTime.MinValue)
				{
					ProtectAuthenticator(item);
				}

				item.LastCode = auth.CurrentCode;
				item.LastUpdate = DateTime.Now;
				item.DisplayUntil = DateTime.Now.AddSeconds(10);
				RefreshCurrentItem();
			}
			else if (menuitem.Name == "copyCodeMenuItem")
			{
				// check if the authentcated is still protected
				DialogResult wasprotected = UnprotectAuthenticator(item);
				if (wasprotected == DialogResult.Cancel)
				{
					return;
				}
				try
				{
					auth.CopyCodeToClipboard(this.Parent as Form, item.LastCode, true);
				}
				finally
				{
					if (wasprotected == DialogResult.OK)
					{
						ProtectAuthenticator(item);
					}
				}
			}
			else if (menuitem.Name == "autoRefreshMenuItem")
			{
				auth.AutoRefresh = !auth.AutoRefresh;
				item.LastUpdate = DateTime.Now;
				item.DisplayUntil = DateTime.MinValue;
				RefreshCurrentItem();
			}
			else if (menuitem.Name == "shortcutKeyMenuItem")
			{
				DialogResult wasprotected = UnprotectAuthenticator(item);
				if (wasprotected == DialogResult.Cancel)
				{
					return;
				}
				try
				{
					SetShortcutKeyForm form = new SetShortcutKeyForm();
					form.Hotkey = auth.HotKey;
					if (form.ShowDialog(this.Parent as Form) != DialogResult.OK)
					{
						return;
					}
					auth.HotKey = form.Hotkey;
				}
				finally
				{
					if (wasprotected == DialogResult.OK)
					{
						ProtectAuthenticator(item);
					}
				}
			}
			else if (menuitem.Name == "copyOnCodeMenuItem")
			{
				auth.CopyOnCode = !auth.CopyOnCode;
			}
			else if (menuitem.Name == "showRestoreCodeMenuItem")
			{
				// check if the authentcated is still protected
				DialogResult wasprotected = UnprotectAuthenticator(item);
				if (wasprotected == DialogResult.Cancel)
				{
					return;
				}
				try
				{
					if (wasprotected != DialogResult.OK)
					{
						// confirm current main password
						var mainform = this.Parent as WinAuthForm;
						if ((mainform.Config.PasswordType & Authenticator.PasswordTypes.Explicit) != 0)
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
								if (mainform.Config.IsPassword(checkform.Password) == true)
								{
									break;
								}
								invalidPassword = true;
							}
						}
					}

					// show the serial and restore code for Battle.net authenticator				
					ShowRestoreCodeForm form = new ShowRestoreCodeForm();
					form.CurrentAuthenticator = auth;
					form.ShowDialog(this.Parent as Form);
				}
				finally
				{
					if (wasprotected == DialogResult.OK)
					{
						ProtectAuthenticator(item);
					}
				}
			}
			else if (menuitem.Name == "showGoogleSecretMenuItem")
			{
				// check if the authentcated is still protected
				DialogResult wasprotected = UnprotectAuthenticator(item);
				if (wasprotected == DialogResult.Cancel)
				{
					return;
				}
				try
				{
					if (wasprotected != DialogResult.OK)
					{
						// confirm current main password
						var mainform = this.Parent as WinAuthForm;
						if ((mainform.Config.PasswordType & Authenticator.PasswordTypes.Explicit) != 0)
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
								if (mainform.Config.IsPassword(checkform.Password) == true)
								{
									break;
								}
								invalidPassword = true;
							}
						}
					}

					// show the secret key for Google authenticator				
					ShowSecretKeyForm form = new ShowSecretKeyForm();
					form.CurrentAuthenticator = auth;
					form.ShowDialog(this.Parent as Form);
				}
				finally
				{
					if (wasprotected == DialogResult.OK)
					{
						ProtectAuthenticator(item);
					}
				}
			}
			else if (menuitem.Name == "showTrionSecretMenuItem")
			{
				// check if the authenticator is still protected
				DialogResult wasprotected = UnprotectAuthenticator(item);
				if (wasprotected == DialogResult.Cancel)
				{
					return;
				}
				try
				{
					// show the secret key for Trion authenticator				
					ShowTrionSecretForm form = new ShowTrionSecretForm();
					form.CurrentAuthenticator = auth;
					form.ShowDialog(this.Parent as Form);
				}
				finally
				{
					if (wasprotected == DialogResult.OK)
					{
						ProtectAuthenticator(item);
					}
				}
			}
			else if (menuitem.Name == "showSteamSecretMenuItem")
			{
				// check if the authenticator is still protected
				DialogResult wasprotected = UnprotectAuthenticator(item);
				if (wasprotected == DialogResult.Cancel)
				{
					return;
				}

				try
				{
					if (wasprotected != DialogResult.OK)
					{
						// confirm current main password
						var mainform = this.Parent as WinAuthForm;
						if ((mainform.Config.PasswordType & Authenticator.PasswordTypes.Explicit) != 0)
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
								if (mainform.Config.IsPassword(checkform.Password) == true)
								{
									break;
								}
								invalidPassword = true;
							}
						}
					}

					// show the secret key for Google authenticator				
					ShowSteamSecretForm form = new ShowSteamSecretForm();
					form.CurrentAuthenticator = auth;
					form.ShowDialog(this.Parent as Form);
				}
				finally
				{
					if (wasprotected == DialogResult.OK)
					{
						ProtectAuthenticator(item);
					}
				}
			}
			else if (menuitem.Name == "showSteamTradesMenuItem")
			{
				// check if the authenticator is still protected
				DialogResult wasprotected = UnprotectAuthenticator(item);
				if (wasprotected == DialogResult.Cancel)
				{
					return;
				}

				try
				{
					// show the Steam trades dialog
					ShowSteamTradesForm form = new ShowSteamTradesForm();
					form.Authenticator = auth;
					form.ShowDialog(this.Parent as Form);
				}
				finally
				{
					if (wasprotected == DialogResult.OK)
					{
						ProtectAuthenticator(item);
					}
				}
			}
			else if (menuitem.Name == "deleteMenuItem")
			{
				if (WinAuthForm.ConfirmDialog(this.Parent as Form, strings.DeleteAuthenticatorWarning, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
				{
					int index = item.Index;
					this.Items.Remove(item);
					ItemRemoved(this, new AuthenticatorListItemRemovedEventArgs(item));
					if (index >= this.Items.Count)
					{
						index = this.Items.Count - 1;
					}
					this.CurrentItem = (this.Items.Count != 0 ? this.Items[index] as AuthenticatorListitem : null);
					// reset the correct indexes of our items
					for (int i = 0; i < this.Items.Count; i++)
					{
						(this.Items[i] as AuthenticatorListitem).Index = i;
					}
				}
			}
			else if (menuitem.Name == "renameMenuItem")
			{
				int y = (this.ItemHeight * item.Index) - (this.TopIndex * this.ItemHeight) + 8;
				RenameTextbox.Location = new Point(64, y);
				RenameTextbox.Text = auth.Name;
				RenameTextbox.Tag = item;
				RenameTextbox.Visible = true;
				RenameTextbox.Focus();
			}
			else if (menuitem.Name == "syncMenuItem")
			{
				// check if the authentcated is still protected
				DialogResult wasprotected = UnprotectAuthenticator(item);
				if (wasprotected == DialogResult.Cancel)
				{
					return;
				}
				Cursor cursor = Cursor.Current;
				try {
					Cursor.Current = Cursors.WaitCursor;
					auth.Sync();
					RefreshItem(item);
				}
				finally
				{
					Cursor.Current = cursor;
					if (wasprotected == DialogResult.OK)
					{
						ProtectAuthenticator(item);
					}
				}
			}
			else if (menuitem.Name.StartsWith("iconMenuItem_") == true)
			{
				if (menuitem.Tag is string && string.Compare((string)menuitem.Tag, "OTHER") == 0)
				{
					do
					{
						// other..choose an image file
						OpenFileDialog ofd = new OpenFileDialog();
						ofd.AddExtension = true;
						ofd.CheckFileExists = true;
						ofd.DefaultExt = "png";
						ofd.InitialDirectory = Directory.GetCurrentDirectory();
						ofd.FileName = string.Empty;
						ofd.Filter = "PNG Image Files (*.png)|*.png|GIF Image Files (*.gif)|*.gif|All Files (*.*)|*.*";
						ofd.RestoreDirectory = true;
						ofd.ShowReadOnly = false;
						ofd.Title = strings.LoadIconImage + " (png or gif @ 48x48)";
						DialogResult result = ofd.ShowDialog(this);
						if (result != System.Windows.Forms.DialogResult.OK)
						{
							return;
						}
						try
						{
							// get the image and create an icon if not already the right size
							using (Bitmap iconimage = (Bitmap)Image.FromFile(ofd.FileName))
							{
								if (iconimage.Width != ICON_WIDTH || iconimage.Height != ICON_HEIGHT)
								{
                  using (Bitmap scaled = new Bitmap(ICON_WIDTH, ICON_HEIGHT))
                  {
                    using (Graphics g = Graphics.FromImage(scaled))
                    {
                      g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                      g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                      g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                      g.DrawImage(iconimage, new Rectangle(0, 0, ICON_WIDTH, ICON_HEIGHT));
                    }
                    auth.Icon = scaled;
                  }
								}
								else
								{
									auth.Icon = iconimage;
								}
								RefreshCurrentItem();
							}
						}
						catch (Exception ex)
						{
							if (MessageBox.Show(this.Parent as Form,
								string.Format(strings.ErrorLoadingIconFile, ex.Message),
								WinAuthMain.APPLICATION_NAME,
								MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
							{
								continue;
							}
						}
						break;
					} while (true);
				}
				else 
				{
					auth.Skin = (((string)menuitem.Tag).Length != 0 ? (string)menuitem.Tag : null);
					RefreshCurrentItem();
				}
			}
		}
}
}
