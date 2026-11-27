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
		#region IList

		/// <summary>
		/// Rewrite the index values in the WinAuthAuthenticator objects to match our order
		/// </summary>
		private void SetIndexes()
		{
			int count = this.Count;
			for (int i = 0; i < count; i++)
			{
				this[i].Index = i;
			}
		}

		/// <summary>
		/// Add a new authenticator
		/// </summary>
		/// <param name="authenticator">WinAuthAuthenticator instance</param>
		public void Add(WinAuthAuthenticator authenticator)
		{
			authenticator.OnWinAuthAuthenticatorChanged += new WinAuthAuthenticatorChangedHandler(this.OnWinAuthAuthenticatorChanged);
			_authenticators.Add(authenticator);
			SetIndexes();
		}

		/// <summary>
		/// Remove all the authenticators
		/// </summary>
		public void Clear()
		{
			foreach (WinAuthAuthenticator authenticator in this)
			{
				authenticator.Index = 0;
				authenticator.OnWinAuthAuthenticatorChanged -= new WinAuthAuthenticatorChangedHandler(this.OnWinAuthAuthenticatorChanged);
			}
			_authenticators.Clear();
		}

		/// <summary>
		/// Check if the config contains an authenticator
		/// </summary>
		/// <param name="authenticator"></param>
		/// <returns></returns>
		public bool Contains(WinAuthAuthenticator authenticator)
		{
			return _authenticators.Contains(authenticator);
		}

		/// <summary>
		/// Copy elements from the list to an array
		/// </summary>
		/// <param name="index"></param>
		/// <param name="array"></param>
		/// <param name="arrayIndex"></param>
		/// <param name="count"></param>
		public void CopyTo(int index, WinAuthAuthenticator[] array, int arrayIndex, int count)
		{
			_authenticators.CopyTo(index, array, arrayIndex, count);
		}

		/// <summary>
		/// Copy the list into an array
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		public void CopyTo(WinAuthAuthenticator[] array, int index)
		{
			_authenticators.CopyTo(array, index);
		}

		/// <summary>
		/// return the count of authenticators
		/// </summary>
		public int Count
		{
			get
			{
				return _authenticators.Count;
			}
		}

		/// <summary>
		/// Get the index of an authenticator
		/// </summary>
		/// <param name="authenticator"></param>
		/// <returns></returns>
		public int IndexOf(WinAuthAuthenticator authenticator)
		{
			return _authenticators.IndexOf(authenticator);
		}

		/// <summary>
		/// Insert an authenticator at a specified position
		/// </summary>
		/// <param name="index"></param>
		/// <param name="authenticator"></param>
		public void Insert(int index, WinAuthAuthenticator authenticator)
		{
			authenticator.OnWinAuthAuthenticatorChanged += new WinAuthAuthenticatorChangedHandler(this.OnWinAuthAuthenticatorChanged);
			_authenticators.Insert(index, authenticator);
			SetIndexes();
		}

		/// <summary>
		/// Return if this list is read only
		/// </summary>
		public bool IsReadOnly
		{
			get
			{
				return _readOnly;
			}
			set
			{
				_readOnly = value;
			}
		}

		/// <summary>
		/// Remove an authenticator
		/// </summary>
		/// <param name="authenticator"></param>
		/// <returns></returns>
		public bool Remove(WinAuthAuthenticator authenticator)
		{
			authenticator.OnWinAuthAuthenticatorChanged -= new WinAuthAuthenticatorChangedHandler(this.OnWinAuthAuthenticatorChanged);
			bool result = _authenticators.Remove(authenticator);
			SetIndexes();
			return result;
		}

		/// <summary>
		/// Remove an authenticator from a specific position
		/// </summary>
		/// <param name="index"></param>
		public void RemoveAt(int index)
		{
			_authenticators[index].OnWinAuthAuthenticatorChanged -= new WinAuthAuthenticatorChangedHandler(this.OnWinAuthAuthenticatorChanged);
			_authenticators.RemoveAt(index);
			SetIndexes();
		}

		/// <summary>
		/// Get an enumerator for the authenticators
		/// </summary>
		/// <returns></returns>
		public IEnumerator<WinAuthAuthenticator> GetEnumerator()
		{
			return _authenticators.GetEnumerator();
		}

		/// <summary>
		/// Get an enumerator for the authenticators
		/// </summary>
		/// <returns></returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		/// <summary>
		/// Indexer to get an authenticator by postion
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public WinAuthAuthenticator this[int index]
		{
			get
			{
				return _authenticators[index];
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		/// <summary>
		/// Sort the authenticator by their index value
		/// </summary>
		public void Sort()
		{
			_authenticators.Sort((a,b) => a.Index.CompareTo(b.Index));
		}

		#endregion
}
}
