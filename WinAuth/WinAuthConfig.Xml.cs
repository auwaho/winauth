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
    #region ICloneable

    /// <summary>
    /// Clone return a new WinAuthConfig object
    /// </summary>
    /// <returns></returns>
    public object Clone()
    {
      WinAuthConfig clone = (WinAuthConfig)this.MemberwiseClone();
      // close the internal authenticator so the data is kept separate
      clone.OnConfigChanged = null;
      clone._authenticators = new List<WinAuthAuthenticator>();
      foreach (var wa in _authenticators)
      {
        clone._authenticators.Add(wa.Clone() as WinAuthAuthenticator);
      }
      clone.CurrentAuthenticator = (this.CurrentAuthenticator != null ? clone._authenticators[this._authenticators.IndexOf(this.CurrentAuthenticator)] : null);
      return clone;
    }

    public bool ReadXml(XmlReader reader, string password = null)
    {
			bool changed = false;

      reader.Read();
      while (reader.EOF == false && reader.IsEmptyElement == true)
      {
        reader.Read();
      }
      reader.MoveToContent();
      while (reader.EOF == false)
      {
        if (reader.IsStartElement())
        {
          switch (reader.Name)
          {
						// v2 file
            case "WinAuth":
              changed = ReadXmlInternal(reader, password);
              break;

            default:
              reader.Skip();
              break;
          }
        }
        else
        {
          reader.Read();
          break;
        }
      }

			return changed;
    }

    protected bool ReadXmlInternal(XmlReader reader, string password = null)
    {
			bool changed = false;

      decimal version;
			if (decimal.TryParse(reader.GetAttribute("version"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out version) == true)
      {
        Version = version;

				if (version > WinAuthConfig.CURRENTVERSION)
				{
					// ensure we don't overwrite a newer config
					throw new WinAuthInvalidNewerConfigException(string.Format(strings.ConfigIsNewer, version));
				}
			}

      string encrypted = reader.GetAttribute("encrypted");
			this.PasswordType = Authenticator.DecodePasswordTypes(encrypted);
      if (this.PasswordType != Authenticator.PasswordTypes.None)
      {
        // read the encrypted text from the node
        string data = reader.ReadElementContentAsString();
        // decrypt
				YubiKey yubi = null;
				if ((this.PasswordType & (Authenticator.PasswordTypes.YubiKeySlot1 | Authenticator.PasswordTypes.YubiKeySlot2)) != 0 /* && this.Yubi == null */)
				{
					yubi = YubiKey.CreateInstance();
				}
				data = Authenticator.DecryptSequence(data, this.PasswordType, password, yubi);

				using (MemoryStream ms = new MemoryStream(Authenticator.StringToByteArray(data)))
				{
          reader = XmlReader.Create(ms);
          changed = ReadXml(reader, password);
        }

				this.PasswordType = Authenticator.DecodePasswordTypes(encrypted);
				this.Password = password;
				this.Yubi = yubi;

        return changed;
      }

      reader.MoveToContent();
      if (reader.IsEmptyElement)
      {
        reader.Read();
        return changed;
      }

      bool defaultAutoRefresh = true;
      bool defaultAllowCopy = false;
      bool defaultCopyOnCode = false;
      bool defaultHideSerial = true;
      string defaultSkin = null;

      reader.Read();
      while (reader.EOF == false)
      {
        if (reader.IsStartElement())
        {
          switch (reader.Name)
          {
						case "config":
							changed = ReadXmlInternal(reader, password) || changed;
							break;

						// 3.2 has new layout 
						case "data":
							{
								encrypted = reader.GetAttribute("encrypted");
								this.PasswordType = Authenticator.DecodePasswordTypes(encrypted);
								if (this.PasswordType != Authenticator.PasswordTypes.None)
								{
                  HashAlgorithm hasher;
									string hash = reader.GetAttribute("sha1");
                  if (string.IsNullOrEmpty(hash) == false)
                  {
                    hasher = Authenticator.SafeHasher("SHA1");
                  }
                  else
                  {
                    // old version has md5
                    hash = reader.GetAttribute("md5");
                    hasher = Authenticator.SafeHasher("MD5");
                  }
                  // read the encrypted text from the node
                  string data = reader.ReadElementContentAsString();

									hasher.ComputeHash(Authenticator.StringToByteArray(data));
                  hasher.Dispose();

                  // decrypt
                  YubiKey yubi = null;
									if ((this.PasswordType & (Authenticator.PasswordTypes.YubiKeySlot1 | Authenticator.PasswordTypes.YubiKeySlot2)) != 0 /* && this.Yubi == null */)
									{
										yubi = YubiKey.CreateInstance();
									}
									data = Authenticator.DecryptSequence(data, this.PasswordType, password, yubi);
									byte[] plain = Authenticator.StringToByteArray(data);

									using (MemoryStream ms = new MemoryStream(plain))
									{
										var datareader = XmlReader.Create(ms);
										changed = ReadXmlInternal(datareader, password) || changed;
									}

									this.PasswordType = Authenticator.DecodePasswordTypes(encrypted);
									this.Password = password;
									this.Yubi = yubi;
								}
							}
							break;
						
						case "alwaysontop":
              _alwaysOnTop = reader.ReadElementContentAsBoolean();
              break;

            case "usetrayicon":
              _useTrayIcon = reader.ReadElementContentAsBoolean();
              break;

						case "notifyaction":
							string s = reader.ReadElementContentAsString();
							if (string.IsNullOrEmpty(s) == false)
							{
								try {
									_notifyAction = (NotifyActions)Enum.Parse(typeof(NotifyActions), s, true);
								}
								catch (Exception) { }
							}
							break;

						case "startwithwindows":
              _startWithWindows = reader.ReadElementContentAsBoolean();
              break;

						case "autosize":
							_autoSize = reader.ReadElementContentAsBoolean();
							break;

						case "left":
							_position.X = reader.ReadElementContentAsInt();
							break;

						case "top":
							_position.Y = reader.ReadElementContentAsInt();
							break;

						case "width":
							_width = reader.ReadElementContentAsInt();
							break;

						case "height":
							_height = reader.ReadElementContentAsInt();
							break;

						case "shadowtype":
							_shadowType = reader.ReadElementContentAsString();
							break;

						case "pgpkey":
							_pgpKey = reader.ReadElementContentAsString();
							break;

						case "settings":
							XmlSerializer serializer = new XmlSerializer(typeof(setting[]), new XmlRootAttribute() { ElementName = "settings" });
							_settings = ((setting[])serializer.Deserialize(reader)).ToDictionary(e => e.Key, e => e.Value);
							break;

            // previous setting used as defaults for new
            case "autorefresh":
              defaultAutoRefresh = reader.ReadElementContentAsBoolean();
              break;
            case "allowcopy":
              defaultAllowCopy = reader.ReadElementContentAsBoolean();
              break;
            case "copyoncode":
              defaultCopyOnCode = reader.ReadElementContentAsBoolean();
              break;
            case "hideserial":
              defaultHideSerial = reader.ReadElementContentAsBoolean();
              break;
            case "skin":
              defaultSkin = reader.ReadElementContentAsString();
              break;

            case "WinAuthAuthenticator":
							var wa = new WinAuthAuthenticator();
              changed = wa.ReadXml(reader, password) || changed;
              this.Add(wa);
							if (this.CurrentAuthenticator == null)
							{
								this.CurrentAuthenticator = wa;
							}
              break;

            // for old 2.x configs
            case "authenticator":
							var waold = new WinAuthAuthenticator();
              waold.AuthenticatorData = Authenticator.ReadXmlv2(reader, password);
              if (waold.AuthenticatorData is BattleNetAuthenticator)
              {
                waold.Name = "Battle.net";
              }
              else if (waold.AuthenticatorData is GuildWarsAuthenticator)
              {
                waold.Name = "GuildWars 2";
              }
              else if (waold.AuthenticatorData is GuildWarsAuthenticator)
              {
                waold.Name = "Authenticator";
              }
              this.Add(waold);
              this.CurrentAuthenticator = waold;
              waold.AutoRefresh = defaultAutoRefresh;
              waold.AllowCopy = defaultAllowCopy;
              waold.CopyOnCode = defaultCopyOnCode;
              waold.HideSerial = defaultHideSerial;
              break;

						// old 2.x auto login script
						case "autologin":
              var hks = new HoyKeySequence();
              hks.ReadXml(reader, password);
							if (hks.HotKey != 0)
							{
								if (this.CurrentAuthenticator.HotKey == null)
								{
									this.CurrentAuthenticator.HotKey = new HotKey();
								}
								HotKey hotkey = this.CurrentAuthenticator.HotKey;
								hotkey.Action = HotKey.HotKeyActions.Inject;
								hotkey.Key = hks.HotKey;
								hotkey.Modifiers = hks.Modifiers;
								if (hks.WindowTitleRegex == true && string.IsNullOrEmpty(hks.WindowTitle) == false)
								{
									hotkey.Window = "/" + Regex.Escape(hks.WindowTitle);
								}
								else if (string.IsNullOrEmpty(hks.WindowTitle) == false)
								{
									hotkey.Window = hks.WindowTitle;
								}
								else if (string.IsNullOrEmpty(hks.ProcessName) == false)
								{
									hotkey.Window = hks.ProcessName;
								}
								if (hks.Advanced == true)
								{
									hotkey.Action = HotKey.HotKeyActions.Advanced;
									hotkey.Advanced = hks.AdvancedScript;
								}
							}
              break;

            default:
              reader.Skip();
              break;
          }
        }
        else
        {
          reader.Read();
          break;
        }
      }

			return changed;
    }

    /// <summary>
    /// Write the data as xml into an XmlWriter
    /// </summary>
    /// <param name="writer">XmlWriter to write config</param>
    public void WriteXmlString(XmlWriter writer, bool includeFilename = false, bool includeSettings = true)
    {
      writer.WriteStartDocument(true);
      //
      if (includeFilename == true && string.IsNullOrEmpty(this.Filename) == false)
      {
        writer.WriteComment(this.Filename);
      }
      //
      writer.WriteStartElement("WinAuth");
      writer.WriteAttributeString("version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(2));
      //
      writer.WriteStartElement("alwaysontop");
      writer.WriteValue(this.AlwaysOnTop);
      writer.WriteEndElement();
      //
      writer.WriteStartElement("usetrayicon");
      writer.WriteValue(this.UseTrayIcon);
      writer.WriteEndElement();
			//
			writer.WriteStartElement("notifyaction");
			writer.WriteValue(Enum.GetName(typeof(NotifyActions), this.NotifyAction));
			writer.WriteEndElement();
			//
			writer.WriteStartElement("startwithwindows");
      writer.WriteValue(this.StartWithWindows);
      writer.WriteEndElement();
			//
			writer.WriteStartElement("autosize");
			writer.WriteValue(this.AutoSize);
			writer.WriteEndElement();
			//
			if (this.Position.IsEmpty == false)
			{
				writer.WriteStartElement("left");
				writer.WriteValue(this.Position.X);
				writer.WriteEndElement();
				writer.WriteStartElement("top");
				writer.WriteValue(this.Position.Y);
				writer.WriteEndElement();
			}
			//
			writer.WriteStartElement("width");
			writer.WriteValue(this.Width);
			writer.WriteEndElement();
			//
			writer.WriteStartElement("height");
			writer.WriteValue(this.Height);
			writer.WriteEndElement();
			//
			if (string.IsNullOrEmpty(this.ShadowType) == false)
			{
				writer.WriteStartElement("shadowtype");
				writer.WriteValue(this.ShadowType);
				writer.WriteEndElement();
			}
			//
			if (string.IsNullOrEmpty(this.PGPKey) == false)
			{
				writer.WriteStartElement("pgpkey");
				writer.WriteCData(this.PGPKey);
				writer.WriteEndElement();
			}

			if (PasswordType != Authenticator.PasswordTypes.None)
			{
				writer.WriteStartElement("data");

				StringBuilder encryptedTypes = new StringBuilder();
				if ((PasswordType & Authenticator.PasswordTypes.Explicit) != 0)
				{
					encryptedTypes.Append("y");
				}
				if ((PasswordType & Authenticator.PasswordTypes.User) != 0)
				{
					encryptedTypes.Append("u");
				}
				if ((PasswordType & Authenticator.PasswordTypes.Machine) != 0)
				{
					encryptedTypes.Append("m");
				}
				if ((PasswordType & Authenticator.PasswordTypes.YubiKeySlot1) != 0)
				{
					encryptedTypes.Append("a");
				}
				if ((PasswordType & Authenticator.PasswordTypes.YubiKeySlot2) != 0)
				{
					encryptedTypes.Append("b");
				}
				writer.WriteAttributeString("encrypted", encryptedTypes.ToString());

				byte[] data;
				using (MemoryStream ms = new MemoryStream())
				{
					XmlWriterSettings settings = new XmlWriterSettings();
					settings.Indent = true;
					settings.Encoding = Encoding.UTF8;
					using (XmlWriter encryptedwriter = XmlWriter.Create(ms, settings))
					{
						encryptedwriter.WriteStartElement("config");
						foreach (WinAuthAuthenticator wa in this)
						{
							wa.WriteXmlString(encryptedwriter);
						}
						encryptedwriter.WriteEndElement();
					}

					data = ms.ToArray();
				}

				using (var hasher = Authenticator.SafeHasher("SHA1"))
				{
					string encdata = Authenticator.EncryptSequence(Authenticator.ByteArrayToString(data), PasswordType, Password, this.Yubi);
					string enchash = Authenticator.ByteArrayToString(hasher.ComputeHash(Authenticator.StringToByteArray(encdata)));
					writer.WriteAttributeString("sha1", enchash);
					writer.WriteString(encdata);
				}
		
				writer.WriteEndElement();
			}
			else
			{
				foreach (WinAuthAuthenticator wa in this)
				{
					wa.WriteXmlString(writer);
				}
			}

			if (includeSettings == true && _settings.Count != 0)
			{
				XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
				ns.Add(string.Empty, string.Empty);
				XmlSerializer serializer = new XmlSerializer(typeof(setting[]), new XmlRootAttribute() { ElementName = "settings" });
				serializer.Serialize(writer, _settings.Select(e => new setting { Key = e.Key, Value = e.Value }).ToArray(), ns);
			}

      // close WinAuth
      writer.WriteEndElement();

      // end document
      writer.WriteEndDocument();
    }

    #endregion
}
}
