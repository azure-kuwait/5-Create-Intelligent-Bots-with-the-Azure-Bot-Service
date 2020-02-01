using DispatcherLUISBotExample.Services;
using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using NewsAPI.Constants;
using NewsAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DispatcherLUISBotExample.Bots
{
    public class DispatcherBot : ActivityHandler
    {
        private ILogger<DispatcherBot> _logger;
        private IBotServices _botServices;

        public DispatcherBot(ILogger<DispatcherBot> logger, IBotServices botServices)
        {
            _logger = logger;
            _botServices = botServices;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            //await turnContext.SendActivityAsync(CreateActivityWithTextAndSpeak($"Echo: {turnContext.Activity.Text}"), cancellationToken);

            // First, we use the dispatch model to determine which cognitive service (LUIS or QnA) to use.
            var recognizerResult = await _botServices.Dispatch.RecognizeAsync(turnContext, cancellationToken);

            // Top intent tell us which cognitive service to use.
            var topIntent = recognizerResult.GetTopScoringIntent();

            // Next, we call the dispatcher with the top intent.
            await DispatchToTopIntentAsync(turnContext, topIntent.intent, recognizerResult, cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(CreateActivityWithTextAndSpeak($"Hello and welcome!"), cancellationToken);
                }
            }
        }

        private IActivity CreateActivityWithTextAndSpeak(string message)
        {
            var activity = MessageFactory.Text(message);
            string speak = @"<speak version='1.0' xmlns='https://www.w3.org/2001/10/synthesis' xml:lang='en-US'>
              <voice name='Microsoft Server Speech Text to Speech Voice (en-US, JessaRUS)'>" +
              $"{message}" + "</voice></speak>";
            activity.Speak = speak;
            return activity;
        }

        private async Task DispatchToTopIntentAsync(ITurnContext<IMessageActivity> turnContext, string intent, RecognizerResult recognizerResult, CancellationToken cancellationToken)
        {
            switch (intent)
            {
                case "l_News":
                    await ProcessNewsAsync(turnContext, recognizerResult.Properties["luisResult"] as LuisResult, cancellationToken);
                    break;
                case "qna-azureq8":
                    await ProcessSampleQnAAsync(turnContext, cancellationToken);
                    break;
                default:
                    _logger.LogInformation($"Dispatch unrecognized intent: {intent}.");
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Dispatch unrecognized intent: {intent}."), cancellationToken);
                    break;
            }
        }

        private async Task ProcessNewsAsync(ITurnContext<IMessageActivity> turnContext, LuisResult luisResult, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProcessNewsAsync");

            // Retrieve LUIS result for News.
            var result = luisResult.ConnectedServiceResult;

            ArticlesResult response;
            //switch (topIntent.TopScoringIntent.Intent)
            switch (result.TopScoringIntent.Intent)
            {
                case "News.Everything":
                    response = await _botServices.GetNewsByTopicAsync(luisResult);

                    if (response.Status == Statuses.Ok)
                    {
                        //await turnContext.SendActivityAsync(MessageFactory.Text($"Here is what you asked for..."), cancellationToken);

                        var attachments = new List<Attachment>();
                        var reply = MessageFactory.Attachment(attachments, "Here is what you asked for...");
                        reply.AttachmentLayout = AttachmentLayoutTypes.List;

                        for (int i = 0; i < response.TotalResults && i < 3; i++)
                        {
                            reply.Attachments.Add(GetHeroCard(response.Articles[i]).ToAttachment());
                        }

                        await turnContext.SendActivityAsync(reply, cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text($"Sorry, we encountered an error! 😞"), cancellationToken);
                        await turnContext.SendActivityAsync(MessageFactory.Text($"{response.Error.Code} --> {response.Error.Message}"), cancellationToken);
                    }

                    break;
                case "News.TopHeadlines":
                    response = await _botServices.GetHeadlinesAsync(luisResult);

                    if (response.Status == Statuses.Ok)
                    {
                        //await turnContext.SendActivityAsync(MessageFactory.Text($"Here is what you asked for..."), cancellationToken);

                        var attachments = new List<Attachment>();
                        var reply = MessageFactory.Attachment(attachments, "Here is what you asked for...");
                        reply.AttachmentLayout = AttachmentLayoutTypes.List;

                        for (int i = 0; i < response.TotalResults && i < 3; i++)
                        {
                            reply.Attachments.Add(GetHeroCard(response.Articles[i]).ToAttachment());
                        }

                        await turnContext.SendActivityAsync(reply, cancellationToken);

                        if (response.TotalResults == 0)
                        {
                            await turnContext.SendActivityAsync(MessageFactory.Text($"There is no news matching your query!"), cancellationToken);
                        }
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text($"Sorry, we encountered an error! 😞"), cancellationToken);
                        await turnContext.SendActivityAsync(MessageFactory.Text($"{response.Error.Code} --> {response.Error.Message}"), cancellationToken);
                    }

                    break;
                default:
                    break;
            }
        }

        private async Task ProcessSampleQnAAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProcessSampleQnAAsync");

            var results = await _botServices.SampleQnA.GetAnswersAsync(turnContext);
            if (results.Any())
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(results.First().Answer), cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Sorry, could not find an answer in the Q and A system."), cancellationToken);
            }
        }

        private HeroCard GetHeroCard(Article article)
        {
            var heroCard = new HeroCard
            {
                Title = article.Title,
                Subtitle = $"Source:{article.Source.Name}   Published At:{(article.PublishedAt.HasValue ? article.PublishedAt.Value.ToShortDateString() : "N/A")}   By:{article.Author}",
                Text = article.Description,
                Images = new List<CardImage> { new CardImage(article.UrlToImage) },
                Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, "Read more", value: article.Url) }
            };

            return heroCard;
        }
    }
}
