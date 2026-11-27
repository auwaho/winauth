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
		
		public WinAuthForm()
    {
      InitializeComponent();
    }

#region Properties

		/// <summary>
		/// The current winauth config
		/// </summary>
    public WinAuthConfig Config { get; set; }

		/// <summary>
		/// Self-updating object
		/// </summary>
		private WinAuthUpdater Updater { get; set; }

		/// <summary>
		/// Datetime for when we should save config
		/// </summary>
		private DateTime? _saveConfigTime;

		/// <summary>
		/// Flag used to set AutoSizing based on authenticators
		/// </summary>
		public bool AutoAuthenticatorSize { get; set; }

		/// <summary>
		/// Flag used to see if we are closing manually
		/// </summary>
		private bool m_explictClose;

		/// <summary>
		/// Hook for hotkey to send code to window
		/// </summary>
		private KeyboardHook m_hook;

		/// <summary>
		/// Handle for USB notification hook
		/// </summary>
		private IntPtr m_usbHandle;

		/// <summary>
		/// Current list of USB YubiKeys
		/// </summary>
		private List<HIDDevice.HIDDeviceEntry> m_yubis;

		/// <summary>
		/// Flag to say we are processing sending message to other window
		/// </summary>
		private object m_sendingKeys = new object();

		/// <summary>
		/// Delegates for clipbaord manipulation
		/// </summary>
		/// <param name="data"></param>
		public delegate void SetClipboardDataDelegate(object data);
		public delegate object GetClipboardDataDelegate(Type format);

		/// <summary>
		/// Save the position of the list within the form for starting as minimized
		/// </summary>
		private Rectangle _listoffset;

		/// <summary>
		/// If we were passed command line arg to minimise
		/// </summary>
		private bool _initiallyMinimised;

		/// <summary>
		/// Existing v2 config file so we can prompt for import
		/// </summary>
		private string _existingv2Config;

		private string _startupConfigFile;

		/// <summary>
		/// Forwarder for mousewheel messages to list control
		/// </summary>
		private WinAPI.MessageForwarder _wheelMessageForwarder;

		/// <summary>
		/// First time only initialzation
		/// </summary>
		private bool m_initOnce;

		/// <summary>
		/// Initial form size so we can reset
		/// </summary>
		private Size m_initialSize;

		/// <summary>
		/// Locker for WM_DEVICECHANGE
		/// </summary>
		private object m_deviceArrivalMutex = new object();

#endregion

		/// <summary>
		/// Load the main form
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
    private void WinAuthForm_Load(object sender, EventArgs e)
    {
      // get any command arguments
			string password = null;
			string proxy = null;
      string[] args = Environment.GetCommandLineArgs();
      for (int i = 1; i < args.Length; i++)
      {
        string arg = args[i];
        if (arg[0] == '-')
        {
          switch (arg)
          {
            case "-min":
            case "--minimize":
              // set initial state as minimized
							_initiallyMinimised = true;
              break;
            case "-p":
            case "--password":
              // set explicit password to use
              i++;
							password = args[i];
              break;
						case "--proxy":
							// set proxy [user[:pass]@]ip[:host]
							i++;
							proxy = args[i];
							break;
						case "-l":
						case "--log":
							i++;
							FieldInfo fi = typeof(LogLevel).GetField(args[i], BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Public);
							if (fi == null)
							{
								WinAuthForm.ErrorDialog(this, "Invalid parameter: log value: " + args[i] + " (must be error,info,debug,trace)");
								System.Diagnostics.Process.GetCurrentProcess().Kill();
							}
							var loglevel = fi.GetValue(null) as LogLevel;
							var target = NLog.LogManager.Configuration.AllTargets.Where(t => t.Name == null).FirstOrDefault();
							if (target != null)
							{
								LogManager.Configuration.LoggingRules.Add(new NLog.Config.LoggingRule("*", loglevel, target));
							}
							break;
						default:
              break;
          }
        }
        else
        {
					_startupConfigFile = arg;
        }
      }

			// set the default web proxy
			if (string.IsNullOrEmpty(proxy) == false)
			{
				try
				{
					Uri uri = new Uri(proxy.IndexOf("://") == -1 ? "http://" + proxy : proxy);
					WebProxy webproxy = new WebProxy(uri.Host + ":" + uri.Port, true);
					if (string.IsNullOrEmpty(uri.UserInfo) == false)
					{
						string[] auth = uri.UserInfo.Split(':');
						webproxy.Credentials = new NetworkCredential(auth[0], (auth.Length > 1 ? auth[1] : string.Empty));
					}
					WebRequest.DefaultWebProxy = webproxy;
				}
				catch (UriFormatException )
				{
					ErrorDialog(this, "Invalid proxy value (" + proxy + ")" + Environment.NewLine + Environment.NewLine + "Use --proxy [user[:password]@]ip[:port], e.g. 127.0.0.1:8080 or me:mypass@10.0.0.1:8080");
					this.Close();
				}
			}

			InitializeOnce();

			loadConfig(password);
		}

