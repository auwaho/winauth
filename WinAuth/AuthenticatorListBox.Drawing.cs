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
#region Owner Draw

		/// <summary>
		/// Calculate the maximum available label with based on the currnet control size
		/// </summary>
		/// <param name="totalWidth">control size</param>
		/// <returns>maximum possible width for label</returns>
		protected int GetMaxAvailableLabelWidth(int totalWidth)
		{
			return totalWidth - MARGIN_LEFT - ICON_WIDTH - ICON_MARGIN_RIGHT - PIE_WIDTH - MARGIN_RIGHT;
		}

		/// <summary>
		/// Calculate the maximum label width based on the currnet names
		/// </summary>
		/// <param name="totalWidth">control size</param>
		/// <returns>maximum possible width for label</returns>
		public int GetMaxItemWidth()
		{
			var items = this.Items.Cast<AuthenticatorListitem>();

			if (items.Where(i => i.AutoWidth == 0).Count() != 0)
			{
				using (Graphics g = this.CreateGraphics())
				{
					using (var font = new Font(this.Font.FontFamily, FONT_SIZE, FontStyle.Regular))
					{
						foreach (AuthenticatorListitem item in items.Where(i => i.AutoWidth == 0))
						{
							WinAuthAuthenticator auth = item.Authenticator;

							SizeF labelsize = g.MeasureString(auth.Name, font);
							item.AutoWidth = MARGIN_LEFT + ICON_WIDTH + ICON_MARGIN_RIGHT + (int)Math.Ceiling(labelsize.Width) + PIE_WIDTH + MARGIN_RIGHT;
						}
					}
				}
			}

			int maxWidth = this.Items.Cast<AuthenticatorListitem>().Max(i => i.AutoWidth);
			return maxWidth;
		}

		/// <summary>
		/// Hook into the default WndProc to make sure we get our redraw messages
		/// </summary>
		/// <param name="m"></param>
		protected override void DefWndProc(ref Message m)
		{
			if (ReadOnly == false || ((m.Msg <= 0x0200 || m.Msg >= 0x020E)
				&& (m.Msg <= 0x0100 || m.Msg >= 0x0109)
				&& m.Msg != 0x2111
				&& m.Msg != 0x87))
			{
				base.DefWndProc(ref m);
			}
		}

		/// <summary>
		/// Event called when an item requires drawing
		/// </summary>
		/// <param name="e"></param>
		/// <param name="cliprect"></param>
		protected void OnDrawItem(DrawItemEventArgs e, Rectangle cliprect)
		{
			// no need to draw nothing
			if (this.Items.Count == 0 || e.Index < 0)
			{
				return;
			}

			AuthenticatorListitem item = this.Items[e.Index] as AuthenticatorListitem;
			WinAuthAuthenticator auth = item.Authenticator;

			// if the item is being dragged, we draw a blank placeholder
			if (item.Dragging == true)
			{
				if (cliprect.IntersectsWith(e.Bounds) == true)
				{
					using (var brush = new SolidBrush(SystemColors.ControlLightLight))
					{
						using (Pen pen = new Pen(SystemColors.Control))
						{
							e.Graphics.DrawRectangle(pen, e.Bounds);
							e.Graphics.FillRectangle(brush, e.Bounds);
						}
					}
				}
				return;
			}

			// draw the requested item
			using (var brush = new SolidBrush(e.ForeColor))
			{
				bool showCode = (auth.AutoRefresh == true || item.DisplayUntil > DateTime.Now);

				e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

				Rectangle rect = new Rectangle(e.Bounds.X + MARGIN_LEFT, e.Bounds.Y + MARGIN_TOP, ICON_WIDTH, ICON_HEIGHT);
				if (cliprect.IntersectsWith(rect) == true)
				{
					using (var icon = auth.Icon)
					{
						if (icon != null)
						{
							e.Graphics.DrawImage(icon, new PointF(e.Bounds.X + MARGIN_LEFT, e.Bounds.Y + MARGIN_TOP));
						}
					}
				}

				using (var font = new Font(e.Font.FontFamily, FONT_SIZE, FontStyle.Regular))
				{
					string label = auth.Name;
					SizeF labelsize = e.Graphics.MeasureString(label.ToString(), font);
					int labelMaxWidth = GetMaxAvailableLabelWidth(e.Bounds.Width);
					if (labelsize.Width > labelMaxWidth)
					{
						StringBuilder newlabel = new StringBuilder(label + "...");
						while ((labelsize = e.Graphics.MeasureString(newlabel.ToString(), font)).Width > labelMaxWidth)
						{
							newlabel.Remove(newlabel.Length - 4, 1);
						}
						label = newlabel.ToString();
					}
					rect = new Rectangle(e.Bounds.X + 64, e.Bounds.Y + MARGIN_TOP, (int)labelsize.Height, (int)labelsize.Width);
					if (cliprect.IntersectsWith(rect) == true)
					{
						e.Graphics.DrawString(label, font, brush, new RectangleF(e.Bounds.X + MARGIN_LEFT + ICON_WIDTH + ICON_MARGIN_RIGHT, e.Bounds.Y + MARGIN_TOP, labelMaxWidth, labelsize.Height));
					}

					string code;
					if (showCode == true)
					{
						try
						{
							// we we aren't autorefresh we just keep the same code up for the 10 seconds so it doesn't change even crossing the 30s boundary
							if (auth.AutoRefresh == false)
							{
								if (item.LastCode == null)
								{
									code = auth.CurrentCode;
								}
								else
								{
									code = item.LastCode;
								}
							}
							else
							{
								code = auth.CurrentCode;
								if (code != item.LastCode && auth.CopyOnCode == true)
								{
									// code has changed - copy to clipboard
									auth.CopyCodeToClipboard(this.Parent as Form, code);
								}
							}
							item.LastCode = code;
							if (code != null && code.Length > 5)
							{
								code = code.Insert(code.Length / 2, " ");
							}
						}
						catch (EncryptedSecretDataException )
						{
							code = "- - - - - -";
						}
					}
					else
					{
						code = "- - - - - -";
					}
					SizeF codesize = e.Graphics.MeasureString(code, e.Font);
					rect = new Rectangle(e.Bounds.X + MARGIN_LEFT + ICON_WIDTH + ICON_MARGIN_RIGHT, e.Bounds.Y + MARGIN_TOP + (int)labelsize.Height + LABEL_MARGIN_BOTTOM, (int)codesize.Width, (int)codesize.Height);
					if (cliprect.IntersectsWith(rect) == true)
					{
						e.Graphics.DrawString(code, e.Font, brush, new PointF(e.Bounds.X + MARGIN_LEFT + ICON_WIDTH + ICON_MARGIN_RIGHT, e.Bounds.Y + MARGIN_TOP + labelsize.Height + LABEL_MARGIN_BOTTOM));
					}
				}

				// draw the refresh image or pie
				rect = new Rectangle(e.Bounds.X + e.Bounds.Width - (MARGIN_RIGHT + ICON_WIDTH), e.Bounds.Y + MARGIN_TOP, ICON_WIDTH, ICON_HEIGHT);
				if (cliprect.IntersectsWith(rect) == true)
				{
					if (auth.AutoRefresh == true)
					{
						using (var piebrush = new SolidBrush(SystemColors.ActiveCaption))
						{
							using (var piepen = new Pen(SystemColors.ActiveCaption))
							{
                //int y = (this.TopIndex * this.ItemHeight) + e.Bounds.y
                //int tillUpdate = ((int)((auth.AuthenticatorData.ServerTime % 30000) / 1000L) + 1) * 12;
                int tillUpdate = (int)Math.Round((decimal)((auth.AuthenticatorData.ServerTime % ((long)auth.AuthenticatorData.Period * 1000L)) / 1000L) * (360M / (decimal)auth.AuthenticatorData.Period));
								e.Graphics.DrawPie(piepen, e.Bounds.X + e.Bounds.Width - (MARGIN_RIGHT + ICON_WIDTH), e.Bounds.Y + MARGIN_TOP + PIE_MARGIN, PIE_WIDTH, PIE_HEIGHT, PIE_STARTANGLE, PIE_SWEEPANGLE);
								e.Graphics.FillPie(piebrush, e.Bounds.X + e.Bounds.Width - (MARGIN_RIGHT + ICON_WIDTH), e.Bounds.Y + MARGIN_TOP + PIE_MARGIN, PIE_WIDTH, PIE_HEIGHT, PIE_STARTANGLE, tillUpdate);
							}
						}
					}
					else
					{
						if (showCode == true)
						{
							using (var piebrush = new SolidBrush(SystemColors.ActiveCaption))
							{
								using (var piepen = new Pen(SystemColors.ActiveCaption))
								{
									int tillUpdate = (int)((item.DisplayUntil.Subtract(DateTime.Now).TotalSeconds * (double)360) / item.DisplayUntil.Subtract(item.LastUpdate).TotalSeconds);
									e.Graphics.DrawPie(piepen, e.Bounds.X + e.Bounds.Width - (MARGIN_RIGHT + ICON_WIDTH), e.Bounds.Y + MARGIN_TOP + PIE_MARGIN, PIE_WIDTH, PIE_HEIGHT, PIE_STARTANGLE, PIE_SWEEPANGLE);
									e.Graphics.FillPie(piebrush, e.Bounds.X + e.Bounds.Width - (MARGIN_RIGHT + ICON_WIDTH), e.Bounds.Y + MARGIN_TOP + PIE_MARGIN, PIE_WIDTH, PIE_HEIGHT, PIE_STARTANGLE, tillUpdate);
								}
							}
						}
						else if (auth.AuthenticatorData != null && auth.AuthenticatorData.RequiresPassword == true)
						{
							e.Graphics.DrawImage(WinAuth.Properties.Resources.RefreshIconWithLock, rect);
						}
						else
						{
							e.Graphics.DrawImage(WinAuth.Properties.Resources.RefreshIcon, rect);
						}
					}
				}

				// draw the separating line
				rect = new Rectangle(e.Bounds.X, e.Bounds.Y + this.ItemHeight - 1, 1, 1);
				if (cliprect.IntersectsWith(rect) == true)
				{
					using (Pen pen = new Pen(SystemColors.Control))
					{
						e.Graphics.DrawLine(pen, e.Bounds.X, e.Bounds.Y + this.ItemHeight - 1, e.Bounds.X + e.Bounds.Width, e.Bounds.Y + this.ItemHeight - 1);
					}
				}
			}
		}

		/// <summary>
		/// Handle the paint event and work out if item needs redrawing
		/// </summary>
		/// <param name="e"></param>
    protected override void OnPaint(PaintEventArgs e)
    {
      using (var brush = new SolidBrush(this.BackColor))
      {
        Region region = new Region(e.ClipRectangle);

        e.Graphics.FillRegion(brush, region);
        if (this.Items.Count > 0)
        {
          for (int i = 0; i < this.Items.Count; ++i)
          {
            Rectangle irect = this.GetItemRectangle(i);
            if (e.ClipRectangle.IntersectsWith(irect))
            {
							if ((this.SelectionMode == SelectionMode.One && this.SelectedIndex == i)
							|| (this.SelectionMode == SelectionMode.MultiSimple && this.SelectedIndices.Contains(i))
							|| (this.SelectionMode == SelectionMode.MultiExtended && this.SelectedIndices.Contains(i)))
							{
								DrawItemEventArgs diea = new DrawItemEventArgs(e.Graphics, this.Font,
										irect, i,
										DrawItemState.Selected, this.ForeColor,
										this.BackColor);
								OnDrawItem(diea, e.ClipRectangle);
								base.OnDrawItem(diea);
							}
							else
							{
								DrawItemEventArgs diea = new DrawItemEventArgs(e.Graphics, this.Font,
										irect, i,
										DrawItemState.Default, this.ForeColor,
										this.BackColor);
								OnDrawItem(diea, e.ClipRectangle);
								base.OnDrawItem(diea);
              }
              region.Complement(irect);
            }
          }
        }
      }

      base.OnPaint(e);
		}

#endregion
}
}
