using System;
using System.Collections.Generic;
using System.Text;

namespace Enyim.Caching.Memcached
{
	public sealed class Base64KeyTransformer : IKeyTransformer
	{
		string IKeyTransformer.Transform(string key)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(key), Base64FormattingOptions.None);
		}
	}
}

#region [ License information          ]
/* ************************************************************
 *
 * Copyright (c) Attila Kisk�, enyim.com, 2007
 *
 * This source code is subject to terms and conditions of 
 * Microsoft Permissive License (Ms-PL).
 * 
 * A copy of the license can be found in the License.html
 * file at the root of this distribution. If you can not 
 * locate the License, please send an email to a@enyim.com
 * 
 * By using this source code in any fashion, you are 
 * agreeing to be bound by the terms of the Microsoft 
 * Permissive License.
 *
 * You must not remove this notice, or any other, from this
 * software.
 *
 * ************************************************************/
#endregion