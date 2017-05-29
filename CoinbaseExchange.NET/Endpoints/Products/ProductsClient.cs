﻿using CoinbaseExchange.NET.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinbaseExchange.NET.Endpoints.Products
{
	public class ProductsClient : ExchangeClientBase
	{
		public ProductsClient() // no need to authenticate to get products list
			: base(null)
		{

		}

		public async Task<GetProductsResponse> GetProductsAsync()
		{
			var request = new GetProductsRequest();
			var response = await this.GetResponse(request);
			var productsResponse = new GetProductsResponse(response);
			return productsResponse;
		}
	}
}
