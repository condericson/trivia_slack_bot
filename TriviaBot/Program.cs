using Newtonsoft.Json.Linq;
using SlackAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TriviaBot.Models;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.EntityFrameworkCore;
using SlackAPI.WebSocketMessages;
using Microsoft.Extensions.Configuration.Binder;
using Microsoft.Extensions.DependencyInjection;
using trivia_web_bot;

namespace TriviaBot
{
    class Program
    {
        static SlackSocketClient client;
        private static IConfigurationRoot Configuration { get; set; }
        private static string workingTriviaChannelId;
        private static string triviaChannelId;
        private static string adminId;
        private static string adminDM;
        private static TimeSpan timespan;
        private static Guid pendingQuestionId;
        static void Main(string[] args)
        {
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build();

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDbContextPool<TriviaContext>(options =>
                options.UseSqlite(config.GetConnectionString("TriviaContext")));

            serviceCollection.AddSingleton<SlackHelper>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            serviceProvider.GetService<TriviaContext>().Database.Migrate();

            var slackHelper = serviceProvider.GetService<SlackHelper>();

            try
            {
                slackHelper.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            
            pendingQuestionId = Guid.Empty;

        }
    }
}
