﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Token.cs" company="">
//   
// </copyright>
// <summary>
//   A token.
// </summary>
// --------------------------------------------------------------------------------------------------------------------



using System;
using System.Runtime.Serialization;

namespace Caas.OpenStack.API.Models.identity
{
	/// <summary>	A token. </summary>
	/// <remarks>	Anthony, 4/13/2015. </remarks>
	[DataContract]
	public class Token
	{
		/// <summary>
		/// Initialises a new instance of the <see cref="Token"/> class. 
		/// Initializes a new instance of the CAAS.OpenStack.API.Models.identity.Token class. 
		/// </summary>
		/// <remarks>
		/// 	Anthony, 4/13/2015. 
		/// </remarks>
		/// <param name="token">
		/// 	 	The token. 
		/// </param>
		public Token(string token)
		{
			_issuedAt = DateTime.Now;
			_expires = _issuedAt.AddMinutes(60);
			Id = token;
			Tenant = Tenant.DefaultTenant;
		}

		/// <summary>	Gets the issued at date. </summary>
		/// <value>	The issued at date. </value>
		[DataMember(Name = "issued_at")]
		public string IssuedAtDate
		{
			get
			{
				return _issuedAt.ToString("s");
			}
		}

		/// <summary>	The issued at Date/Time. </summary>
		private DateTime _issuedAt;

		/// <summary>	Gets the expires date. </summary>
		/// <value>	The expires date. </value>
		[DataMember(Name = "expires")]
		public string ExpiresDate
		{
			get { return _expires.ToString("s"); }
		}

		/// <summary>	The expires Date/Time. </summary>
		private DateTime _expires;

		/// <summary>	Gets or sets the identifier. </summary>
		/// <value>	The identifier. </value>
		[DataMember(Name = "id")]
		public string Id { get; set; }

		/// <summary>	Gets or sets the tenant. </summary>
		/// <value>	The tenant. </value>
		[DataMember(Name = "tenant")]
		public Tenant Tenant { get; set; }
	}
}