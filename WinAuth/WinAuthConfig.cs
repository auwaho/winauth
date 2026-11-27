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
  /// <summary>
  /// Delegate for ConfigChange event
  /// </summary>
  /// <param name="source"></param>
  /// <param name="args"></param>
  public delegate void ConfigChangedHandler(object source, ConfigChangedEventArgs args);

  /// <summary>
  /// Class holding configuration data for application
  /// </summary>
  [Serializable()]
	public partial class WinAuthConfig : IList<WinAuthAuthenticator>, ICloneable, IWinAuthAuthenticatorChangedListener
  {
    public static decimal CURRENTVERSION = decimal.Parse(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(2), System.Globalization.CultureInfo.InvariantCulture);

		/// <summary>
		/// Default actions for double-click or click system tray
		/// </summary>
		public enum NotifyActions
		{
			Notification = 0,
			CopyToClipboard = 1,
			HotKey = 2
		}

		/// <summary>
		/// Event handler fired when a config property is changed
		/// </summary>
		public event ConfigChangedHandler OnConfigChanged;

    /// <summary>
    /// Current file name
    /// </summary>
    private string _filename;

		/// <summary>
		/// Current version of this Config
		/// </summary>
    public decimal Version { get; private set; }

		/// <summary>
		/// Save password for re-saving and encrypting file
		/// </summary>
		public string Password { protected get; set; }

		/// <summary>
		/// Save Yubi key for re-saving and encrypting file
		/// </summary>
		public YubiKey Yubi { get; set; }

		/// <summary>
		/// If the config was upgraded
		/// </summary>
		public bool Upgraded { get; set; }

		/// <summary>
		/// Current encryption type
		/// </summary>
		private Authenticator.PasswordTypes _passwordType = Authenticator.PasswordTypes.None;

		/// <summary>
		/// Get/set the encryption type
		/// </summary>
		public Authenticator.PasswordTypes PasswordType
		{
			get
			{
				return _passwordType;
			}
			set
			{
				_passwordType = value;

				if ((_passwordType & Authenticator.PasswordTypes.Explicit) == 0)
				{
					this.Password = null;
				}
				if ((_passwordType & (Authenticator.PasswordTypes.YubiKeySlot1 | Authenticator.PasswordTypes.YubiKeySlot2)) == 0)
				{
					this.Yubi = null;
				}
			}
		}


    /// <summary>
    /// All authenticators
    /// </summary>
    private List<WinAuthAuthenticator> _authenticators = new List<WinAuthAuthenticator>();

    /// <summary>
    /// Current authenticator
    /// </summary>
    private WinAuthAuthenticator _authenticator;

    /// <summary>
    /// Flag for always on top
    /// </summary>
    private bool _alwaysOnTop;

    /// <summary>
    /// Flag to use tray icon
    /// </summary>
    private bool _useTrayIcon;

		/// <summary>
		/// Default action when click in system tray
		/// </summary>
		private NotifyActions _notifyAction;

		/// <summary>
		/// Flag to set start with Windows
		/// </summary>
		private bool _startWithWindows;

		/// <summary>
		/// Flag to size form based on numebr authenticators
		/// </summary>
		private bool _autoSize;

		/// <summary>
		/// Remember position
		/// </summary>
		private Point _position = Point.Empty;

		/// <summary>
		/// Width if not autosize
		/// </summary>
		private int _width;

		/// <summary>
		/// Height if not autosize
		/// </summary>
		private int _height;

		/// <summary>
		/// This config is readonly
		/// </summary>
		private bool _readOnly;

		/// <summary>
		/// MetroFormShadowType for main form
		/// </summary>
		private string _shadowType;

		/// <summary>
		/// User's own PGPKey for backups
		/// </summary>
		private string _pgpKey;

		/// <summary>
		/// Class used to serialize the settings inside the Xml config file
		/// </summary>
		[XmlRoot(ElementName="settings")]
		public class setting
		{
			/// <summary>
			/// Name of dictionary entry
			/// </summary>
			[XmlAttribute(AttributeName="key")]
			public string Key;

			/// <summary>
			/// Value of dictionary entry
			/// </summary>
			[XmlAttribute(AttributeName = "value")]
			public string Value;
		}

		/// <summary>
		/// Inline settings for Portable mode
		/// </summary>
		private Dictionary<string, string> _settings = new Dictionary<string, string>();



		#region Authenticator Settings

    /// <summary>
    /// Current authenticator
    /// </summary>
    public WinAuthAuthenticator CurrentAuthenticator
    {
      get
      {
        return _authenticator;
      }
      set
      {
        _authenticator = value;
      }
    }

    #endregion

    /// <summary>
    /// Create a default config object
    /// </summary>
    public WinAuthConfig()
    {
      Version = CURRENTVERSION;
			AutoSize = true;
			NotifyAction = NotifyActions.Notification;
    }

		public void OnWinAuthAuthenticatorChanged(WinAuthAuthenticator sender, WinAuthAuthenticatorChangedEventArgs e)
		{
			if (OnConfigChanged != null)
			{
				OnConfigChanged(this, new ConfigChangedEventArgs("Authenticator", sender, e));
			}
		}


	}

  /// <summary>
  /// Config change event arguments
  /// </summary>
  public class ConfigChangedEventArgs : EventArgs
  {
		public string PropertyName { get; private set; }

		public WinAuthAuthenticator Authenticator { get; private set; }
		public WinAuthAuthenticatorChangedEventArgs AuthenticatorChangedEventArgs { get; private set; }

    /// <summary>
    /// Default constructor
    /// </summary>
		public ConfigChangedEventArgs(string propertyName, WinAuthAuthenticator authenticator = null, WinAuthAuthenticatorChangedEventArgs acargs = null)
			: base()
		{
			PropertyName = propertyName;
			Authenticator = authenticator;
			AuthenticatorChangedEventArgs = acargs;
		}
	}

  public class WinAuthInvalidConfigException : ApplicationException
  {
    public WinAuthInvalidConfigException(string msg, Exception ex)
      : base(msg, ex)
    {
    }
  }
  public class WinAuthConfigRequirePasswordException : ApplicationException
  {
    public WinAuthConfigRequirePasswordException()
      : base()
    {
    }
  }
	public class WinAuthInvalidNewerConfigException : ApplicationException
	{
		public WinAuthInvalidNewerConfigException(string msg)
			: base(msg)
		{
		}
	}

}
