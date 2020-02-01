using DispatcherLUISBotExample.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime.Models;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Extensions.Configuration;
using NewsAPI;
using NewsAPI.Constants;
using NewsAPI.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DispatcherLUISBotExample.Services
{
    public class BotServices : IBotServices
    {
        private readonly NewsApiClient newsApiClient;
        public LuisRecognizer Dispatch { get; private set; }
        public QnAMaker SampleQnA { get; private set; }

        private readonly IConfiguration _configration;
        private readonly IHostingEnvironment _hostingEnvironment;

        public BotServices(IConfiguration configuration, IHostingEnvironment hostingEnvironment)
        {
            _configration = configuration;
            _hostingEnvironment = hostingEnvironment;

            // Read the setting for cognitive services (LUIS, QnA) from the appsettings.json
            // If includeApiResults is set to true, the full response from the LUIS api (LuisResult)
            // will be made available in the properties collection of the RecognizerResult

            var luisApplication = new LuisApplication(
                configuration["LuisAppId"],
                configuration["LuisAPIKey"],
               $"https://{configuration["LuisAPIHostName"]}.api.cognitive.microsoft.com");

            // Set the recognizer options depending on which endpoint version you want to use.
            // More details can be found in https://docs.microsoft.com/en-gb/azure/cognitive-services/luis/luis-migration-api-v3
            var recognizerOptions = new LuisRecognizerOptionsV2(luisApplication)
            {
                IncludeAPIResults = true,
                PredictionOptions = new LuisPredictionOptions()
                {
                    IncludeAllIntents = true,
                    IncludeInstanceData = true
                }
            };

            Dispatch = new LuisRecognizer(recognizerOptions);

            SampleQnA = new QnAMaker(new QnAMakerEndpoint
            {
                KnowledgeBaseId = configuration["QnAKnowledgebaseId"],
                EndpointKey = configuration["QnAEndpointKey"],
                Host = configuration["QnAEndpointHostName"]
            });

            //openWeatherMapClient = new OpenWeatherMapClient(appId: configuration["OpenWeatherAppID"]);

            newsApiClient = new NewsApiClient(configuration["NewsApiAPIKey"]);
        }
        public async Task<ArticlesResult> GetNewsByTopicAsync(LuisResult luisResult)
        {
            var result = luisResult;

            var topicEntity = result.Entities
                .SingleOrDefault(q => q.Type == "News.Query");

            var domainEntities = result.Entities
                .Where(q => q.Type.ToLower().Contains("url"))
                .ToList();

            var dateTimeEntity = result.Entities
                .SingleOrDefault(q => q.Type.ToLower().Contains("datetimev2"));

            var sourceEntities = result.Entities
                .Where(q => q.Type == "News.Source")
                .ToList();

            var categoryEntity = result.Entities
                .SingleOrDefault(q => q.Type == "News.Category");

            var languageEntity = result.Entities
               .SingleOrDefault(q => q.Type == "News.Language");

            var topic = topicEntity?.Entity;
            var domain = domainEntities
                .Select(d => d.Entity)
                .ToList();
            var sources = sourceEntities
                 .Select(s => s.Entity)
                 .ToList();

            //date should be fixed
            var dts = ParseDates(dateTimeEntity?.Entity);
            var from = dts?[0];
            var to = dts?[1];

            if (languageEntity != null)
            {
                string json = File.ReadAllText(_hostingEnvironment.ContentRootPath + @"\wwwroot\Data\languages.json");
                var languages = JsonConvert.DeserializeObject<List<LanguageInfo>>(json);
                var code = languages.SingleOrDefault(c => c.Name.ToLower().Contains(languageEntity.Entity)).Code;

                var lang = (Languages)Enum.Parse(typeof(Languages), code, true);
                return await GetNewsByTopicAsync(topic, domain, sources, from, to, lang);
            }
            else
            {
                return await GetNewsByTopicAsync(topic, domain, sources, from, to, null);
            }
        }
        public async Task<ArticlesResult> GetHeadlinesAsync(LuisResult luisResult)
        {
            var result = luisResult;

            var topicEntity = result.Entities
                .SingleOrDefault(q => q.Type == "News.Query");

            var domainEntities = result.Entities
                .Where(q => q.Type.ToLower().Contains("url"))
                .ToList();

            var dateTimeEntity = result.Entities
                .SingleOrDefault(q => q.Type.ToLower().Contains("datetimev2"));

            var sourceEntities = result.Entities
                .Where(q => q.Type == "News.Source")
                .ToList();

            var categoryEntity = result.Entities
                .SingleOrDefault(q => q.Type == "News.Category");

            var countriesEntity = result.Entities
                .SingleOrDefault(q => q.Type.ToLower().Contains("geographyv2"));

            var languageEntity = result.Entities
               .SingleOrDefault(q => q.Type == "News.Language");

            var topic = topicEntity?.Entity;
            var domain = domainEntities
                .Select(d => d.Entity)
                .ToList();
            var sources = sourceEntities
                 .Select(s => s.Entity)
                 .ToList();

            Categories? cat;
            if (categoryEntity != null)
            {
                var category = categoryEntity?.Entity;
                cat = (Categories)Enum.Parse(typeof(Categories), category, true);
            }
            else
            {
                cat = null;
            }



            //date should be fixed
            var dts = ParseDates(dateTimeEntity?.Entity);
            var from = dts?[0];
            var to = dts?[1];

            Languages? lang;
            if (languageEntity != null)
            {
                string json = File.ReadAllText(_hostingEnvironment.ContentRootPath + @"\wwwroot\Data\languages.json");
                var languages = JsonConvert.DeserializeObject<List<LanguageInfo>>(json);
                var code = languages.SingleOrDefault(c => c.Name.ToLower().Contains(languageEntity.Entity)).Code;

                lang = (Languages)Enum.Parse(typeof(Languages), code, true);
            }
            else
            {
                lang = null;
            }

            Countries? country;
            if (countriesEntity != null)
            {
                string json = File.ReadAllText(_hostingEnvironment.ContentRootPath + @"\wwwroot\Data\countries.json");
                var countries = JsonConvert.DeserializeObject<List<CountryInfo>>(json);
                var code = countries.FirstOrDefault(c => c.Name.ToLower().Contains(countriesEntity.Entity)).Alpha2;

                country = (Countries)Enum.Parse(typeof(Countries), code, true);
            }
            else
            {
                country = null;
            }


            if (sources?.Count == 0)
            {
                return await GetHeadlinesByCountryOrCategoryAsync(country, cat, topic, lang);
            }
            else
            {
                return await GetHeadlinesBySourcesAsync(sources, topic, lang);
            }
        }
        public async Task<ArticlesResult> GetNewsByTopicAsync(string topic, List<string> domains, List<string> sources, DateTime? from, DateTime? to, Languages? language)
        {
            var request = new EverythingRequest();
            if (!string.IsNullOrEmpty(topic) && !string.IsNullOrWhiteSpace(topic))
            {
                request.Q = topic;
            }
            if (domains != null)
            {
                request.Domains = domains;
            }
            if (sources != null)
            {
                request.Sources = sources;
            }
            request.From = from;
            request.To = to;
            request.Language = language;
            request.SortBy = SortBys.Relevancy;

            return await newsApiClient.GetEverythingAsync(request);
        }
        public async Task<ArticlesResult> GetHeadlinesByCountryOrCategoryAsync(Countries? country, Categories? category, string topic, Languages? language)
        {
            var request = new TopHeadlinesRequest();
            request.Country = country;
            request.Category = category;
            request.Language = language;
            if (!string.IsNullOrEmpty(topic) && !string.IsNullOrWhiteSpace(topic))
            {
                request.Q = topic;
            }
            var response = await newsApiClient.GetTopHeadlinesAsync(request);

            return response;
        }
        public async Task<ArticlesResult> GetHeadlinesBySourcesAsync(List<string> sources, string topic, Languages? language)
        {
            var request = new TopHeadlinesRequest();
            request.Sources = sources;
            request.Language = language;
            if (!string.IsNullOrEmpty(topic) && !string.IsNullOrWhiteSpace(topic))
            {
                request.Q = topic;
            }
            var response = await newsApiClient.GetTopHeadlinesAsync(request);

            return response;
        }
        private List<DateTime> ParseDates(string inputText)
        {
            if (string.IsNullOrEmpty(inputText) || string.IsNullOrWhiteSpace(inputText))
            {
                return null;
            }

            var dts = new List<DateTime>();
            var regex = new Regex(@"\b\d{1,2}\/\d{1,2}/\d{4}\b");

            foreach (Match m in regex.Matches(inputText))
            {
                DateTime dt;
                string[] format = new string[]
                {
                    "dd/MM/yyyy",
                    "MM/dd/yyyy",
                    "dd.MM.yyyy",
                    "MM.dd.yyyy",
                    "dd/MM/yy",
                    "MM/dd/yy",
                    "dd.MM.yy",
                    "MM.dd.yy",
                    "MM/d/yy",
                    "dd/M/yyyy",
                    "d/M/yyyy",
                    "d/M/yy",
                    "M/d/yyyy",
                    "M/d/yy",
                };
                if (DateTime.TryParseExact(m.Value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                    dts.Add(dt);
            }

            return dts.Count == 0 ? null : dts;
        }
    }
}
