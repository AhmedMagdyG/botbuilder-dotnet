﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Microsoft.Bot.Builder.AI.Translation.Model;
using Newtonsoft.Json;

namespace Microsoft.Bot.Builder.AI.Translation.RequestBuilder
{
    /// <summary>
    /// Provides http requests needed for translation and language detection.
    /// </summary>
    internal class TranslatorRequestBuilder : IRequestBuilder
    {
        private const string DetectUrl = "https://api.cognitive.microsofttranslator.com/detect?api-version=3.0";
        private const string TranslateUrl = "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&includeAlignment=true&includeSentenceLength=true";
        private readonly string _apiKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="TranslatorRequestBuilder"/> class.
        /// </summary>
        /// <param name="apiKey">Your subscription key for the Microsoft Translator Text API.</param>
        public TranslatorRequestBuilder(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey));
            }

            _apiKey = apiKey;
        }

        public HttpRequestMessage GetTranslateRequestMessage(string from, string to, IEnumerable<TranslatorRequestModel> translatorRequests)
        {
            var query = $"&from={from}&to={to}";
            var requestUri = new Uri(TranslateUrl + query);
            return GetRequestMessage(requestUri, translatorRequests);
        }

        public HttpRequestMessage GetDetectRequestMessage(IEnumerable<TranslatorRequestModel> translatorRequests)
        {
            var requestUri = new Uri(DetectUrl);
            return GetRequestMessage(requestUri, translatorRequests);
        }

        /// <summary>
        /// Build HttpRequestMessage with its content.
        /// </summary>
        /// <param name="requestUri">Uri of request</param>
        /// <param name="translatorRequests">The models to be included in the content.</param>
        /// <returns>An HttpRequestMessage with its content.</returns>
        private HttpRequestMessage GetRequestMessage(Uri requestUri, IEnumerable<TranslatorRequestModel> translatorRequests)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _apiKey);
            request.Content = new StringContent(JsonConvert.SerializeObject(translatorRequests), Encoding.UTF8, "application/json");
            return request;
        }
    }
}
