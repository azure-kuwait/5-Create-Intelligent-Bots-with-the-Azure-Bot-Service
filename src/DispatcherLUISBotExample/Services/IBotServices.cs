using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime.Models;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.AI.QnA;
using NewsAPI.Constants;
using NewsAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DispatcherLUISBotExample.Services
{
    public interface IBotServices
    {
        LuisRecognizer Dispatch { get; }
        QnAMaker SampleQnA { get; }
        Task<ArticlesResult> GetNewsByTopicAsync(LuisResult luisResult);
        Task<ArticlesResult> GetHeadlinesAsync(LuisResult luisResult);
        Task<ArticlesResult> GetNewsByTopicAsync(string topic, List<string> domains, List<string> sources, DateTime? from, DateTime? to, Languages? language);
        Task<ArticlesResult> GetHeadlinesByCountryOrCategoryAsync(Countries? country, Categories? category, string topic, Languages? language);
        Task<ArticlesResult> GetHeadlinesBySourcesAsync(List<string> sources, string topic, Languages? language);
    }
}
