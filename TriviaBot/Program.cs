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
            } catch (Exception ex)
            {
              Console.WriteLine(ex);
            }
            


            pendingQuestionId = Guid.Empty;


            //var builder = new ConfigurationBuilder()
            //  .SetBasePath(Directory.GetCurrentDirectory())
            //  .AddJsonFile("authentication.json", false, false)
            //  .AddJsonFile("conversations.json", false, true);

            //Configuration = builder.Build();

            //string token = Configuration["auth"];

            //client = new SlackSocketClient(token);

            //workingTriviaChannelId = Configuration["workingchannel"];
            //triviaChannelId = Configuration["triviaChannelId"];
            ////triviaChannelId = Configuration["workingchannel"];
            //adminId = Configuration["adminId"];
            //adminDM = Configuration["adminDM"];
            //string timeSpanString = Configuration["timespan"];
            //int.TryParse(timeSpanString, out int timeSpanHours);
            //timespan = new TimeSpan(0, timeSpanHours, 0, 0);

            //ManualResetEventSlim clientReady = new ManualResetEventSlim(false);
            //Console.WriteLine("Communing with Slack Gods.");
            //client.Connect((connected) =>
            //{
            //    // This is called once the client has emitted the RTM start command
            //    Console.WriteLine("They are ready to receive me.");
            //    clientReady.Set();
            //    //MakeDMsEmptyStrings();
            //}, () =>
            //{
            //    // This is called once the RTM client has connected to the end point
            //    Console.WriteLine("The Gods have heard my call.");
            //});
            //client.OnMessageReceived += OnMessageReceived;
            //clientReady.Wait();
            //Thread.Sleep(-1);
        }
    }
}
