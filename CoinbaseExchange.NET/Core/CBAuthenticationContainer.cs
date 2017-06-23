using CoinbaseExchange.NET.Utilities;
using System;
using System.Security.Cryptography;
using System.Text;

namespace CoinbaseExchange.NET.Core
{
	public struct SignatureBlock
	{
		public string ApiKey { get; internal set; }
		public string Passphrase { get; internal set; }
		public string TimeStamp { get; internal set; }
		public string Signature { get; internal set; }
	}

	public class CBAuthenticationContainer
	{
		private readonly string _secret;
		public string ApiKey { get; private set; }
		public string Passphrase { get; private set; }

		public CBAuthenticationContainer(string apiKey, string passphrase, string secret)
		{
			if (String.IsNullOrWhiteSpace(apiKey))
				throw new ArgumentNullException("apiKey", "An API key is required to use the coinbase API");

			if (String.IsNullOrWhiteSpace(passphrase))
				throw new ArgumentNullException("passphrase", "A passphrase is required to use the coinbase API");

			if (String.IsNullOrWhiteSpace(secret))
				throw new ArgumentNullException("secret", "A secret is required to use the coinbase API");

			ApiKey = apiKey;
			Passphrase = passphrase;
			_secret = secret;
		}

		public SignatureBlock ComputeSignature(string relativeUrl, string method, string body)
		{
			var timeStamp = TimeStamp;
			byte[] data = Convert.FromBase64String(this._secret);
			var prehash = timeStamp + method + relativeUrl + body;
			return new SignatureBlock
			{
						ApiKey = ApiKey, Passphrase = Passphrase,
						TimeStamp = timeStamp,
						Signature = HashString(prehash, data)
					};
		}

		private string HashString(string str, byte[] secret)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(str);
			using (var hmac = new HMACSHA256(secret))
			{
				byte[] hash = hmac.ComputeHash(bytes);
				return Convert.ToBase64String(hash);
			}
		}

		public string TimeStamp
		{
			get
			{
				var time = DateTime.UtcNow.ToUnixTimestamp();
				return time.ToString(System.Globalization.CultureInfo.InvariantCulture);
			}
		}
	}
}
