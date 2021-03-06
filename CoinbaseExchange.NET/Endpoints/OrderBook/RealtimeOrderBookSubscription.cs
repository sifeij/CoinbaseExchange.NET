﻿using CoinbaseExchange.NET.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VSLee.Utils;

namespace CoinbaseExchange.NET.Endpoints.OrderBook
{
	public class RealtimeOrderBookSubscription : ExchangeClientBase
	{
		public static readonly Uri WSS_SANDBOX_ENDPOINT_URL = new Uri("wss://ws-feed-public.sandbox.gdax.com");
		public static readonly Uri WSS_ENDPOINT_URL = new Uri("wss://ws-feed.gdax.com");
		private readonly string ProductString;
		// The websocket feed is publicly available, but connection to it are rate-limited to 1 per 4 seconds per IP.
		/// <summary>
		/// Only for subscribing to Websockets. Polling has its own RateGate
		/// </summary>
		private static readonly RateGate rateGateRealtime = new RateGate(occurrences: 1, timeUnit: new TimeSpan(0, 0, seconds: 4));
		private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		public event EventHandler<RealtimeReceived> RealtimeReceived;
		public event EventHandler<RealtimeOpen> RealtimeOpen;
		public event EventHandler<RealtimeDone> RealtimeDone;
		public event EventHandler<RealtimeMatch> RealtimeMatch;
		public event EventHandler<RealtimeChange> RealtimeChange;
		public event EventHandler<RealtimeError> RealtimeError;

		public RealtimeOrderBookSubscription(string ProductString, CBAuthenticationContainer auth = null) : base(auth)
		{ // + eventually can take an array of productStrings and subscribe simultaneously 
			this.ProductString = ProductString;
		}

		protected virtual void OnRealtimeError(RealtimeError e)
		{
			RealtimeError?.Invoke(this, e);
		}

		/// <summary>
		/// Don't await this - or it won't return until the subscription ends
		/// Authenticated feed messages will not increment the sequence number. It is currently not possible to detect if an authenticated feed message was dropped.
		/// </summary>
		/// <param name="onMessageReceived"></param>
		public async Task SubscribeAsync()
        {
            if (String.IsNullOrWhiteSpace(ProductString))
                throw new ArgumentNullException("product");

			string requestString;
			var uri = ExchangeClientBase.IsSandbox ? WSS_SANDBOX_ENDPOINT_URL : WSS_ENDPOINT_URL;
			if (_authContainer == null)
			{ // unauthenticated feed 
				requestString = String.Format(@"{{""type"": ""subscribe"",""product_id"": ""{0}""}}", ProductString);
			}
			else
			{ // authenticated feed
				var signBlock = _authContainer.ComputeSignature(relativeUrl: "/users/self", method: "GET", body: "");
				requestString = String.Format(
					@"{{""type"": ""subscribe"",""product_id"": ""{0}"",""signature"": ""{1}"",""key"": ""{2}"",""passphrase"": ""{3}"",""timestamp"": ""{4}""}}",
					ProductString, signBlock.Signature, signBlock.ApiKey, signBlock.Passphrase, signBlock.TimeStamp);
				uri = new Uri(uri, "/users/self");
			}
			var requestBytes = UTF8Encoding.UTF8.GetBytes(requestString);
			var subscribeRequest = new ArraySegment<byte>(requestBytes);
			var cancellationToken = cancellationTokenSource.Token;
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					using (var webSocketClient = new ClientWebSocket())
					{
						await webSocketClient.ConnectAsync(uri, cancellationToken);
						if (webSocketClient.State == WebSocketState.Open)
						{
							await rateGateRealtime.WaitToProceedAsync(); // don't subscribe at too high of a rate
							await webSocketClient.SendAsync(subscribeRequest, WebSocketMessageType.Text, true, cancellationToken);
							while (webSocketClient.State == WebSocketState.Open)
							{
								string jsonResponse = "<not assigned>";
								try
								{
									var receiveBuffer = new ArraySegment<byte>(new byte[1024 * 1024 * 5]); // 5MB buffer
									var webSocketReceiveResult = await webSocketClient.ReceiveAsync(receiveBuffer, cancellationToken);
									if (webSocketReceiveResult.Count == 0) continue;

									jsonResponse = Encoding.UTF8.GetString(receiveBuffer.Array, 0, webSocketReceiveResult.Count);
									var jToken = JToken.Parse(jsonResponse);

									var typeToken = jToken["type"];
									if (typeToken == null)
									{
										OnRealtimeError(new RealtimeError("null typeToken: + " + jsonResponse));
										continue; // go to next msg
									}

									var type = typeToken.Value<string>();
									switch (type)
									{
										case "received":
											EventHandler<RealtimeReceived> receivedHandler = RealtimeReceived;
											receivedHandler?.Invoke(this, new RealtimeReceived(jToken));
											break;
										case "open":
											EventHandler<RealtimeOpen> openHandler = RealtimeOpen;
											openHandler?.Invoke(this, new RealtimeOpen(jToken));
											break;
										case "done":
											EventHandler<RealtimeDone> doneHandler = RealtimeDone;
											doneHandler?.Invoke(this, new RealtimeDone(jToken));
											break;
										case "match":
											EventHandler<RealtimeMatch> matchHandler = RealtimeMatch;
											matchHandler?.Invoke(this, new RealtimeMatch(jToken));
											break;
										case "change":
											EventHandler<RealtimeChange> changeHandler = RealtimeChange;
											changeHandler?.Invoke(this, new RealtimeChange(jToken));
											break;
										case "heartbeat":
											// + should implement this
											break;
										case "error":
											OnRealtimeError(new RealtimeError(jToken));
											break;
										default:
											break;
									}
								}
								catch (Newtonsoft.Json.JsonReaderException e)
								{ // Newtonsoft.Json.JsonReaderException occurred Message = Unexpected end of content while loading JObject.Path 'time'
									OnRealtimeError(new RealtimeError(e.Message + ", Msg: " + jsonResponse)); // probably malformed message, so just go to the next msg
								}
							}
						}
					}
				}
				catch (System.Net.WebSockets.WebSocketException e)
				{ // System.Net.WebSockets.WebSocketException: 'The remote party closed the WebSocket connection without completing the close handshake.'
					OnRealtimeError(new RealtimeError(e.Message)); // probably just disconnected, so loop back and reconnect again
				}
				catch (System.OperationCanceledException e)
				{ // System.OperationCanceledException: 'The operation was canceled.'
					OnRealtimeError(new RealtimeError("Cancellation successful: " + e.Message));
					break; // exit loop
				}
			}
		}

		public void UnSubscribe()
		{
			cancellationTokenSource.Cancel();
		}
	}
}
