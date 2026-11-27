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
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using WinAuth.Resources;

namespace WinAuth
{
	public partial class WinAuthConfig : IList<WinAuthAuthenticator>, ICloneable, IWinAuthAuthenticatorChangedListener
{
		#region System Settings

    /// <summary>
    /// Get/set file name of config data
    /// </summary>
    public string Filename
    {
      get
      {
        return _filename;
      }
      set
      {
        _filename = value;
      }
    }

    /// <summary>
    /// Get/set on top flag
    /// </summary>
    public bool AlwaysOnTop
    {
      get
      {
        return _alwaysOnTop;
      }
      set
      {
        _alwaysOnTop = value;
        if (OnConfigChanged != null)
        {
          OnConfigChanged(this, new ConfigChangedEventArgs("AlwaysOnTop"));
        }
      }
    }

    /// <summary>
    /// Get/set use tray icon top flag
    /// </summary>
    public bool UseTrayIcon
    {
      get
      {
        return _useTrayIcon;
      }
      set
      {
        _useTrayIcon = value;
        if (OnConfigChanged != null)
        {
          OnConfigChanged(this, new ConfigChangedEventArgs("UseTrayIcon"));
        }
      }
    }

		/// <summary>
		/// Default action when using the system tray or double-click
		/// </summary>
		public NotifyActions NotifyAction
		{
			get
			{
				return _notifyAction;
			}
			set
			{
				_notifyAction = value;
				if (OnConfigChanged != null)
				{
					OnConfigChanged(this, new ConfigChangedEventArgs("NotifyAction"));
				}
			}
		}

		/// <summary>
		/// Get/set start with windows flag
		/// </summary>
		public bool StartWithWindows
    {
      get
      {
        return _startWithWindows;
      }
      set
      {
        _startWithWindows = value;
        if (OnConfigChanged != null)
        {
          OnConfigChanged(this, new ConfigChangedEventArgs("StartWithWindows"));
        }
      }
    }

		/// <summary>
		/// Get/set start with windows flag
		/// </summary>
		public bool AutoSize
		{
			get
			{
				return _autoSize;
			}
			set
			{
				_autoSize = value;
				if (OnConfigChanged != null)
				{
					OnConfigChanged(this, new ConfigChangedEventArgs("AutoSize"));
				}
			}
		}

		/// <summary>
		/// Get/set the position
		/// </summary>
		public Point Position
		{
			get
			{
				return _position;
			}
			set
			{
				if (_position != value)
				{
					_position = value;
					if (OnConfigChanged != null)
					{
						OnConfigChanged(this, new ConfigChangedEventArgs("Position"));
					}
				}
			}
		}

		/// <summary>
		/// Saved window width
		/// </summary>
		public int Width
		{
			get
			{
				return _width;
			}
			set
			{
				if (_width != value)
				{
					_width = value;
					if (OnConfigChanged != null)
					{
						OnConfigChanged(this, new ConfigChangedEventArgs("Width"));
					}
				}
			}
		}

		/// <summary>
		/// Saved window height
		/// </summary>
		public int Height
		{
			get
			{
				return _height;
			}
			set
			{
				if (_height != value)
				{
					_height = value;
					if (OnConfigChanged != null)
					{
						OnConfigChanged(this, new ConfigChangedEventArgs("Height"));
					}
				}
			}
		}

		/// <summary>
		/// Get/set shadow type for main form
		/// </summary>
		public string ShadowType
		{
			get
			{
				return _shadowType;
			}
			set
			{
				_shadowType = value;
				if (OnConfigChanged != null)
				{
					OnConfigChanged(this, new ConfigChangedEventArgs("ShadowType"));
				}
			}
		}

		/// <summary>
		/// Get user's own PGP key
		/// </summary>
		public string PGPKey
		{
			get
			{
				return _pgpKey;
			}
		}

		/// <summary>
		/// Return if we are in portable mode, which is when the config filename is in teh same directory as the exe
		/// </summary>
		public bool IsPortable
		{
			get
			{
				return (string.IsNullOrEmpty(this.Filename) == false
					&& string.Compare(Path.GetDirectoryName(this.Filename), Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), true) == 0);
			}
		}

		/// <summary>
		/// Read a setting value.
		/// </summary>
		/// <param name="name">name of setting</param>
		/// <param name="defaultValue">default value if setting doesn't exist</param>
		/// <returns>setting value or default value</returns>
		public string ReadSetting(string name, string defaultValue = null)
		{
			if (this.IsPortable == true)
			{
				// read setting from _settings
				string value;
				if (_settings.TryGetValue(name, out value) == true)
				{
					return value;
				}
				else
				{
					return defaultValue;
				}
			}
			else
			{
				return WinAuthHelper.ReadRegistryValue(name, defaultValue) as string;
			}
		}

		/// <summary>
		/// Get all the settings keys beneath the specified key
		/// </summary>
		/// <param name="name">name of parent key</param>
		/// <returns>string array of all child (recursively) setting names. Empty is none.</returns>
		public string[] ReadSettingKeys(string name)
		{
			if (this.IsPortable == true)
			{
				List<string> keys = new List<string>();
				foreach (var entry in _settings)
				{
					if (entry.Key.StartsWith(name) == true)
					{
						keys.Add(entry.Key);
					}
				}
				return keys.ToArray();
			}
			else
			{
				return WinAuthHelper.ReadRegistryKeys(name);
			}
		}

		/// <summary>
		/// Write a setting value into the Config
		/// </summary>
		/// <param name="name">name of setting value</param>
		/// <param name="value">setting value. If null, the setting is deleted.</param>
		public void WriteSetting(string name, string value)
		{
			if (this.IsPortable == true)
			{
				if (value == null)
				{
					if (_settings.ContainsKey(name) == true)
					{
						_settings.Remove(name);
					}
				}
				else
				{
					_settings[name] = value;
				}
			}
			else
			{
				if (value == null)
				{
					WinAuthHelper.DeleteRegistryKey(name);
				}
				else
				{
					WinAuthHelper.WriteRegistryValue(name, value);
				}
			}
		}

		/// <summary>
		/// Check if a given password is the config's password
		/// </summary>
		/// <param name="password"></param>
		/// <returns></returns>
		public bool IsPassword(string password)
		{
			return (string.Compare(password, this.Password) == 0);
		}

    #endregion
}
}
