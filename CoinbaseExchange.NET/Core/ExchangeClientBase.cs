﻿using CoinbaseExchange.NET.Endpoints.PersonalOrders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace CoinbaseExchange.NET.Core
{
    public abstract class ExchangeClientBase
    {
		public readonly Uri API_SANDBOX_ENDPOINT_URL = new Uri("https://api-public.sandbox.gdax.com");
		public readonly Uri API_ENDPOINT_URL = new Uri("https://api.gdax.com");
        private const string ContentType = "application/json";
		public static bool IsSandbox { get; set; }

		protected readonly CBAuthenticationContainer _authContainer;

        public ExchangeClientBase(CBAuthenticationContainer authContainer)
        {
            _authContainer = authContainer;
        }

        protected async Task<ExchangeResponse> GetResponse(ExchangeRequestBase request)
        {
			var relativeUrlForURL = request.RequestUrl;
			var baseURI = IsSandbox ? API_SANDBOX_ENDPOINT_URL : API_ENDPOINT_URL;
			var absoluteUri = new Uri(baseURI, relativeUrlForURL);
			var uriBuilder = new UriBuilder(absoluteUri);
			uriBuilder.Port = -1;
			if (request is ExchangePageableRequestBase)
			{
				var requestCasted = request as ExchangePageableRequestBase;
				var nvc = HttpUtility.ParseQueryString(string.Empty);

				if (requestCasted.Cursor < 0)
				{
					nvc["before"] = (Math.Abs(requestCasted.Cursor)).ToString();
					if (requestCasted.RecordCount != null)
						nvc["limit"] = requestCasted.RecordCount.Value + 1.ToString();
				}
				else if (requestCasted.Cursor > 0)
				{
					nvc["after"] = (requestCasted.Cursor).ToString();
					if (requestCasted.RecordCount != null)
						nvc["limit"] = requestCasted.RecordCount.Value.ToString();
				}
				// else it's zero so no need to put any query parameter

				if (request is GetPersonalOrdersRequest)
				{
					var requestCasted2 = request as GetPersonalOrdersRequest;
					if (requestCasted2.Status != null)
						foreach (var status in requestCasted2.Status)
							nvc.Add("status", status);
				}

				if (nvc.Keys.Count > 0)
					uriBuilder.Query = nvc.ToString();
			}

            var body = request.RequestBody;
            var method = request.Method;
            var url = uriBuilder.ToString();
			var relativeUrlForSignature = baseURI.MakeRelativeUri(uriBuilder.Uri).ToString();


            using(var httpClient = new HttpClient())
            {
                HttpResponseMessage response;

				if (_authContainer != null)
				{ // authenticated get, required for querying account specific data, but optional for public data
					// Caution: Use the relative URL, *NOT* the absolute one.
					var signature = _authContainer.ComputeSignature("/" + relativeUrlForSignature, method, body);
					httpClient.DefaultRequestHeaders.Add("CB-ACCESS-KEY", signature.ApiKey);
					httpClient.DefaultRequestHeaders.Add("CB-ACCESS-SIGN", signature.Signature);
					httpClient.DefaultRequestHeaders.Add("CB-ACCESS-TIMESTAMP", signature.TimeStamp);
					httpClient.DefaultRequestHeaders.Add("CB-ACCESS-PASSPHRASE", signature.Passphrase);
				}

				httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(ContentType));
				httpClient.DefaultRequestHeaders.Add("User-Agent", "vslee fork of sefbkn.github.io");

                switch(method)
                {
                    case "GET":
                        response = await httpClient.GetAsync(url);
                        break;
                    case "POST":
                        var requestBody = new StringContent(body, Encoding.UTF8, "application/json");
                        response = await httpClient.PostAsync(url, requestBody);
                        break;
                    default:
                        throw new NotImplementedException("The supplied HTTP method is not supported: " + method ?? "(null)");
                }


                var contentBody = await response.Content.ReadAsStringAsync();
                var headers = response.Headers.AsEnumerable();
                var statusCode = response.StatusCode;
                var isSuccess = response.IsSuccessStatusCode;

                var genericExchangeResponse = new ExchangeResponse(statusCode, isSuccess, headers, contentBody);
                return genericExchangeResponse;
            }
        }

    }
}
