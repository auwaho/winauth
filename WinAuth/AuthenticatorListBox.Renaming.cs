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
		#region Item renaming

		/// <summary>
		/// Set the position of the rename textbox
		/// </summary>
		private void setRenameTextboxLocation()
		{
			if (_renameTextbox != null && _renameTextbox.Visible == true)
			{
				AuthenticatorListitem item = _renameTextbox.Tag as AuthenticatorListitem;
				if (item != null)
				{
					int y = (this.ItemHeight * item.Index) - (this.TopIndex * this.ItemHeight) + MARGIN_TOP;
					if (RenameTextbox.Location.Y != y)
					{
						RenameTextbox.Location = new Point(RenameTextbox.Location.X, y);
					}
					Refresh();
				}
			}
		}

		/// <summary>
		/// Get or create the rename textbox
		/// </summary>
		public TextBox RenameTextbox
		{
			get
			{
				bool hasvscroll = (this.Height < (this.Items.Count * this.ItemHeight));
				int labelMaxWidth = GetMaxAvailableLabelWidth(this.Width - this.Margin.Horizontal - this.DefaultPadding.Horizontal - (hasvscroll ? SystemInformation.VerticalScrollBarWidth : 0));
				if (_renameTextbox == null)
				{
					_renameTextbox = new TextBox();
					_renameTextbox.Name = "renameTextBox";
					_renameTextbox.AllowDrop = true;
					_renameTextbox.CausesValidation = false;
					_renameTextbox.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
					_renameTextbox.Location = new System.Drawing.Point(0, 0);
					_renameTextbox.Multiline = false;
					_renameTextbox.Name = "secretCodeField";
					_renameTextbox.Size = new System.Drawing.Size(labelMaxWidth, 22);
					_renameTextbox.TabIndex = 0;
					_renameTextbox.Visible = false;
					_renameTextbox.Leave += RenameTextbox_Leave;
					_renameTextbox.KeyPress += _renameTextbox_KeyPress;

					this.Controls.Add(_renameTextbox);
				}
				else
				{
					_renameTextbox.Width = labelMaxWidth;
				}

				return _renameTextbox;
			}
		}

		/// <summary>
		/// Get flag is we are renaming
		/// </summary>
		public bool IsRenaming
		{
			get
			{
				return (RenameTextbox.Visible == true);
			}
		}

		/// <summary>
		/// End the renaming and decide to save
		/// </summary>
		/// <param name="save"></param>
		public void EndRenaming(bool save = true)
		{
			if (save == false)
			{
				RenameTextbox.Tag = null;
			}
			RenameTextbox.Visible = false;
		}

		/// <summary>
		/// Keypress event to cancel or commit renaming
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void _renameTextbox_KeyPress(object sender, KeyPressEventArgs e)
		{
			if (e.KeyChar == 27)
			{
				RenameTextbox.Tag = null;
				RenameTextbox.Visible = false;
				e.Handled = true;
			}
			else if (e.KeyChar == 13 || e.KeyChar == 9)
			{
				RenameTextbox.Visible = false;
				e.Handled = true;
			}
		}

		/// <summary>
		/// Handle the focus leave for rename box
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void RenameTextbox_Leave(object sender, EventArgs e)
		{
			RenameTextbox.Visible = false;
			AuthenticatorListitem item = RenameTextbox.Tag as AuthenticatorListitem;
			if (item != null)
			{
				string newname = RenameTextbox.Text.Trim();
				if (newname.Length != 0)
				{
					// force the autowidth to be recaculated when we set the name
					item.AutoWidth = 0;
					item.Authenticator.Name = newname;
					RefreshItem(item.Index);
				}
			}
		}

		#endregion
}
}