#region Private Methods




		private void InitializeOnce()
		{
			if (m_initOnce == false)
			{

				// hook into System time change event
				Microsoft.Win32.SystemEvents.TimeChanged += new EventHandler(SystemEvents_TimeChanged);

				// save the initial form size
				m_initialSize = this.Size;

				// redirect mouse wheel events
				_wheelMessageForwarder = new WinAPI.MessageForwarder(authenticatorList, WinAPI.WM_MOUSEWHEEL);

				// get the current USB devices
				m_yubis = HIDDevice.GetAllDevices(YubiKey.VENDOR_ID);

				// register for USB changes
				m_usbHandle = WinAPI.RegisterUsbDeviceNotification(this.Handle);

				m_initOnce = true;
			}
		}

		/// <summary>
		/// Initialise the current form and UI
		/// </summary>
		private void InitializeForm()
		{
			// set up list
			loadAuthenticatorList();

			// set always on top
			this.TopMost = this.Config.AlwaysOnTop;

			// size the form based on the authenticators
			setAutoSize();

			// initialize UI
			LoadAddAuthenticatorTypes();
			loadOptionsMenu(this.optionsMenu);
			loadNotifyMenu(this.notifyMenu);
			loadingPanel.Visible = false;
			passwordPanel.Visible = false;
			yubiPanel.Visible = false;
			commandPanel.Visible = true;
			introLabel.Visible = (this.Config.Count == 0);
			authenticatorList.Visible = (this.Config.Count != 0);
			this.addAuthenticatorButton.Visible = !this.Config.IsReadOnly;

			// set title
			notifyIcon.Visible = this.Config.UseTrayIcon;
			notifyIcon.Text = this.Text = WinAuthMain.APPLICATION_TITLE;

			// hook hotkeys
			HookHotkeys();

			// hook Steam notifications
			HookSteam();

			// save the position of the list within the form else starting as minimized breaks the size
			_listoffset = new Rectangle(authenticatorList.Left, authenticatorList.Top, (this.Width - authenticatorList.Width), (this.Height - authenticatorList.Height));

			// set the shadow type (change in config for compatibility)
			try
			{
				MetroFormShadowType shadow = (MetroFormShadowType)Enum.Parse(typeof(MetroFormShadowType), this.Config.ShadowType, true);
				this.ShadowType = shadow;
			}
			catch (Exception) { }

			// set positions
			if (this.Config.Position.IsEmpty == false)
			{
				// check we aren't out of bounds in case of multi-monitor change
				var v = SystemInformation.VirtualScreen;
				if ((this.Config.Position.X + this.Width) >= v.Left && this.Config.Position.X < v.Width && this.Config.Position.Y > v.Top)
				{
					try
					{
						this.StartPosition = FormStartPosition.Manual;
						this.Left = this.Config.Position.X;
						this.Top = this.Config.Position.Y;
					}
					catch (Exception) { }
				}

				// check we aren't below the taskbar
				int lowesty = Screen.GetWorkingArea(this).Bottom;
				var bottom = this.Top + this.Height;
				if (bottom > lowesty)
				{
					this.Top -= (bottom - lowesty);
					if (this.Top < 0)
					{
						this.Height += this.Top;
						Top = 0;
					}
				}
			}
			else if (this.Config.AutoSize == true)
			{
				this.CenterToScreen();
			}

			// if we passed "-min" flag
			if (_initiallyMinimised == true)
			{
				this.WindowState = FormWindowState.Minimized;
				this.ShowInTaskbar = true;
			}
			if (this.Config.UseTrayIcon == true)
			{
				notifyIcon.Visible = true;
				notifyIcon.Text = this.Text;

				// if initially minimized, we need to hide
				if (WindowState == FormWindowState.Minimized)
				{
					// hide this and metro owner
					Form form = this;
					do
					{
						form.Hide();
					} while ((form = form.Owner) != null);
				}
			}
		}

		/// <summary>
		/// Load the authenticators into the display list
		/// </summary>
		/// <param name="added">authenticator we just added</param>
		private void loadAuthenticatorList(WinAuthAuthenticator added = null)
		{
			// set up list
			authenticatorList.Items.Clear();

			int index = 0;
			foreach (var auth in Config)
			{
				var ali = new AuthenticatorListitem(auth, index);
				if (added != null && added == auth && auth.AutoRefresh == false && !(auth.AuthenticatorData is HOTPAuthenticator))
				{
					ali.LastUpdate = DateTime.Now;
					ali.DisplayUntil = DateTime.Now.AddSeconds(10);
				}
				authenticatorList.Items.Add(ali);
				index++;
			}

			authenticatorList.Visible = (authenticatorList.Items.Count != 0);
		}


		/// <summary>
		/// Show an error message dialog
		/// </summary>
		/// <param name="form">owning form</param>
		/// <param name="message">optional message to display</param>
		/// <param name="ex">optional exception details</param>
		/// <param name="buttons">button choice other than OK</param>
		/// <returns>DialogResult</returns>
		public static DialogResult ErrorDialog(Form form, string message = null, Exception ex = null, MessageBoxButtons buttons = MessageBoxButtons.OK)
		{
			if (message == null)
			{
				message = strings.ErrorOccured + (ex != null ? ": " + ex.Message : string.Empty);
			}
			if (ex != null && string.IsNullOrEmpty(ex.Message) == false)
			{
				message += Environment.NewLine + Environment.NewLine + ex.Message;
			}
#if DEBUG
      StringBuilder capture = new StringBuilder();
      Exception e = ex;
      while (e != null)
      {
        capture.Append(new System.Diagnostics.StackTrace(e).ToString()).Append(Environment.NewLine);
        e = e.InnerException;
      }
			message += Environment.NewLine + Environment.NewLine + capture.ToString();

			if (ex != null)
			{
				WinAuthMain.LogException(ex);
			}
#endif

			return MessageBox.Show(form, message, WinAuthMain.APPLICATION_TITLE, buttons, MessageBoxIcon.Exclamation);
		}

		/// <summary>
		/// Show a confirmation Yes/No dialog
		/// </summary>
		/// <param name="form">owning form</param>
		/// <param name="message">message to display</param>
		/// <param name="buttons">button if other than YesNo</param>
		/// <returns>DialogResult</returns>
		public static DialogResult ConfirmDialog(Form form, string message, MessageBoxButtons buttons = MessageBoxButtons.YesNo, MessageBoxIcon icon = MessageBoxIcon.Question, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
		{
			return MessageBox.Show(form, message, WinAuthMain.APPLICATION_TITLE, buttons, icon, defaultButton);
		}

		/// <summary>
		/// Preload the context menu with the possible set of authenticator types
		/// </summary>
		private void LoadAddAuthenticatorTypes()
		{
			addAuthenticatorMenu.Items.Clear();

			ToolStripMenuItem subitem;
			int index = 0;
			foreach (RegisteredAuthenticator auth in WinAuthMain.REGISTERED_AUTHENTICATORS)
			{
				if (auth == null)
				{
					addAuthenticatorMenu.Items.Add(new ToolStripSeparator());
					continue;
				}

				subitem = new ToolStripMenuItem();
				subitem.Text = auth.Name;
				subitem.Name = "addAuthenticatorMenuItem_" + index++;
				subitem.Tag = auth;
				if (string.IsNullOrEmpty(auth.Icon) == false)
				{
					subitem.Image = new Bitmap(Assembly.GetExecutingAssembly().GetManifestResourceStream("WinAuth.Resources." + auth.Icon));
					subitem.ImageAlign = ContentAlignment.MiddleLeft;
					subitem.ImageScaling = ToolStripItemImageScaling.SizeToFit;
				}
				subitem.Click += addAuthenticatorMenu_Click;
				addAuthenticatorMenu.Items.Add(subitem);
			}
			//
			addAuthenticatorMenu.Items.Add(new ToolStripSeparator());
			//
			subitem = new ToolStripMenuItem();
			subitem.Text = strings.MenuImportText;
			subitem.Name = "importTextMenuItem";
			subitem.Image = new Bitmap(Assembly.GetExecutingAssembly().GetManifestResourceStream("WinAuth.Resources.TextIcon.png"));
			subitem.ImageAlign = ContentAlignment.MiddleLeft;
			subitem.ImageScaling = ToolStripItemImageScaling.SizeToFit;
			subitem.Click += importTextMenu_Click;
			addAuthenticatorMenu.Items.Add(subitem);
		}




		/// <summary>
		/// General Windows Message handler
		/// </summary>
		/// <param name="m"></param>
		protected override void WndProc(ref Message m)
		{
			base.WndProc(ref m);

			// pick up the HotKey message from RegisterHotKey and call hook callback
			if (m.Msg == WinAPI.WM_HOTKEY)
			{
				Keys key = (Keys)(((int)m.LParam >> 16) & 0xffff);
				WinAPI.KeyModifiers modifier = (WinAPI.KeyModifiers)((int)m.LParam & 0xffff);

				if (m_hook != null)
				{
					m_hook.KeyCallback(new KeyboardHookEventArgs(key, modifier));
				}
			}
			else if (m.Msg == WinAPI.WM_DEVICECHANGE)
			{
				int wParam = (int)m.WParam;
				if (wParam == WinAPI.DBT_DEVICEARRIVAL)
				{
					lock (m_deviceArrivalMutex)
					{
						if (this.Config == null || (this.Config.PasswordType & (Authenticator.PasswordTypes.YubiKeySlot1 | Authenticator.PasswordTypes.YubiKeySlot2)) != 0 && m_yubis.Count == 0)
						{
							m_yubis = HIDDevice.GetAllDevices(YubiKey.VENDOR_ID);
							if (m_yubis.Count != 0)
							{
								this.authenticatorList.Items.Clear();
								this.Size = m_initialSize;
								this.Config = null;
								loadNotifyMenu(this.notifyMenu);
								UnhookHotkeys();
								loadConfig(string.Empty);
							}
						}
					}
				}
				else if (wParam == WinAPI.DBT_DEVICEREMOVED)
				{
					if (this.Config != null && (this.Config.PasswordType & (Authenticator.PasswordTypes.YubiKeySlot1 | Authenticator.PasswordTypes.YubiKeySlot2)) != 0)
					{
						// check if YubiKey still present
						m_yubis = HIDDevice.GetAllDevices(YubiKey.VENDOR_ID);
						if (m_yubis.Count == 0)
						{
							this.authenticatorList.Items.Clear();
							this.Size = m_initialSize;
							this.Config = null;
							loadNotifyMenu(this.notifyMenu);
							UnhookHotkeys();
							loadConfig(string.Empty);
						}
					}
				}
			}
			else if (m.Msg == WinAPI.WM_USER + 1)
			{
				// show the main form
				BringToFront();
				Show();
				WindowState = FormWindowState.Normal;
				Activate();
			}
		}

		/// <summary>
		/// A hotkey keyboard event occured, e.g. "Ctrl-Alt-C"
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void Hotkey_KeyPressed(object sender, KeyboardHookEventArgs e)
		{
			// avoid multiple keypresses being sent
			if (e.Authenticator != null && Monitor.TryEnter(m_sendingKeys) == true)
			{
				try
				{
					// set Tag as HotKeyLauncher so we can pull back related authenticator and check for timeout
					hotkeyTimer.Tag = new HotKeyLauncher(this, e.Authenticator);
					hotkeyTimer.Enabled = true;

					// mark event as handled
					e.Handled = true;
				}
				finally
				{
					Monitor.Exit(m_sendingKeys);
				}
			}
		}




		/// <summary>
		/// Put data into the clipboard
		/// </summary>
		/// <param name="data"></param>
		public void SetClipboardData(object data)
		{
			bool clipRetry = false;
			do
			{
				try
				{
					Clipboard.Clear();
					Clipboard.SetDataObject(data, true, 4, 250);
				}
				catch (ExternalException)
				{
					// only show an error the first time
					clipRetry = (MessageBox.Show(this, strings.ClipboardInUse,
						WinAuthMain.APPLICATION_NAME,
						MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes);
				}
			}
			while (clipRetry == true);
		}

		/// <summary>
		/// Get data from the clipboard
		/// </summary>
		/// <param name="format"></param>
		/// <returns></returns>
		public object GetClipboardData(Type format)
		{
			bool clipRetry = false;
			do
			{
				try
				{
					IDataObject clipdata = Clipboard.GetDataObject();
					return (clipdata != null ? clipdata.GetData(format) : null);
				}
				catch (ExternalException)
				{
					// only show an error the first time
					clipRetry = (MessageBox.Show(this, strings.ClipboardInUse,
						WinAuthMain.APPLICATION_NAME,
						MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes);
				}
			}
			while (clipRetry == true);

			return null;
		}

		/// <summary>
		/// Set the size of the form based on config AutoSize property
		/// </summary>
		private void setAutoSize()
		{
			if (Config.AutoSize == true)
			{
				if (this.Config.Count != 0)
				{
					this.Width = Math.Max(420, authenticatorList.Margin.Horizontal + authenticatorList.GetMaxItemWidth() + (this.Width - authenticatorList.Width));
				}
				else
				{
					this.Width = 420;
				}

				// Issue#175; take the smallest of full height or 62% screen height
				int height = this.Height - authenticatorList.Height;
				height += (this.Config.Count * authenticatorList.ItemHeight);
				this.Height = Math.Min(Screen.GetWorkingArea(this).Height * 62 / 100, height);

				this.Resizable = false;
			}
			else
			{
				this.Resizable = true;
				if (Config.Width != 0)
				{
					this.Width = Config.Width;
				}
				if (Config.Height != 0)
				{
					this.Height = Config.Height;
				}
			}
		}

		/// <summary>
		/// Can the renaming of an authenticator
		/// </summary>
		private void EndRenaming()
		{
			// set focus to form, so that the edit field will disappear if it is visble
			if (authenticatorList.IsRenaming == true)
			{
				authenticatorList.EndRenaming();
			}
		}

		/// <summary>
		/// Set up the notify icon when the main form is shown
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void WinAuthForm_Shown(object sender, EventArgs e)
		{
			// if we use tray icon make sure it is set
			if (this.Config != null && this.Config.UseTrayIcon == true)
			{
				notifyIcon.Visible = true;
				notifyIcon.Text = this.Text;

				// if initially minizied, we need to hide
				if (WindowState == FormWindowState.Minimized)
				{
					// hide this and metro owner
					Form form = this;
					do
					{
						form.Hide();
					} while ((form = form.Owner) != null);
				}
			}

			// prompt to import v2
			if (string.IsNullOrEmpty(_existingv2Config) == false)
			{
				DialogResult importResult = MessageBox.Show(this,
					string.Format(strings.LoadPreviousAuthenticator, _existingv2Config),
					WinAuthMain.APPLICATION_TITLE,
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Question);
				if (importResult == System.Windows.Forms.DialogResult.Yes)
				{
					importAuthenticatorFromV2(_existingv2Config);
				}
				_existingv2Config = null;
			}
		}

		/// <summary>
		/// Minimize to icon when closing or unbind and close
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void WinAuthForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			// keep in the tray when closing Form 
			if (Config != null && Config.UseTrayIcon == true && this.Visible == true && m_explictClose == false)
			{
				e.Cancel = true;
				notifyIcon.Visible = true;
				// hide this and metro owner
				Form form = this;
				do
				{
					form.Hide();
				} while ((form = form.Owner) != null);
				return;
			}

			// remove the Steam hook
			UnhookSteam();

			// remove the hotkey hook
			UnhookHotkeys();

			// remove USB hook
			WinAPI.UnregisterUsbDeviceNotification(m_usbHandle);

			// ensure the notify icon is closed
			notifyIcon.Visible = false;

			// save size if we are not autoresize
			if (this.Config != null && this.Config.AutoSize == false && (this.Config.Width != this.Width || this.Config.Height != this.Height))
			{
				this.Config.Width = this.Width;
				this.Config.Height = this.Height;
			}
			if (this.Config != null /* && this.Config.Position.IsEmpty == false */)
			{
				this.Config.Position = new Point(this.Left, this.Top);
			}

			// perform save if we have one pending
			if (_saveConfigTime != null)
			{
				SaveConfig(true);
			}
		}

		/// <summary>
		/// Click on a choice of new authenticator
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void addAuthenticatorMenu_Click(object sender, EventArgs e)
		{
			ToolStripItem menuitem = (ToolStripItem)sender;
			RegisteredAuthenticator registeredauth = menuitem.Tag as RegisteredAuthenticator;
			if (registeredauth != null)
			{
				// add the new authenticator
				WinAuthAuthenticator winauthauthenticator = new WinAuthAuthenticator();
				bool added = false;

				if (registeredauth.AuthenticatorType == RegisteredAuthenticator.AuthenticatorTypes.BattleNet)
				{
					int existing = 0;
					string name;
					do
					{
						name = "Battle.net" + (existing != 0 ? " (" + existing + ")" : string.Empty);
						existing++;
					} while (authenticatorList.Items.Cast<AuthenticatorListitem>().Where(a => a.Authenticator.Name == name).Count() != 0);

					winauthauthenticator.Name = name;
					winauthauthenticator.AutoRefresh = false;

					// create the Battle.net authenticator
					AddBattleNetAuthenticator form = new AddBattleNetAuthenticator();
					form.Authenticator = winauthauthenticator;
					added = (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK);
				}
				else if (registeredauth.AuthenticatorType == RegisteredAuthenticator.AuthenticatorTypes.Trion)
				{
					// create the Trion authenticator
					int existing = 0;
					string name;
					do
					{
						name = "Trion" + (existing != 0 ? " (" + existing + ")" : string.Empty);
						existing++;
					} while (authenticatorList.Items.Cast<AuthenticatorListitem>().Where(a => a.Authenticator.Name == name).Count() != 0);

					winauthauthenticator.Name = name;
					winauthauthenticator.AutoRefresh = false;

					AddTrionAuthenticator form = new AddTrionAuthenticator();
					form.Authenticator = winauthauthenticator;
					added = (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK);
				}
				else if (registeredauth.AuthenticatorType == RegisteredAuthenticator.AuthenticatorTypes.Steam)
				{
					// create the authenticator
					int existing = 0;
					string name;
					do
					{
						name = "Steam" + (existing != 0 ? " (" + existing + ")" : string.Empty);
						existing++;
					} while (authenticatorList.Items.Cast<AuthenticatorListitem>().Where(a => a.Authenticator.Name == name).Count() != 0);

					winauthauthenticator.Name = name;
					winauthauthenticator.AutoRefresh = false;

					AddSteamAuthenticator form = new AddSteamAuthenticator();
					form.Authenticator = winauthauthenticator;
					added = (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK);
				}
				else if (registeredauth.AuthenticatorType == RegisteredAuthenticator.AuthenticatorTypes.Google)
				{
					// create the Google authenticator
					// add the new authenticator
					int existing = 0;
					string name;
					do
					{
						name = "Google" + (existing != 0 ? " (" + existing + ")" : string.Empty);
						existing++;
					} while (authenticatorList.Items.Cast<AuthenticatorListitem>().Where(a => a.Authenticator.Name == name).Count() != 0);
					winauthauthenticator.Name = name;
					winauthauthenticator.AutoRefresh = false;

					AddGoogleAuthenticator form = new AddGoogleAuthenticator();
					form.Authenticator = winauthauthenticator;
					added = (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK);
				}
				else if (registeredauth.AuthenticatorType == RegisteredAuthenticator.AuthenticatorTypes.GuildWars)
				{
					// create the GW2 authenticator
					int existing = 0;
					string name;
					do
					{
						name = "GuildWars" + (existing != 0 ? " (" + existing + ")" : string.Empty);
						existing++;
					} while (authenticatorList.Items.Cast<AuthenticatorListitem>().Where(a => a.Authenticator.Name == name).Count() != 0);
					winauthauthenticator.Name = name;
					winauthauthenticator.AutoRefresh = false;

					AddGuildWarsAuthenticator form = new AddGuildWarsAuthenticator();
					form.Authenticator = winauthauthenticator;
					added = (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK);
				}
				else if (registeredauth.AuthenticatorType == RegisteredAuthenticator.AuthenticatorTypes.Microsoft)
				{
					// create the Microsoft authenticator
					int existing = 0;
					string name;
					do
					{
						name = "Microsoft" + (existing != 0 ? " (" + existing + ")" : string.Empty);
						existing++;
					} while (authenticatorList.Items.Cast<AuthenticatorListitem>().Where(a => a.Authenticator.Name == name).Count() != 0);
					winauthauthenticator.Name = name;
					winauthauthenticator.AutoRefresh = false;

					AddMicrosoftAuthenticator form = new AddMicrosoftAuthenticator();
					form.Authenticator = winauthauthenticator;
					added = (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK);
				}
				else if (registeredauth.AuthenticatorType == RegisteredAuthenticator.AuthenticatorTypes.RFC6238_TIME)
				{
					// create the Google authenticator
					// add the new authenticator
					int existing = 0;
					string name;
					do
					{
						name = "Authenticator" + (existing != 0 ? " (" + existing + ")" : string.Empty);
						existing++;
					} while (authenticatorList.Items.Cast<AuthenticatorListitem>().Where(a => a.Authenticator.Name == name).Count() != 0);
					winauthauthenticator.Name = name;
					winauthauthenticator.AutoRefresh = false;
					winauthauthenticator.Skin = "WinAuthIcon.png";

					AddAuthenticator form = new AddAuthenticator();
					form.Authenticator = winauthauthenticator;
					added = (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK);
				}
				else if (registeredauth.AuthenticatorType == RegisteredAuthenticator.AuthenticatorTypes.OktaVerify)
				{
					// create the Okta Verify authenticator
					int existing = 0;
					string name;
					do
					{
						name = "Okta" + (existing != 0 ? " (" + existing + ")" : string.Empty);
						existing++;
					} while (authenticatorList.Items.Cast<AuthenticatorListitem>().Where(a => a.Authenticator.Name == name).Count() != 0);
					winauthauthenticator.Name = name;
					winauthauthenticator.AutoRefresh = false;

					AddOktaVerifyAuthenticator form = new AddOktaVerifyAuthenticator();
					form.Authenticator = winauthauthenticator;
					added = (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK);
				}
				else
				{
					throw new NotImplementedException(strings.AuthenticatorNotImplemented + ": " + registeredauth.AuthenticatorType.ToString());
				}

				if (added == true)
				{
					// save off any new authenticators as a backup
					WinAuthHelper.SaveToRegistry(this.Config, winauthauthenticator);

					// first time we prompt for protection
					if (this.Config.Count == 0)
					{
						ChangePasswordForm form = new ChangePasswordForm();
						form.PasswordType = Authenticator.PasswordTypes.Explicit;
						if (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
						{
							this.Config.Yubi = form.Yubikey;
							this.Config.PasswordType = form.PasswordType;
							if ((this.Config.PasswordType & Authenticator.PasswordTypes.Explicit) != 0 && string.IsNullOrEmpty(form.Password) == false)
							{
								this.Config.Password = form.Password;
							}
						}
					}

					this.Config.Add(winauthauthenticator);
					SaveConfig(true);
					loadAuthenticatorList(winauthauthenticator);

					// reset UI
					setAutoSize();
					introLabel.Visible = (this.Config.Count == 0);

					// reset hotkeeys
					HookHotkeys();
				}
			}
		}

		/// <summary>
		/// Click to import an text file of authenticators
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void importTextMenu_Click(object sender, EventArgs e)
		{
			ToolStripItem menuitem = (ToolStripItem)sender;

			OpenFileDialog ofd = new OpenFileDialog();
			ofd.AddExtension = true;
			ofd.CheckFileExists = true;
			ofd.CheckPathExists = true;
			//
			string lastv2file = WinAuthHelper.GetLastV2Config();
			if (string.IsNullOrEmpty(lastv2file) == false)
			{
				ofd.InitialDirectory = Path.GetDirectoryName(lastv2file);
				ofd.FileName = Path.GetFileName(lastv2file);
			}
			//
			ofd.Filter = "WinAuth Files (*.xml)|*.xml|Text Files (*.txt)|*.txt|Zip Files (*.zip)|*.zip|PGP Files (*.pgp)|*.pgp|All Files (*.*)|*.*";
			ofd.RestoreDirectory = true;
			ofd.Title = WinAuthMain.APPLICATION_TITLE;
			if (ofd.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
			{
				importAuthenticator(ofd.FileName);
			}
		}

		/// <summary>
		/// Timer tick event
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void mainTimer_Tick(object sender, EventArgs e)
		{
			authenticatorList.Tick(sender, e);

			// if a save is due
			if (_saveConfigTime != null && _saveConfigTime.Value <= DateTime.Now)
			{
				SaveConfig();
			}
		}

		/// <summary>
		/// Click the Add button to add an authenticator
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void addAuthenticatorButton_Click(object sender, EventArgs e)
		{
			addAuthenticatorMenu.Show(addAuthenticatorButton, addAuthenticatorButton.Width, 0);
		}

		/// <summary>
		/// Click the Options button to show menu
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void optionsButton_Click(object sender, EventArgs e)
		{
			optionsMenu.Show(optionsButton, optionsButton.Width - optionsMenu.Width, optionsButton.Height - 1);
		}

		/// <summary>
		/// Double click notify to re-open
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void notifyIcon_DoubleClick(object sender, EventArgs e)
		{
			BringToFront();
			Show();
			WindowState = FormWindowState.Normal;
			Activate();
		}

		/// <summary>
		/// Event fired when an authenticator is removed (i.e. deleted) from the list
		/// </summary>
		/// <param name="source"></param>
		/// <param name="args"></param>
		private void authenticatorList_ItemRemoved(object source, AuthenticatorListItemRemovedEventArgs args)
		{
			foreach (var auth in Config)
			{
				if (auth == args.Item.Authenticator)
				{
					Config.Remove(auth);
					break;
				}
			}

			// update UI
			setAutoSize();

			// if no authenticators, show intro text and remove any encryption
			if (this.Config.Count == 0)
			{
				introLabel.Visible = true;
				authenticatorList.Visible = false;
				this.Config.PasswordType = Authenticator.PasswordTypes.None;
				this.Config.Password = null;
			}

			// save the current config
			SaveConfig();
		}

		/// <summary>
		/// Event fired when an authenticator is dragged and dropped in the listbox
		/// </summary>
		/// <param name="source"></param>
		/// <param name="args"></param>
		private void authenticatorList_Reordered(object source, AuthenticatorListReorderedEventArgs args)
		{
			// set the new order of items in Config from that of the list
			int count = this.authenticatorList.Items.Count;
			for (int i=0; i<count; i++)
			{
				AuthenticatorListitem item = (AuthenticatorListitem)this.authenticatorList.Items[i];
				this.Config.Where(a => a == item.Authenticator).FirstOrDefault().Index = i;
			}
			// resort the config list
			this.Config.Sort();
			// update the notify menu
			loadNotifyMenu(this.notifyMenu);

			// update UI
			setAutoSize();

			// save the current config
			SaveConfig();
		}

		/// <summary>
		/// Double click an item in the list
		/// </summary>
		/// <param name="source"></param>
		/// <param name="args"></param>
		private void authenticatorList_DoubleClick(object source, AuthenticatorListDoubleClickEventArgs args)
		{
			RunAction(args.Authenticator, WinAuthConfig.NotifyActions.CopyToClipboard);
		}

		/// <summary>
		/// Click in the main form
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void WinAuthForm_MouseDown(object sender, MouseEventArgs e)
		{
			EndRenaming();
		}

		/// <summary>
		/// Click in the command panel
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void commandPanel_MouseDown(object sender, MouseEventArgs e)
		{
			EndRenaming();
		}

		/// <summary>
		/// Resizing the form, we have to manually adjust the width and height of list else starting as minimized borks
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void WinAuthForm_Resize(object sender, EventArgs e)
		{
			this.SuspendLayout();
			if (_listoffset.Bottom != 0)
			{
				authenticatorList.Height = this.Height - _listoffset.Height;
				authenticatorList.Width = this.Width - _listoffset.Width;
			}
			this.ResumeLayout(true);
		}

		/// <summary>
		/// Set the config once resizing has completed
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void WinAuthForm_ResizeEnd(object sender, EventArgs e)
		{
			if (this.Config != null && this.Config.AutoSize == false)
			{
				this.Config.Width = this.Width;
				this.Config.Height = this.Height;
			}
		}

		/// <summary>
		/// Click the button to enter a password
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void passwordButton_Click(object sender, EventArgs e)
		{
			if (this.passwordField.Text.Trim().Length == 0)
			{
				this.passwordErrorLabel.Text = strings.EnterPassword;
				this.passwordErrorLabel.Tag = DateTime.Now.AddSeconds(3);
				this.passwordTimer.Enabled = true;
				return;
			}

			loadConfig(this.passwordField.Text);
			this.passwordField.Text = string.Empty;
		}

		/// <summary>
		/// Remove the password error message after a few seconds
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void passwordTimer_Tick(object sender, EventArgs e)
		{
			if (this.passwordErrorLabel.Tag != null && (DateTime)this.passwordErrorLabel.Tag <= DateTime.Now)
			{
				this.passwordTimer.Enabled = false;
				this.passwordErrorLabel.Tag = null;
				this.passwordErrorLabel.Text = string.Empty;
			}
		}

		/// <summary>
		/// Catch pressing Enter in the password field
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void passwordField_KeyPress(object sender, KeyPressEventArgs e)
		{
			if (e.KeyChar == (char)Keys.Return)
			{
				e.Handled = true;
				passwordButton_Click(sender, null);
			}
		}

		/// <summary>
		/// Click the button to check the YubiKey again
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void yubiRetryButton_Click(object sender, EventArgs e)
		{
			loadConfig(this.passwordField.Text);
		}

		/// <summary>
		/// Callback from the Updater if a newer version is available
		/// </summary>
		/// <param name="latest"></param>
		private void NewVersionAvailable(Version latest)
		{
			if (Updater != null && Updater.IsAutoCheck == true && latest != null && latest > Updater.CurrentVersion)
			{
				this.Invoke((MethodInvoker)delegate { newVersionLink.Text = "New version " + latest.ToString(3) + " available"; newVersionLink.Visible = true; });
			}
			else
			{
				this.Invoke((MethodInvoker)delegate { newVersionLink.Visible = false; });
			}
		}

		/// <summary>
		/// Show the Update form and update status if necessary
		/// </summary>
		private void ShowUpdaterForm()
		{
			UpdateCheckForm form = new UpdateCheckForm();
			form.Config = this.Config;
			form.Updater = this.Updater;
			if (form.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
			{
				NewVersionAvailable(Updater.LastKnownLatestVersion);
				SaveConfig();
			}
		}

		/// <summary>
		/// If click the new version status link
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void newVersionLink_Click(object sender, EventArgs e)
		{
			if (System.Deployment.Application.ApplicationDeployment.IsNetworkDeployed == false)
			{
				ShowUpdaterForm();
			}
		}

		/// <summary>
		/// System time change event. We need to resync any unprotected authenticators
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void SystemEvents_TimeChanged(object sender, EventArgs e)
		{
			Cursor cursor = Cursor.Current;
			Cursor.Current = Cursors.WaitCursor;
			foreach (var auth in this.Config)
			{
				if (auth.AuthenticatorData != null && auth.AuthenticatorData.RequiresPassword == false)
				{
					try
					{
						auth.Sync();
					}
					catch (Exception) { }
				}
			}
			Cursor.Current = cursor;
		}

#endregion



		/// <summary>
		/// Inner class used to form details of hot key
		/// </summary>
		class HotKeyLauncher
		{
			/// <summary>
			/// Owning form
			/// </summary>
			public WinAuthForm Form { get; set; }

			/// <summary>
			/// Hotkey authenticator
			/// </summary>
			public WinAuthAuthenticator Authenticator { get; set; }

			/// <summary>
			/// When hotkey was pressed
			/// </summary>
			public DateTime Started { get; set; }

			/// <summary>
			/// Create a new HotKeyLauncher object
			/// </summary>
			/// <param name="form">owning Form</param>
			/// <param name="auth">Authenticator</param>
			public HotKeyLauncher(WinAuthForm form, WinAuthAuthenticator auth)
			{
				Started = DateTime.Now;
				Form = form;
				Authenticator = auth;
			}
		}



  }
}
