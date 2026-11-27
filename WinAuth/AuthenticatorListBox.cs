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
	/// <summary>
	/// An item within the OwnerDraw list that represents an authenticator
	/// </summary>
	public class AuthenticatorListitem
	{
		/// <summary>
		/// Create a new AuthenticatorListitem
		/// </summary>
		/// <param name="auth">authenticator</param>
		/// <param name="index">index of item</param>
		public AuthenticatorListitem(WinAuthAuthenticator auth, int index)
		{
			Authenticator = auth;
			LastUpdate = DateTime.MinValue;
			Index = index;
			DisplayUntil = DateTime.MinValue;
		}

		/// <summary>
		/// Index of item in list
		/// </summary>
		public int Index { get; set; }

		/// <summary>
		/// Authenticator wrapper
		/// </summary>
		public WinAuthAuthenticator Authenticator { get; set; }

		/// <summary>
		/// When the item was last updated
		/// </summary>
		public DateTime LastUpdate { get; set;}

		/// <summary>
		/// When the code should be displayed to if not AutoRefresh
		/// </summary>
		public DateTime DisplayUntil { get; set; }

		/// <summary>
		/// The last code to be displayed
		/// </summary>
		public string LastCode { get; set; }

		/// <summary>
		/// If this item is being dragged
		/// </summary>
		public bool Dragging { get; set; }

		/// <summary>
		/// A count for password protected to allow multipel unprotect operations
		/// </summary>
		public int UnprotectCount { get; set; }

		/// <summary>
		/// Width of item for autosizing
		/// </summary>
		public int AutoWidth { get; set; }
	}

	/// <summary>
	/// Delegate for event when item is removed
	/// </summary>
	/// <param name="source"></param>
	/// <param name="args"></param>
	public delegate void AuthenticatorListItemRemovedHandler(object source, AuthenticatorListItemRemovedEventArgs args);

	/// <summary>
	/// Delegate for Redordered event
	/// </summary>
	/// <param name="source"></param>
	/// <param name="args"></param>
	public delegate void AuthenticatorListReorderedHandler(object source, AuthenticatorListReorderedEventArgs args);

	/// <summary>
	/// Event arguments for removing an item
	/// </summary>
	public class AuthenticatorListItemRemovedEventArgs : EventArgs
	{
		/// <summary>
		/// The item that was removed
		/// </summary>
		public AuthenticatorListitem Item { get; private set; }

		/// <summary>
		/// Default constructor
		/// </summary>
		public AuthenticatorListItemRemovedEventArgs(AuthenticatorListitem item)
			: base()
		{
			Item = item;
		}
	}

	/// <summary>
	/// Event arguments for reordering the list
	/// </summary>
	public class AuthenticatorListReorderedEventArgs : EventArgs
	{
		/// <summary>
		/// Default constructor
		/// </summary>
		public AuthenticatorListReorderedEventArgs()
			: base()
		{
		}
	}

	/// <summary>
	/// Delegate for event when double click
	/// </summary>
	/// <param name="source"></param>
	/// <param name="args"></param>
	public delegate void AuthenticatorListDoubleClickHandler(object source, AuthenticatorListDoubleClickEventArgs args);

	/// <summary>
	/// Event arguments for double click
	/// </summary>
	public class AuthenticatorListDoubleClickEventArgs : EventArgs
	{
		public WinAuthAuthenticator Authenticator;

		/// <summary>
		/// Default constructor
		/// </summary>
		public AuthenticatorListDoubleClickEventArgs(WinAuthAuthenticator auth)
			: base()
		{
			Authenticator = auth;
		}
	}

	/// <summary>
	/// An owner draw listbox that displays wrapped authenticators
	/// </summary>
	public partial class AuthenticatorListBox : ListBox
  {
		private const int MARGIN_LEFT = 4;
		private const int MARGIN_TOP = 8;
		private const int MARGIN_RIGHT = 8;
		private const int ICON_WIDTH = 48;
		private const int ICON_HEIGHT = 48;
		private const int ICON_MARGIN_RIGHT = 12;

		private const int LABEL_MARGIN_BOTTOM = 4;

		private const int LABEL_WIDTH = 250;

		private const int FONT_SIZE = 12;

		private const int PIE_WIDTH = 46;
		private const int PIE_HEIGHT = 46;
		private const int PIE_MARGIN = 2;
		private const int PIE_STARTANGLE = 270;
		private const int PIE_SWEEPANGLE = 360;

		/// <summary>
		/// Event handler for ItemRemoved
		/// </summary>
		public event AuthenticatorListItemRemovedHandler ItemRemoved;

		/// <summary>
		/// Event handler for Reordered
		/// </summary>
		public event AuthenticatorListReorderedHandler Reordered;

		/// <summary>
		/// Scrolled event handler
		/// </summary>
		[Category("Action")]
		public event ScrollEventHandler Scrolled = null;

		/// <summary>
		/// Event handler for double click
		/// </summary>
		public new event AuthenticatorListDoubleClickHandler DoubleClick;

		/// <summary>
		/// Rename textbox
		/// </summary>
		private TextBox _renameTextbox;

		/// <summary>
		/// Current item
		/// </summary>
		private AuthenticatorListitem _currentItem;

		/// <summary>
		/// Saved point of mouse down
		/// </summary>
		private Point _mouseDownLocation = Point.Empty;

		/// <summary>
		/// Saved pont of mouse move
		/// </summary>
		private Point _mouseMoveLocation = Point.Empty;

		/// <summary>
		/// Bitmap of cloned item we are dragging
		/// </summary>
		private Bitmap _draggedBitmap;

		/// <summary>
		/// Item that is being dragged
		/// </summary>
		private AuthenticatorListitem _draggedItem;

		/// <summary>
		/// Offset of last position of dragged bitmap so we can paint behind it
		/// </summary>
		private Rectangle _draggedBitmapRect;

		/// <summary>
		/// Offset of mouseY and top of item
		/// </summary>
		private int _draggedBitmapOffsetY;

		/// <summary>
		/// Last TopIndex if we scrolled while dragging
		/// </summary>
		private int? _lastDragTopIndex;

		/// <summary>
		/// When we last changed the TopIndex
		/// </summary>
		private DateTime _lastDragScroll;

		/// <summary>
		/// Default constructor for our authenticator list box
		/// </summary>
		public AuthenticatorListBox()
    {
			// set owner draw stlying
      this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
			this.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
			this.ReadOnly = true;
			this.AllowDrop = true;
			this.DoubleBuffered = true;

			// hook the scroll event
			this.Scrolled += AuthenticatorListBox_Scrolled;

			// hook the context menu
			this.ContextMenuStrip = new ContextMenuStrip();
			this.ContextMenuStrip.Opening += ContextMenuStrip_Opening;

			// preload the content menu
			loadContextMenuStrip();
    }

		/// <summary>
		/// Capture the mouse wheel events and scroll to appropriate position
		/// </summary>
		/// <param name="e"></param>
		protected override void OnMouseWheel(MouseEventArgs e)
		{
			base.OnMouseWheel(e);

			int y = (e.Delta * this.ItemHeight) + MARGIN_TOP;
			ScrollEventArgs sargs = new ScrollEventArgs(ScrollEventType.ThumbPosition, y, ScrollOrientation.VerticalScroll);
			Scrolled(this, sargs);
		}

		/// <summary>
		/// Copy the current code when double-clicking
		/// </summary>
		/// <param name="e"></param>
		protected override void OnMouseDoubleClick(MouseEventArgs e)
		{
			var item = this.CurrentItem;
			if (item != null)
			{
				DoubleClick(this, new AuthenticatorListDoubleClickEventArgs(item.Authenticator));
			}
		}



		#region Public Properties

		/// <summary>
		/// Readonly flag
		/// </summary>
		public bool ReadOnly { get; set; }

		/// <summary>
		/// The current (selected) authenticator
		/// </summary>
		public AuthenticatorListitem CurrentItem
		{
			get
			{
				if (_currentItem == null && this.Items.Count != 0)
				{
					_currentItem = (AuthenticatorListitem)this.Items[0];
				}
				return _currentItem;
			}
			set
			{
				_currentItem = value;
			}
		}

		#endregion

		#region Private methods

		/// <summary>
		/// Set the current item based on the mouse position
		/// </summary>
		/// <param name="mouseLocation">mouse position</param>
		private void SetCurrentItem(Point mouseLocation)
		{
			int index = this.IndexFromPoint(mouseLocation);
			if (index < 0)
			{
				index = 0;
			}
			else if (index >= this.Items.Count)
			{
				index = this.Items.Count - 1;
			}

			if (index >= this.Items.Count)
			{
				CurrentItem = null;
			}
			else
			{
				CurrentItem = this.Items[index] as AuthenticatorListitem;
			}
		}

		/// <summary>
		/// Set the current cursor based on position
		/// </summary>
		/// <param name="mouseLocation">current mouse position</param>
		private void SetCursor(Point mouseLocation, Cursor force = null)
		{
			// set cursor if we are over a refresh icon
			var cursor = Cursor.Current;
			if (force == null)
			{
				int index = this.IndexFromPoint(mouseLocation);
				if (index >= 0 && index < this.Items.Count)
				{
					var item = this.Items[index] as AuthenticatorListitem;
					int x = 0;
					int y = (this.ItemHeight * index) - (this.TopIndex * this.ItemHeight);
					bool hasvscroll = (this.Height < (this.Items.Count * this.ItemHeight));
					if (item.Authenticator.AutoRefresh == false && item.DisplayUntil < DateTime.Now
						&& new Rectangle(x + this.Width - (ICON_WIDTH + MARGIN_RIGHT) - (hasvscroll ? SystemInformation.VerticalScrollBarWidth : 0), y + MARGIN_TOP, ICON_WIDTH, ICON_HEIGHT).Contains(mouseLocation))
					{
						cursor = Cursors.Hand;
					}
				}
			}
			else
			{
				cursor = force;
			}
			if (Cursor.Current != cursor)
			{
				Cursor.Current = cursor;
			}
		}

		/// <summary>
		/// Unprotected an authenticator (if possible)
		/// </summary>
		/// <param name="item">item to unprotect</param>
		/// <param name="screen">screen to display dialog for multi-monitors</param>
		/// <returns></returns>
		private DialogResult UnprotectAuthenticator(AuthenticatorListitem item, Screen screen = null)
		{
			// keep a count so we can have multiples
			if (item.UnprotectCount > 0)
			{
				item.UnprotectCount++;
				return DialogResult.OK;
			}

			// if there is no protection return None
			WinAuthAuthenticator auth = item.Authenticator;
			if (auth.AuthenticatorData == null || auth.AuthenticatorData.RequiresPassword == false)
			{
				return DialogResult.None;
			}

			// request the password
			UnprotectPasswordForm getPassForm = new UnprotectPasswordForm();
			getPassForm.Authenticator = auth;
			if (screen != null)
			{
				// center on the current windows screen (in case of multiple monitors)
				getPassForm.StartPosition = FormStartPosition.Manual;
				int left = (screen.Bounds.Width / 2) - (getPassForm.Width / 2) + screen.Bounds.Left;
				int top = (screen.Bounds.Height / 2) - (getPassForm.Height / 2) + screen.Bounds.Top;
				getPassForm.Location = new Point(left, top);
			}
			else
			{
				getPassForm.StartPosition = FormStartPosition.CenterScreen;
			}
			DialogResult result = getPassForm.ShowDialog(this.Parent as Form);
			if (result == DialogResult.OK)
			{
				item.UnprotectCount++;
			}

			return result;
		}

		/// <summary>
		/// Reprotect the authenticator and each action or when the 30second window has expired
		/// </summary>
		/// <param name="item"></param>
		private void ProtectAuthenticator(AuthenticatorListitem item)
		{
			// if already protected just decrement counter
			item.UnprotectCount--;
			if (item.UnprotectCount > 0)
			{
				return;
			}

			// reprotect the authenticator
			WinAuthAuthenticator auth = item.Authenticator;
			if (auth.AuthenticatorData == null)
			{
				return;
			}
			auth.AuthenticatorData.Protect();
			item.UnprotectCount = 0;
		}




		/// <summary>
		/// Get the current code for an item in the list, unprotecting and reprotecting the authenticator if necessary
		/// </summary>
		/// <param name="item">List item to get code</param>
		/// <returns>code or null if failed (i.e. bad password)</returns>
		public string GetItemCode(AuthenticatorListitem item, Screen screen = null)
		{
			WinAuthAuthenticator auth = item.Authenticator;

			// check if the authentcated is still protected
			DialogResult wasprotected = UnprotectAuthenticator(item, screen);
			if (wasprotected == DialogResult.Cancel)
			{
				return null;
			}

			try {
				return auth.CurrentCode;
			}
			finally
			{
				if (wasprotected == DialogResult.OK)
				{
					ProtectAuthenticator(item);
				}
			}
		}

		/// <summary>
		/// Refresh the current item by invalidating it
		/// </summary>
		private void RefreshCurrentItem()
		{
			RefreshItem(this.CurrentItem);
		}

		/// <summary>
		/// Refresh the item by invalidating it
		/// </summary>
		private void RefreshItem(AuthenticatorListitem item)
		{
			bool hasvscroll = (this.Height < (this.Items.Count * this.ItemHeight));
			int y = (this.ItemHeight * item.Index) - (this.ItemHeight * this.TopIndex);
			Rectangle rect = new Rectangle(0, y, this.Width, this.Height);
			this.Invalidate(rect, false);
		}

		/// <summary>
		/// Convert a Bitmap into grayscale
		/// http://stackoverflow.com/questions/4669317/how-to-convert-a-bitmap-image-to-black-and-white-in-c
		/// </summary>
		/// <param name="bmp">Bitmap to convert</param>
		/// <returns>Original bitmap but grayscale</returns>
		private static Bitmap GrayScale(Bitmap bmp)
		{
			int rgb;
			Color c;

			for (int y = 0; y < bmp.Height; y++)
			{
				for (int x = 0; x < bmp.Width; x++)
				{
					c = bmp.GetPixel(x, y);
					rgb = (int)((c.R * 0.3) + (c.G * 0.59) + (c.B * 0.11));
					bmp.SetPixel(x, y, Color.FromArgb(rgb, rgb, rgb));
				}
			}
			return bmp;
		}

		/// <summary>
		/// Lighten or darken a bitmap
		/// </summary>
		/// <param name="bmp">Bitmap</param>
		/// <param name="amount">amount to change 0-255</param>
		/// <returns>Original bitmap but darkened</returns>
		private static Bitmap Lighten(Bitmap bmp, int amount)
		{
			Color c;
			int r, g, b;

			for (int y = 0; y < bmp.Height; y++)
			{
				for (int x = 0; x < bmp.Width; x++)
				{
					c = bmp.GetPixel(x, y);
					r = Math.Max(Math.Min(c.R + amount, 255), 0);
					g = Math.Max(Math.Min(c.G + amount, 255), 0);
					b = Math.Max(Math.Min(c.B + amount, 255), 0);
					bmp.SetPixel(x, y, Color.FromArgb(r, g, b));
				}
			}
			return bmp;
		}

#endregion

	}
}
