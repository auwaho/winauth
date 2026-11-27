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
		#region Control Events

		/// <summary>
		/// Resize event to resize base and fix rename box location
		/// </summary>
		/// <param name="e"></param>
		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			setRenameTextboxLocation();
		}

		/// <summary>
		/// Fix position of rename box when scrolling
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void AuthenticatorListBox_Scrolled(object sender, ScrollEventArgs e)
		{
			if (e.Type == ScrollEventType.EndScroll || e.Type == ScrollEventType.ThumbPosition)
			{
				setRenameTextboxLocation();
			}
		}

		/// <summary>
		/// Click an item in the context menu
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void ContextMenu_Click(object sender, EventArgs e)
		{
			ProcessMenu((ToolStripItem)sender);
		}

		/// <summary>
		/// Click to open the contxet menu and set the state
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void ContextMenuStrip_Opening(object sender, CancelEventArgs e)
		{
			SetContextMenuItems();
		}

		/// <summary>
		/// Tick event used to update the pie, codes and relock authenticators
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public void Tick(object sender, EventArgs e)
		{
			for (int index = 0; index < this.Items.Count; index++)
			{
				// get the item
				AuthenticatorListitem item = this.Items[index] as AuthenticatorListitem;
				WinAuthAuthenticator auth = item.Authenticator;

				int y = (this.ItemHeight * index) - (this.TopIndex * this.ItemHeight);
				if (auth.AutoRefresh == true)
				{
          // for autorefresh we repaint the pie or the code too
          //int tillUpdate = (int)((auth.AuthenticatorData.ServerTime % ((long)auth.AuthenticatorData.Period * 1000L)) / 1000L);
          int tillUpdate = (int)Math.Round((decimal)((auth.AuthenticatorData.ServerTime % ((long)auth.AuthenticatorData.Period * 1000L)) / 1000L) * (360M / (decimal)auth.AuthenticatorData.Period));
          if (item.LastUpdate == DateTime.MinValue || tillUpdate == 0)
					{
						this.Invalidate(new Rectangle(0, y, this.Width, this.ItemHeight), false);
						item.LastUpdate = DateTime.Now;
					}
					else
					{
						this.Invalidate(new Rectangle(0, y, this.Width, this.ItemHeight), false);
						item.LastUpdate = DateTime.Now;
					}
				}
				else
				{
					// check if we need to redraw
					if (item.DisplayUntil != DateTime.MinValue)
					{
						// clear the item
						if (item.DisplayUntil <= DateTime.Now)
						{
							item.DisplayUntil = DateTime.MinValue;
							item.LastUpdate = DateTime.MinValue;
							item.LastCode = null;

							if (item.Authenticator.AuthenticatorData != null && item.Authenticator.AuthenticatorData.PasswordType == Authenticator.PasswordTypes.Explicit)
							{
								ProtectAuthenticator(item);
							}

							SetCursor(this.PointToClient(Control.MousePosition));
						}
						this.Invalidate(new Rectangle(0, y, this.Width, this.ItemHeight), false);
					}
				}
			}
		}

		/// <summary>
		/// Handle mouse down event
		/// </summary>
		/// <param name="e"></param>
		protected override void OnMouseDown(MouseEventArgs e)
		{
			// set the current item based on position
			SetCurrentItem(e.Location);
			_mouseDownLocation = e.Location;

			base.OnMouseDown(e);
		}

		/// <summary>
		/// Handle the mouse up and check for click in the refresh icon
		/// </summary>
		/// <param name="e"></param>
		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);

			if ((e.Button & System.Windows.Forms.MouseButtons.Left) != 0)
			{
				// if this was in a refresh icon, we do a refresh
				int index = this.IndexFromPoint(e.Location);
				if (index >= 0 && index < this.Items.Count)
				{
					var item = this.Items[index] as AuthenticatorListitem;
					int x = 0;
					int y = (this.ItemHeight * index) - (this.ItemHeight * this.TopIndex);
					bool hasvscroll = (this.Height < (this.Items.Count * this.ItemHeight));
					if (item.Authenticator.AutoRefresh == false && item.DisplayUntil < DateTime.Now
						&& new Rectangle(x + this.Width - (ICON_WIDTH + MARGIN_RIGHT) - (hasvscroll ? SystemInformation.VerticalScrollBarWidth : 0), y + MARGIN_TOP, ICON_WIDTH, ICON_HEIGHT).Contains(e.Location))
					{
						if (UnprotectAuthenticator(item) == DialogResult.Cancel)
						{
							return;
						}

						item.LastCode = item.Authenticator.CurrentCode;
						item.LastUpdate = DateTime.Now;
						item.DisplayUntil = DateTime.Now.AddSeconds(10);

						if (item.Authenticator.CopyOnCode == true)
						{
							// copy to clipboard
							item.Authenticator.CopyCodeToClipboard(this.Parent as Form);
						}

						RefreshCurrentItem();
					}
				}
			}

			// dispose and reset the dragging
			_mouseDownLocation = Point.Empty;
			if (_draggedBitmap != null)
			{
				_draggedBitmap.Dispose();
				_draggedBitmap = null;
				this.Invalidate(_draggedBitmapRect);
			}
			if (_draggedItem != null)
			{
				_draggedItem = null;
			}
		}

		/// <summary>
		/// Handle the mouse move event to capture cursor position
		/// </summary>
		/// <param name="e"></param>
		protected override void OnMouseMove(MouseEventArgs e)
		{
			_mouseMoveLocation = e.Location;

			// if we are moving with LeftMouse down and moved more than 2 pixles then we are dragging
			if (e.Button == System.Windows.Forms.MouseButtons.Left && _mouseDownLocation != Point.Empty && this.Items.Count > 1)
			{
				int dx = Math.Abs(_mouseDownLocation.X - e.Location.X);
				int dy = Math.Abs(_mouseDownLocation.Y - e.Location.Y);
				if (dx > 2 || dy > 2)
				{
					_draggedItem = this.CurrentItem;

					// get a snapshot of the current item for the drag
					bool hasvscroll = (this.Height < (this.Items.Count * this.ItemHeight));
					_draggedBitmap = new Bitmap(this.Width - (hasvscroll ? SystemInformation.VerticalScrollBarWidth : 0), this.ItemHeight);
					_draggedBitmapRect = Rectangle.Empty;
					using (Graphics g = Graphics.FromImage(_draggedBitmap))
					{
						int y = (this.ItemHeight * this.CurrentItem.Index) - (this.ItemHeight * this.TopIndex);

						Point screen = this.Parent.PointToScreen(new Point(this.Location.X, this.Location.Y + y));
						g.CopyFromScreen(screen.X, screen.Y, 0, 0, new Size(this.Width - (hasvscroll ? SystemInformation.VerticalScrollBarWidth : 0), this.ItemHeight), CopyPixelOperation.SourceCopy);

						// save offset in Y from top of item
						_draggedBitmapOffsetY = e.Y - y;
					}

					// make the bitmap darker
					Lighten(_draggedBitmap, -10);

					_draggedItem.Dragging = true;
					this.RefreshItem(_draggedItem);

					// moved enough so start drag
					this.DoDragDrop(_draggedItem, DragDropEffects.Move);
				}
			}
			else if (e.Button == System.Windows.Forms.MouseButtons.None)
			{
				SetCursor(e.Location);
			}

			base.OnMouseMove(e);
		}

		/// <summary>
		/// When we are dragging over
		/// </summary>
		/// <param name="e"></param>
		protected override void OnDragOver(DragEventArgs e)
		{
			Rectangle screen = this.Parent.RectangleToScreen(new Rectangle(this.Location.X, this.Location.Y, this.Width, this.Height));
			Point mousePoint = new Point(e.X, e.Y);
			if (screen.Contains(mousePoint) == false)
			{
				e.Effect = DragDropEffects.None;
			}
			else if (_draggedBitmap != null)
			{
				e.Effect = DragDropEffects.Move;
				using (Graphics g = this.CreateGraphics())
				{
					Point mouseClientPoint = this.PointToClient(mousePoint);
					int x = 0;
					int y = Math.Max(mouseClientPoint.Y - _draggedBitmapOffsetY, 0);
					Rectangle rect = new Rectangle(x, y, _draggedBitmap.Width, _draggedBitmap.Height);
					g.DrawImageUnscaled(_draggedBitmap, rect);

					if (_draggedBitmapRect != Rectangle.Empty)
					{
						// invalidate the extent between the old rect and this one
						Region region = new Region(rect);
						region.Union(_draggedBitmapRect);
						region.Exclude(rect);
						this.Invalidate(region);
					}

					_draggedBitmapRect = rect;
				}
			}
		}

		/// <summary>
		/// Check if we should continue dragging
		/// </summary>
		/// <param name="e"></param>
		protected override void OnQueryContinueDrag(QueryContinueDragEventArgs e)
		{
			Rectangle screen = this.Parent.RectangleToScreen(new Rectangle(this.Location.X, this.Location.Y, this.Width, this.Height));
			Point mousePoint = Cursor.Position;

			// if ESC is pressed, always stop
			if (e.EscapePressed == true || ((e.KeyState & 1) == 0 && screen.Contains(mousePoint) == false))
			{
				e.Action = DragAction.Cancel;

				if (_draggedItem != null)
				{
					_draggedItem.Dragging = false;
					this.RefreshItem(_draggedItem);
					_draggedItem = null;
				}
				if (_draggedBitmap != null)
				{
					_draggedBitmap.Dispose();
					_draggedBitmap = null;
					this.Invalidate(_draggedBitmapRect);
				}
				_mouseDownLocation = Point.Empty;
			}
			else
			{
				DateTime now = DateTime.Now;

				// if we are at the top or bottom, scroll every 150ms
				if (mousePoint.Y >= screen.Bottom)
				{
					int visible = this.ClientSize.Height / this.ItemHeight;
					int maxtopindex = Math.Max(this.Items.Count - visible + 1, 0);
					if (this.TopIndex < maxtopindex && now.Subtract(_lastDragScroll).TotalMilliseconds >= 150)
					{
						this.TopIndex++;
						_lastDragScroll = now;
						this.Refresh();
					}
				}
				else if (mousePoint.Y <= screen.Top)
				{
					int visible = this.ClientSize.Height / this.ItemHeight;
					if (this.TopIndex > 0 && now.Subtract(_lastDragScroll).TotalMilliseconds >= 150)
					{
						this.TopIndex--;
						_lastDragScroll = now;
						this.Refresh();
					}
				}
				_lastDragTopIndex = this.TopIndex;

				base.OnQueryContinueDrag(e);
			}
		}

		/// <summary>
		/// When a dragdrop operation has completed
		/// </summary>
		/// <param name="e"></param>
		protected override void OnDragDrop(DragEventArgs e)
		{
			AuthenticatorListitem item = e.Data.GetData(typeof(AuthenticatorListitem)) as AuthenticatorListitem;
			if (item != null)
			{
				// stop paiting as we reorder to reduce flicker
				WinAPI.SendMessage(this.Handle, WinAPI.WM_SETREDRAW, 0, IntPtr.Zero);
				try
				{
					// get the new index
					Point point = this.PointToClient(new Point(e.X, e.Y));
					int index = this.IndexFromPoint(point);
					if (index < 0)
					{
						index = this.Items.Count - 1;
					}
					// move the item
					this.Items.Remove(item);
					this.Items.Insert(index, item);

					// set the correct indexes of our items
					for (int i = 0; i < this.Items.Count; i++)
					{
						(this.Items[i] as AuthenticatorListitem).Index = i;
					}

					// fire the reordered event
					Reordered(this, new AuthenticatorListReorderedEventArgs());

					// clear state
					item.Dragging = false;
					_draggedItem = null;
					if (_draggedBitmap != null)
					{
						_draggedBitmap.Dispose();
						_draggedBitmap = null;
					}

					if (_lastDragTopIndex != null)
					{
						this.TopIndex = _lastDragTopIndex.Value;
					}
				}
				finally
				{
					// resume painting
					WinAPI.SendMessage(this.Handle, WinAPI.WM_SETREDRAW, 1, IntPtr.Zero);
				}
				this.Refresh();
			}
			else
			{
				base.OnDragDrop(e);
			}
		}

		/// <summary>
		/// Main WndProc handler to capture scroll events
		/// </summary>
		/// <param name="msg"></param>
		protected override void WndProc(ref System.Windows.Forms.Message msg)
		{
			if (msg.Msg == WinAPI.WM_VSCROLL)
			{
				if (Scrolled != null)
				{
					WinAPI.ScrollInfoStruct si = new WinAPI.ScrollInfoStruct();
					si.fMask = WinAPI.SIF_ALL;
					si.cbSize = Marshal.SizeOf(si);
					WinAPI.GetScrollInfo(msg.HWnd, 0, ref si);

					if (msg.WParam.ToInt32() == WinAPI.SB_ENDSCROLL)
					{
						ScrollEventArgs sargs = new ScrollEventArgs(ScrollEventType.EndScroll, si.nPos);
						Scrolled(this, sargs);
					}
					else if (msg.WParam.ToInt32() == WinAPI.SB_THUMBTRACK)
					{
						ScrollEventArgs sargs = new ScrollEventArgs(ScrollEventType.ThumbTrack, si.nPos);
						Scrolled(this, sargs);
					}
				}
			}

			base.WndProc(ref msg);
		}

		#endregion
}
}
