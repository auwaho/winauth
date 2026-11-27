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
#region Custom Events

    /// <summary>
    /// Event called when any property in the config is changed
    /// </summary>
    /// <param name="source"></param>
    /// <param name="args"></param>
    void OnConfigChanged(object source, ConfigChangedEventArgs args)
    {
			if (args.PropertyName == "AlwaysOnTop")
			{
				this.TopMost = this.Config.AlwaysOnTop;
			}
			else if (args.PropertyName == "UseTrayIcon")
			{
				bool useTrayIcon = Config.UseTrayIcon;
				if (useTrayIcon == false && this.Visible == false)
				{
					BringToFront();
					Show();
					WindowState = FormWindowState.Normal;
					Activate();
				}
				notifyIcon.Visible = useTrayIcon;
			}
			else if (args.PropertyName == "AutoSize" || (args.PropertyName == "Authenticator" && args.AuthenticatorChangedEventArgs.Property == "Name"))
			{
				setAutoSize();
				this.Invalidate();
			}
			else if (args.PropertyName == "StartWithWindows")
			{
				if (this.Config.IsPortable == false)
				{
					WinAuthHelper.SetStartWithWindows(this.Config.StartWithWindows);
				}
			}
			else if (args.AuthenticatorChangedEventArgs != null && args.AuthenticatorChangedEventArgs.Property == "HotKey")
			{
				// rehook hotkeys
				HookHotkeys();
			}

			// batch up saves so they can be done out of line
			SaveConfig();
    }
#endregion
}
}
