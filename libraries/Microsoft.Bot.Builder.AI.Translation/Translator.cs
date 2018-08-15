// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.AI.Translation.Model;
using Microsoft.Bot.Builder.AI.Translation.PostProcessor;
using Microsoft.Bot.Builder.AI.Translation.PreProcessor;
using Microsoft.Bot.Builder.AI.Translation.RequestBuilder;
using Microsoft.Bot.Builder.AI.Translation.ResponseGenerator;

namespace Microsoft.Bot.Builder.AI.Translation
{
    /// <summary>
    /// Provides access to the Microsoft Translator Text API.
    /// Uses api key and detect input language translate single sentence or array of sentences then apply translation post processing fix.
    /// </summary>
    public class Translator : ITranslator
    {
        private IPreProcessor _preProcessor;
        private IRequestBuilder _requestBuilder;
        private IResponseGenerator _responseGenerator;

        /// <summary>
        /// Initializes a new instance of the <see cref="Translator"/> class.
        /// </summary>
        /// <param name="apiKey">Your subscription key for the Microsoft Translator Text API.</param>
        /// <param name="preProcessor">The PreProcessor to use.</param>"
        /// <param name="requestBuilder">The RequestBuilder to use.</param>
        /// <param name="responseGenerator">The ResponseBuilder to use.</param>
        /// <param name="httpClient">An alternate HTTP client to use.</param>
        public Translator(string apiKey, HttpClient httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey));
            }

            _preProcessor = new TranslatorPreProcessor();
            _requestBuilder = new TranslatorRequestBuilder(apiKey);
            _responseGenerator = new TranslatorResponseGenerator(httpClient);
        }

        public async Task<string> DetectAsync(string textToDetect)
        {
            textToDetect = _preProcessor.PreprocessMessage(textToDetect);

            var payload = new TranslatorRequestModel[] { new TranslatorRequestModel { Text = textToDetect } };

            using (var request = _requestBuilder.GetDetectRequestMessage(payload))
            {
                var detectedLanguages = await _responseGenerator.GenerateDetectResponseAsync(request).ConfigureAwait(false);
                return detectedLanguages.First().Language;
            }
        }

        public async Task<TranslatedDocument> TranslateAsync(string textToTranslate, string from, string to)
        {
            var results = await TranslateArrayAsync(new string[] { textToTranslate }, from, to).ConfigureAwait(false);
            return results.First();
        }

        public async Task<List<TranslatedDocument>> TranslateArrayAsync(string[] translateArraySourceTexts, string from, string to)
        {
            var translatedDocuments = new List<TranslatedDocument>();
            for (var srcTxtIndx = 0; srcTxtIndx < translateArraySourceTexts.Length; srcTxtIndx++)
            {
                // Check for literal tag in input user message
                var currentTranslatedDocument = new TranslatedDocument(translateArraySourceTexts[srcTxtIndx]);
                translatedDocuments.Add(currentTranslatedDocument);
                _preProcessor.PreprocessMessage(currentTranslatedDocument.SourceMessage, out var processedText, out var literanlNoTranslateList);
                currentTranslatedDocument.SourceMessage = processedText;
                translateArraySourceTexts[srcTxtIndx] = processedText;
                currentTranslatedDocument.LiteranlNoTranslatePhrases = literanlNoTranslateList;
            }

            // list of translation request for the service
            var payload = translateArraySourceTexts.Select(s => new TranslatorRequestModel { Text = s });
            using (var request = _requestBuilder.GetTranslateRequestMessage(from, to, payload))
            {
                var translatedResults = await _responseGenerator.GenerateTranslateResponseAsync(request).ConfigureAwait(false);
                var sentIndex = 0;
                foreach (var translatedValue in translatedResults)
                {
                    var translation = translatedValue.Translations.First();
                    var currentTranslatedDocument = translatedDocuments[sentIndex];
                    currentTranslatedDocument.RawAlignment = translation.Alignment?.Projection ?? null;
                    currentTranslatedDocument.TargetMessage = translation.Text;

                    if (!string.IsNullOrEmpty(currentTranslatedDocument.RawAlignment))
                    {
                        var alignments = currentTranslatedDocument.RawAlignment.Trim().Split(' ');
                        currentTranslatedDocument.SourceTokens = PostProcessingUtilities.SplitSentence(currentTranslatedDocument.SourceMessage, alignments);
                        currentTranslatedDocument.TranslatedTokens = PostProcessingUtilities.SplitSentence(translation.Text, alignments, false);
                        currentTranslatedDocument.IndexedAlignment = PostProcessingUtilities.WordAlignmentParse(alignments, currentTranslatedDocument.SourceTokens, currentTranslatedDocument.TranslatedTokens);
                        currentTranslatedDocument.TargetMessage = PostProcessingUtilities.Join(" ", currentTranslatedDocument.TranslatedTokens);
                    }
                    else
                    {
                        var translatedText = translation.Text;
                        currentTranslatedDocument.TargetMessage = translatedText;
                        currentTranslatedDocument.SourceTokens = new string[] { currentTranslatedDocument.SourceMessage };
                        currentTranslatedDocument.TranslatedTokens = new string[] { currentTranslatedDocument.TargetMessage };
                        currentTranslatedDocument.IndexedAlignment = new Dictionary<int, int>();
                    }

                    sentIndex++;
                }

                return translatedDocuments;
            }
        }
    }
}
