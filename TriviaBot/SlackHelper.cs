﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using SlackAPI;
using SlackAPI.WebSocketMessages;
using TriviaBot.Models;
using System.Timers;

namespace trivia_web_bot
{
    public class SlackHelper
    {
        SlackSocketClient client;
        private IConfigurationRoot Configuration { get; set; }
        private string workingTriviaChannelId;
        private string triviaChannelId;
        private string adminId;
        private string adminDM;
        private Guid pendingQuestionId;
        private static TimeSpan timespan;
        private readonly TriviaContext dbcontext;

        public SlackHelper(TriviaContext _dbcontext)
        {
            dbcontext = _dbcontext;
            pendingQuestionId = Guid.Empty;


            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("authentication.json", false, false)
                .AddJsonFile("conversations.json", false, true);

            Configuration = builder.Build();

            string token = Configuration["auth"];

            client = new SlackSocketClient(token);

            workingTriviaChannelId = Configuration["workingchannel"];
            triviaChannelId = Configuration["triviaChannelId"];
            //triviaChannelId = Configuration["workingchannel"];
            adminId = Configuration["adminId"];
            adminDM = Configuration["adminDM"];
            string timeSpanString = Configuration["timespan"];
            int.TryParse(timeSpanString, out int timeSpanHours);
            //timespan = new TimeSpan(0, timeSpanHours, 0, 0);
            timespan = new TimeSpan(0, 1, 0, 0);
        }

        public void Start()
        {
            ManualResetEventSlim clientReady = new ManualResetEventSlim(false);
            Console.WriteLine("Communing with Slack Gods.");
            client.Connect((connected) =>
            {
                // This is called once the client has emitted the RTM start command
                Console.WriteLine("They are ready to receive me.");
                clientReady.Set();
                //MakeDMsEmptyStrings();
            }, () =>
            {
                // This is called once the RTM client has connected to the end point
                Console.WriteLine("The Gods have heard my call.");
            });
            client.OnMessageReceived += OnMessageReceived;
            clientReady.Wait();
            Thread.Sleep(-1);
        }

        private void OnMessageReceived(SlackAPI.WebSocketMessages.NewMessage message)
        {
            if (!string.IsNullOrEmpty(message.user) && message.user != client.MySelf.id)
            {
                string messageText = SanitizeText(message.text);

                //Admin control
                if (message.user == adminId && message.channel == adminDM)
                {
                    if (message.text[0] == '{')
                    {
                        pendingQuestionId = RecordQuestion(message.text);
                        PostMessage(adminId, "Here's what you sent me:");
                        PostQuestion(pendingQuestionId, adminId);
                        PostMessage(adminId, "Want me to post this? Say 'yes post' or 'no post'.");
                    }
                    else if (messageText == "yes post")
                    {
                        if (pendingQuestionId != Guid.Empty)
                        {
                            PostQuestion(pendingQuestionId, triviaChannelId);
                            pendingQuestionId = Guid.Empty;
                        }
                        else
                        {
                            PostMessage(adminId, "Something went wrong! Red alert!");
                            PostMessage(adminId, "Error in admin control");
                        }
                    }
                    else if (messageText == "no post")
                    {
                        pendingQuestionId = Guid.Empty;
                        PostMessage(adminId, "Okay, I won't post it.");
                    }
                    else if (messageText == "score")
                    {
                        PostMessage(adminId, CheckAllScores());
                    }
                    else if (messageText.Contains("delete previous"))
                    {
                        Console.WriteLine("You haven't set up functionality for this yet.");
                    }
                    else if (messageText == "end")
                    {
                        PostMessage(adminDM, "Here's what I'll say to end the round: ");
                        PostMessage(adminDM, EndRound());
                        PostMessage(adminDM, "Want me to end round? Say 'yes end' or 'no end'.");
                    }
                    else if (messageText == "yes end")
                    {
                        PostMessage(triviaChannelId, EndRound());
                    }
                    else if (messageText == "no end")
                    {
                        PostMessage(adminDM, "Trivia bot no end.");
                    }
                    else if (messageText.Contains("history"))
                    {
                        GetMessageHistory();
                    }
                    else if (messageText == "test")
                    {
                        PostMessage(message.channel, "Test passed!");
                    }
                    else if (messageText == "delete history 8675309")
                    {
                        DeleteHistory();
                    }
                    else if (message.text.Contains("delete player attempt"))
                    {
                        // Needs to follow pattern: 'delete player attempt @slackmention'
                        PostMessage(adminDM, message.text);
                        DeleteMostRecentPlayerAttempt(message.text);
                    }
                    else if (messageText == "delete last question 8675309")
                    {
                        DeleteMostRecentQuestion();
                    }
                    else if (messageText.Split(" ")[0] == "tell")
                    {
                        Ventriloquist(message.text);
                    }
                    else if (messageText.Contains("add attempt for"))
                    {
                        PostMessage(adminDM, ManuallyAddAttempts(message.text));
                    }
                    else if (messageText == "help")
                    {
                        var helpMessage = "Hey Connor, here's what you can tell me:\n" +
                                          "Send me a question using json format.\n" +
                                          "Delete all attempts by saying 'delete attempts 8675309'.\n" +
                                          "Delete a player's last attempt by saying 'delete history attempt @[slackmention]'.\n" +
                                          "Delete the most recent question by saying 'delete last question 8675309'.\n" +
                                          "Make me send a DM to a user by saying 'tell user @[slackmention] [Here's the sentence I want to send]'.\n" +
                                          "Add attempts for a player by saying 'add attempt for @[slackmention] [letter]'.\n" +
                                          "Get current score in trivia channel by saying 'current score'.\n" +
                                          "Get current score in this channel by saying 'score'.\n";
                        PostMessage(adminDM, helpMessage);
                    }
                    else
                    {
                        PostMessage(adminDM, Converse(message.user, messageText));
                    }
                }
                //For all other users
                else if (message.channel == triviaChannelId && message.user == adminId)
                {
                    if (messageText == "current score")
                    {
                        PostMessage(triviaChannelId, CheckAllScores());
                    }
                }
                else if (message.channel != triviaChannelId)
                {
                    if (LookUpPlayer(message.user) == null)
                    {
                        if (CreatePlayer(message.user, message.channel) == null)
                        {
                            PostMessage(message.user, "Something went wrong! I tried to add you but it looks like you're a demon.");
                            PostMessage(adminId, "Something went wrong! Red alert!");
                            PostMessage(adminId, "Error in createplayer");
                        }
                    }
                    var responseToCheck = CheckForAndAddDM(message.user, message.channel);
                    if (responseToCheck != "DM exists")
                    {
                        PostMessage(adminId, responseToCheck);
                    }
                    if (messageText.Contains("help"))
                    {
                        PostMessage(message.user,
                            "Need help?\n" +
                            "When I post a question, send me a direct message with the letter of the answer that you think is correct.\n" +
                            "If you'd like to check your current score, send me a direct message and say 'score'."
                            );
                    }
                    else if (messageText == "score")
                    {
                        PostMessage(message.channel, "Getting your score...");
                        PostMessage(message.channel, "Your current score is " + CheckPlayerScores(message.user) + "!");
                    }
                    else if (messageText == "a" || messageText == "b" || messageText == "c" || messageText == "d")
                    {
                        PostMessage(message.channel, RecordAnswer(message, messageText));
                    }

                    else
                    {
                        PostMessage(message.channel, Converse(message.user, messageText));
                    }

                }
                if (message.user != adminId)
                {
                    if (message.channel != "C6ZPBU15W")
                    {
                        var speakingPlayer = LookUpPlayer(message.user);
                        PostMessage(adminId, "*Message received from " + speakingPlayer.PlayerName + ":* " + messageText);
                    }

                }
            }
        }





        //For responding
        void PostMessage(string channelId, string responseMessage)
        {
            try
            {
                client.PostMessage((mr) => Console.WriteLine("Sent message on " + channelId + " and said " + responseMessage), channelId, responseMessage, null, null, true, null, false, null, null, true, null);
            }
            catch (Exception ex)
            {
                HandleExceptions("PostMessage", ex, false);
            }
        }

        string SanitizeText(string text)
        {
            string safeText = "";
            safeText = text.ToLower();
            var sb = new StringBuilder();
            foreach (char c in safeText)
            {
                if (!char.IsPunctuation(c))
                    sb.Append(c);
            }
            safeText = sb.ToString();
            return safeText;
        }

        List<string> FindUsersInMessage(string message)
        {
            List<string> users = new List<string>();
            var words = message.Split(' ');
            foreach (var word in words)
            {
                if (word.Length > 2 && word[1] == '@')
                {
                    users.Add(word.Substring(2, word.Length - 3));
                }
            }
            return users;
        }

        List<string> RemoveUsersFromMessage(string message)
        {
            List<string> remainingWords = new List<string>();
            var words = message.Split(' ');
            foreach (var word in words)
            {
                if (word[0] != '@')
                {
                    remainingWords.Add(word);
                }
            }
            return remainingWords;
        }

        // Make function for parsing sender

        Guid RecordQuestion(string json)
        {
            Console.WriteLine("Recording question");
            try
            {
                var input = JObject.Parse(json);
                var questionStatement = input.GetValue("question").Value<string>();
                var answers = ((JArray)input["answers"]).Select(x => x.Value<string>()).ToArray();

                var correctAnswer = input.GetValue("correctAnswer").Value<string>();

                var correctAnswerGuid = Guid.NewGuid();

                List<Answer> setOfAnswers = new List<Answer>();
                foreach (var a in answers)
                {
                    Answer answer = new Answer
                    {
                        AnswerId = Guid.NewGuid(),
                        Statement = a,
                        IsCorrect = false,
                    };
                    setOfAnswers.Add(answer);
                }
                Answer correctAnswerDefined = new Answer
                {
                    AnswerId = correctAnswerGuid,
                    Statement = correctAnswer,
                    IsCorrect = true,
                };

                setOfAnswers.Add(correctAnswerDefined);

                Question question = new Question
                {
                    QuestionId = Guid.NewGuid(),
                    Statement = questionStatement,
                    Date = DateTime.Now,
                    Answers = setOfAnswers.OrderBy(x => x.Statement).ToList(),
                };
                dbcontext.Questions.Add(question);
                dbcontext.SaveChanges();
                return question.QuestionId;
            }
            catch (Exception ex)
            {
                ExceptionLogging(ex);
                return Guid.Empty;
            }

        }

        void PostQuestion(Guid questionId, string channelId)
        {
            var question = dbcontext.Questions.FirstOrDefault(x => x.QuestionId == questionId);
            var answerSet = dbcontext.Answers.Where(x => x.Question.QuestionId == question.QuestionId).OrderBy(x => x.Statement).ToList();
            var postedTriviaQuestion = "@channel\n*Trivia of the Day* for " + question.Date.ToString("MM/dd/yyyy") + ": \n\n" + question.Statement + "\n\n";
            var answerLetter = 'A';
            foreach (var answer in answerSet)
            {
                postedTriviaQuestion += answerLetter + ": " + answer.Statement + " \n";
                answerLetter += (char)1;
            }
            postedTriviaQuestion += "\n*Do not reply with your answer in this channel!*\n" +
                                    "\nSo players remain anonymous, make sure you send a direct message to @triviabot with the LETTER of your guess!\n" +
                                    "You have *10 hours* to answer the question. Good luck!";
            PostMessage(channelId, postedTriviaQuestion);
            SetReminder();
            SetEndRound();
        }



        //string PrintMostRecentQuestion()
        //{
        //    string message = "";
        //    Question question = new Question();
        //    List<Answer> answers = null;
        //    using (TriviaContext dbcontext = new TriviaContext())
        //    {
        //        question = dbcontext.Questions.OrderByDescending(x => x.Date).FirstOrDefault();
        //        answers = dbcontext.Answers.Where(x => x.Question.QuestionId == question.QuestionId).OrderByDescending(x => x.Statement).ToList();
        //    }
        //    message += question.Statement 
        //    return message;
        //}



        string RecordAnswer(NewMessage message, string messageText)
        {
            Player currentPlayer = new Player();
            var previousScore = CheckPlayerScores(message.user);
            var playersAnswer = "";
            bool correctOrIncorrect;

            var question = dbcontext.Questions.OrderByDescending(x => x.Date).First();
            PostMessage(adminDM, ("question.Date is " + question.Date + ". Timespan is " + timespan + ". DateTime.Now is " + DateTime.Now));
            if (question.Date + timespan < DateTime.Now)
            {
                PostMessage(adminDM, currentPlayer.PlayerName + " answered too late.");
                return ("Thanks " + currentPlayer.PlayerName + " for your answer! Unfortunately, you've answered after the end of the round. Check back soon for another question!");
            }
            var answers = dbcontext.Answers.Where(x => x.Question.QuestionId == question.QuestionId).OrderBy(x => x.Statement).ToList();
            currentPlayer = dbcontext.Players.FirstOrDefault(x => x.PlayerSlackId == message.user);
            if (dbcontext.Attempts.FirstOrDefault(x => x.Question.QuestionId == question.QuestionId && x.Player.PlayerId == currentPlayer.PlayerId) != null)
            {
                PostMessage(adminDM, currentPlayer.PlayerName + " tried to answer again but I found an answer for them already.");
                return ("Thanks " + currentPlayer.PlayerName + "! You answered " + messageText.ToUpper() + ". Your answer has been...wait a second, didn't you already answer this one?");
            }
            //playersAnswer = CreateAttempt(messageText, question, currentPlayer, answers);
            Attempt newAttempt = new Attempt
            {
                AttemptId = Guid.NewGuid(),
                Question = question,
                Player = currentPlayer,
                Answer = answers[(int)(messageText[0] - 'a')],
            };
            Console.WriteLine("Message text: " + messageText[0]);
            Console.WriteLine("Answer statement: " + answers[(int)(messageText[0] - 'a')].Statement);
            playersAnswer = answers[(int)(messageText[0] - 'a')].Statement;
            Console.WriteLine("Player Answer: " + playersAnswer);
            if (answers[(int)(messageText[0] - 'a')].IsCorrect)
            {
                newAttempt.Correct = true;
                Console.WriteLine("it was correct!");
                correctOrIncorrect = newAttempt.Correct;
            }
            else
            {
                newAttempt.Correct = false;
                Console.WriteLine("Wrong!");
                correctOrIncorrect = newAttempt.Correct;
            }
            dbcontext.Attempts.Add(newAttempt);
            dbcontext.SaveChanges();

            var postScore = CheckPlayerScores(message.user);
            PostMessage(adminDM, currentPlayer.PlayerName + " just guessed! Previous score was " + previousScore + ". Current score is " + postScore + ".");
            var responseToPlayer = "Thanks " + currentPlayer.PlayerName + "! You answered " + messageText.ToUpper() + ". " + playersAnswer + ". \n";
            if (correctOrIncorrect)
            {
                responseToPlayer += "*You got it right!*";
            }
            if (!correctOrIncorrect)
            {
                responseToPlayer += "*Unfortunately, it was not correct.*";
            }
            responseToPlayer += "\nYour answer has been recorded. The final scores for all players will be posted before the next question!";
            return responseToPlayer;
        }




        //string CreateAttempt(string messageText, Question question, Player currentPlayer, List<Answer> answers)
        //{
        //    var playersAnswer = "";
        //    using (TriviaContext dbcontext = new TriviaContext())
        //    {
        //        Attempt newAttempt = new Attempt
        //        {
        //            AttemptId = Guid.NewGuid(),
        //            Question = question,
        //            Player = currentPlayer,
        //            Answer = answers[(int)(messageText[0] - 'a')],
        //        };
        //        Console.WriteLine("Message text: " + messageText[0]);
        //        Console.WriteLine("Answer statement: " + answers[(int)(messageText[0] - 'a')].Statement);
        //        playersAnswer = answers[(int)(messageText[0] - 'a')].Statement;
        //        Console.WriteLine("Player Answer: " + playersAnswer);
        //        if (answers[(int)(messageText[0] - 'a')].IsCorrect)
        //        {
        //            newAttempt.Correct = true;
        //            Console.WriteLine("it was correct!");
        //        }
        //        else
        //        {
        //            newAttempt.Correct = false;
        //            Console.WriteLine("Wrong!");
        //        }
        //        dbcontext.Attempts.Add(newAttempt);
        //        dbcontext.SaveChanges();
        //    }
        //    return playersAnswer;
        //}





        Player CreatePlayer(string playerId, string directMessageChannel)
        {
            Player newPlayer = null;
            User user = null;
            client.UserLookup.TryGetValue(playerId, out user);

            newPlayer = new Player
            {
                PlayerId = Guid.NewGuid(),
                PlayerSlackId = user.id,
                PlayerName = user.profile.first_name,
                PlayerMention = user.name,
                //DirectMessage = directMessageChannel,
            };
            dbcontext.Players.Add(newPlayer);
            dbcontext.SaveChanges();

            return newPlayer;
        }

        Player LookUpPlayer(string playerId)
        {
            return dbcontext.Players.FirstOrDefault(x => x.PlayerSlackId == playerId);
        }

        string CheckAllScores()
        {
            string scoreSheet = "Current Scores: \n";

            var playerList = dbcontext.Players.OrderByDescending(p => dbcontext.Attempts.Where(a => a.Player.PlayerId == p.PlayerId && a.Correct).Count()).ToList();

            if (playerList == null)
            {
                return "Error on db look up";
            }
            else
            {
                foreach (var player in playerList)
                {
                    var score = dbcontext.Attempts.Where(a => a.Player.PlayerId == player.PlayerId && a.Correct).Count();
                    scoreSheet += player.PlayerName + ": " + score + "\n";
                }
                return scoreSheet;
            }
        }

        string CheckPlayerScores(string playerId)
        {
            string scoreReport = "";

            Player requestingPlayer = dbcontext.Players.FirstOrDefault(x => x.PlayerSlackId == playerId);
            var attempts = dbcontext.Attempts.Where(x => x.Player.PlayerId == requestingPlayer.PlayerId);
            var score = 0;
            foreach (var attempt in attempts)
            {
                if (attempt.Correct)
                {
                    score++;
                }
            }
            scoreReport = score.ToString();

            return scoreReport;
        }

        string EndRound()
        {
            var endRoundMessage = "@channel\n\n";
            var congratzMessage = "Congratz ";
            var currentScoreMessage = CheckAllScores();

            var mostRecentQuestion = dbcontext.Questions.OrderByDescending(x => x.Date).First();
            //var attemptsForQuestion = dbcontext.Attempts.Where(x => x.Question.QuestionId == mostRecentQuestion.QuestionId).ToList();
            var playersWithCorrectAttempts = dbcontext.Players.Where(x => dbcontext.Attempts.FirstOrDefault(y => y.PlayerId == x.PlayerId && y.Question.QuestionId == mostRecentQuestion.QuestionId && y.Correct) != null).ToList();
            var numberOfCorrectPlayers = playersWithCorrectAttempts.Count();
            var correctAnswer = dbcontext.Answers.FirstOrDefault(x => x.Question.QuestionId == mostRecentQuestion.QuestionId && x.IsCorrect);

            if (numberOfCorrectPlayers == 0)
            {
                congratzMessage = "Unfortunately, no one got the correct answer for " + mostRecentQuestion.Date.ToString("MM/dd/yyyy") + ". :slightly_frowning_face:";
            }
            else
            {
                if (numberOfCorrectPlayers == 1)
                {
                    congratzMessage += "@" + playersWithCorrectAttempts[0].PlayerMention;
                }
                else if (numberOfCorrectPlayers == 2)
                {
                    congratzMessage += "@" + playersWithCorrectAttempts[0].PlayerMention + " and @" + playersWithCorrectAttempts[1].PlayerMention;
                }
                else
                {
                    for (int i = 0; i < numberOfCorrectPlayers - 1; i++)
                    {
                        congratzMessage += "@" + playersWithCorrectAttempts[i].PlayerMention + ", ";
                    }
                    congratzMessage += "and @" + playersWithCorrectAttempts[numberOfCorrectPlayers - 1].PlayerMention;
                }
                congratzMessage += " for getting the correct answer to the trivia on " + mostRecentQuestion.Date.ToString("MM/dd/yyyy") + "! \n\n" +
                    "*The correct answer was " + correctAnswer.Statement + ".*\n\n";
            }
            return endRoundMessage + congratzMessage + currentScoreMessage;
        }

        void HandleExceptions(string functionName, Exception ex, bool postMessage)
        {
            string errorMessage = "Something broke! An exception was thrown on function " + functionName + " that said " + ex.Message + " with inner exception " + ex.InnerException + " .";
            if (postMessage == true)
            {
                PostMessage(adminId, errorMessage);
            }
            else
            {
                Console.WriteLine(errorMessage);
            }
        }

        void GetMessageHistory()
        {
            Channel channel;
            var channels = client.ChannelLookup.TryGetValue(triviaChannelId, out channel);
            client.GetChannelHistory((cmh) =>
            {
                foreach (var message in cmh.messages.ToList())
                {
                    Console.WriteLine("Message: " + message.text);
                }
            }, channel);
        }

        void DeletePreviousMessage()
        {
            //client.DeleteMessage(Console.WriteLine("deleted previous message"), triviaChannelId, )
        }

        string Converse(string slackUserId, string message)
        {
            var workingMessage = SanitizeText(message);
            var response = "";
            var conversationModels = new List<ConversationModel>();
            var conversationModelsPlayer = new List<ConversationModel>();
            Configuration.GetSection("conversations:Player").Bind(conversationModelsPlayer);
            conversationModels.AddRange(conversationModelsPlayer);

            if (slackUserId == adminId)
            {
                var conversationModelsAdmin = new List<ConversationModel>();
                Configuration.GetSection("conversations:Admin").Bind(conversationModelsAdmin);
                conversationModels.AddRange(conversationModelsAdmin);
            }

            foreach (var conversationPairing in conversationModels)
            {
                foreach (var request in conversationPairing.Statement)
                {
                    if (workingMessage == request)
                    {
                        Random rand = new Random();
                        int randomInt = rand.Next(0, conversationPairing.Response.Count());
                        response = conversationPairing.Response[randomInt];
                    }
                }

            }
            return response;
        }


        // Exception handling
        void ExceptionLogging(Exception ex)
        {
            Console.WriteLine(ex.Message + "\n" + ex.InnerException);
        }


        void DeleteHistory()
        {
            dbcontext.Attempts.RemoveRange(dbcontext.Attempts.ToList());
            dbcontext.Questions.RemoveRange(dbcontext.Questions.ToList());
            dbcontext.SaveChanges();
            PostMessage(adminDM, "Deleted history");
            PostMessage(adminDM, CheckAllScores());
        }


        void DeleteMostRecentPlayerAttempt(string command)
        {
            string slackPlayerMention = command.Split(' ').ToList()[3];
            Console.WriteLine(slackPlayerMention);
            Attempt playerAttempt = new Attempt();
            var playerSlackId = slackPlayerMention.Substring(2, slackPlayerMention.Length - 3);
            Console.WriteLine("Here is the player mention: " + playerSlackId);
            Player player = new Player();
            player = dbcontext.Players.FirstOrDefault(x => x.PlayerSlackId == playerSlackId);
            var mostRecentQuestion = dbcontext.Questions.OrderByDescending(x => x.Date).First();
            Console.WriteLine("mostRecentQuestion: " + mostRecentQuestion.Statement);
            playerAttempt = dbcontext.Attempts.FirstOrDefault(x => x.Player == player && x.Question == mostRecentQuestion);
            dbcontext.Attempts.Remove(playerAttempt);
            dbcontext.SaveChanges();
            PostMessage(adminDM, "Deleted " + player.PlayerName + "'s last attempt.");
        }


        void DeleteMostRecentQuestion()
        {
            var mostRecentQuestion = dbcontext.Questions.OrderByDescending(x => x.Date).FirstOrDefault();
            var mostRecentAnswers = dbcontext.Answers.Where(x => x.Question == mostRecentQuestion).ToList();
            if (mostRecentAnswers != null && mostRecentQuestion != null)
            {
                dbcontext.Answers.RemoveRange(mostRecentAnswers);
                dbcontext.Questions.Remove(mostRecentQuestion);
                dbcontext.SaveChanges();
                PostMessage(adminDM, "Deleted most recent question.");
            }
            else
            {
                PostMessage(adminDM, "Something exploded...");
            }
        }


        string CheckForAndAddDM(string playerId, string playerDM)
        {
            var player = dbcontext.Players.FirstOrDefault(x => x.PlayerSlackId == playerId);
            if (player == null)
            {
                return "Something went wrong in CheckForAndAddDM.";
            }
            else if (string.IsNullOrWhiteSpace(player.DirectMessage))
            {
                player.DirectMessage = playerDM;
                dbcontext.SaveChanges();
                return $"Added DM to {player.PlayerName}'s profile.";
            }
            else
            {
                return "DM exists";
            }
        }


        void Ventriloquist(string message)
        //Tell @username Here's the sentence I want to send.
        {
            var words = message.Split(' ');
            var message2 = string.Join(" ", words.Skip(2).ToArray());
            var unformattedSlackId = words[1];
            var playerSlackId = unformattedSlackId.Substring(2, unformattedSlackId.Length - 3);
            var player = new Player();
            var playerDM = "";
            player = dbcontext.Players.Where(x => x.PlayerSlackId == playerSlackId).FirstOrDefault();
            if (player != null)
            {
                playerDM = player.DirectMessage;
            }
            else
            {
                Console.WriteLine("It died...");
            }
            PostMessage(adminDM, $"*Telling {player.PlayerName}:* {message2}");
            PostMessage(playerDM, message2);
        }




        //string RecordAnswer(NewMessage message, string messageText)
        //{
        //    Player currentPlayer = new Player();
        //    var previousScore = CheckPlayerScores(message.user);
        //    var playersAnswer = "";
        //    using (TriviaContext dbcontext = new TriviaContext())
        //    {
        //        var question = dbcontext.Questions.OrderByDescending(x => x.Date).First();
        //        var answers = dbcontext.Answers.Where(x => x.Question.QuestionId == question.QuestionId).OrderBy(x => x.Statement).ToList();
        //        currentPlayer = dbcontext.Players.FirstOrDefault(x => x.PlayerSlackId == message.user);
        //        if (dbcontext.Attempts.FirstOrDefault(x => x.Question.QuestionId == question.QuestionId && x.Player.PlayerId == currentPlayer.PlayerId) != null)
        //        {
        //            PostMessage(adminDM, currentPlayer.PlayerName + " tried to answer again but I found an answer for them already.");
        //            return ("Thanks " + currentPlayer.PlayerName + "! You answered " + messageText.ToUpper() + ". Your answer has been...wait a second, didn't you already answer this one?");
        //        }
        //        //playersAnswer = CreateAttempt(messageText, question, currentPlayer, answers);
        //        Attempt newAttempt = new Attempt
        //        {
        //            AttemptId = Guid.NewGuid(),
        //            Question = question,
        //            Player = currentPlayer,
        //            Answer = answers[(int)(messageText[0] - 'a')],
        //        };
        //        Console.WriteLine("Message text: " + messageText[0]);
        //        Console.WriteLine("Answer statement: " + answers[(int)(messageText[0] - 'a')].Statement);
        //        playersAnswer = answers[(int)(messageText[0] - 'a')].Statement;
        //        Console.WriteLine("Player Answer: " + playersAnswer);
        //        if (answers[(int)(messageText[0] - 'a')].IsCorrect)
        //        {
        //            newAttempt.Correct = true;
        //            Console.WriteLine("it was correct!");
        //        }
        //        else
        //        {
        //            newAttempt.Correct = false;
        //            Console.WriteLine("Wrong!");
        //        }
        //        dbcontext.Attempts.Add(newAttempt);
        //        dbcontext.SaveChanges();
        //    }
        //    var postScore = CheckPlayerScores(message.user);
        //    PostMessage(adminDM, currentPlayer.PlayerName + " just guessed! Previous score was " + previousScore + ". Current score is " + postScore + ".");
        //    return ("Thanks " + currentPlayer.PlayerName + "! You answered " + messageText.ToUpper() + ". " + playersAnswer + ". Your answer has been recorded! The final score will be posted before the next question!");
        //}





        string ManuallyAddAttempts(string message)
        //add attempt for @username a
        {
            try
            {
                var words = message.Split(' ');
                var guess = words[4];
                var unformattedSlackId = words[3];
                var playerSlackId = unformattedSlackId.Substring(2, unformattedSlackId.Length - 3);
                Console.WriteLine(playerSlackId);
                var currentPlayer = new Player();
                var previousScore = CheckPlayerScores(playerSlackId);
                var playersAnswer = "";

                currentPlayer = dbcontext.Players.FirstOrDefault(x => x.PlayerSlackId == playerSlackId);
                Console.WriteLine(currentPlayer.PlayerSlackId);
                if (currentPlayer == null)
                {
                    return "currentPlayer is null. Couldn't find a player";
                }
                Console.WriteLine("Got here!");
                var question = dbcontext.Questions.OrderByDescending(x => x.Date).First();
                var answers = dbcontext.Answers.Where(x => x.Question.QuestionId == question.QuestionId).OrderBy(x => x.Statement).ToList();
                Console.WriteLine("Got here!");
                if (dbcontext.Attempts.FirstOrDefault(x => x.Question.QuestionId == question.QuestionId && x.Player.PlayerId == currentPlayer.PlayerId) != null)
                {
                    return ($"Attempt already created for {currentPlayer.PlayerName}");
                }
                //        playersAnswer = CreateAttempt(guess, question, currentPlayer, answers);
                Attempt newAttempt = new Attempt
                {
                    AttemptId = Guid.NewGuid(),
                    Question = question,
                    Player = currentPlayer,
                    Answer = answers[(int)(guess[0] - 'a')],
                };
                Console.WriteLine("Message text: " + guess[0]);
                Console.WriteLine("Answer statement: " + answers[(int)(guess[0] - 'a')].Statement);
                playersAnswer = answers[(int)(guess[0] - 'a')].Statement;
                Console.WriteLine("Player Answer: " + playersAnswer);
                if (answers[(int)(guess[0] - 'a')].IsCorrect)
                {
                    newAttempt.Correct = true;
                    Console.WriteLine("it was correct!");
                }
                else
                {
                    newAttempt.Correct = false;
                    Console.WriteLine("Wrong!");
                }
                dbcontext.Attempts.Add(newAttempt);
                dbcontext.SaveChanges();
                var postScore = CheckPlayerScores(playerSlackId);
                return $"{currentPlayer.PlayerName}'s guess added. Previous score was {previousScore}. Current score is {postScore}";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return "fail";
            }
        }

        void SetReminder()
        {
            var milliseconds = (timespan - new TimeSpan(0, 1, 0, 0)).TotalMilliseconds;
            var timer = new System.Timers.Timer(milliseconds);
            timer.Elapsed += ReminderEvent;
            timer.AutoReset = false;
            timer.Enabled = true;
        }

        private void ReminderEvent(Object source, ElapsedEventArgs e)
        {
            PostMessage(triviaChannelId, "Hey @channel! One hour left to answer!");
        }

        void SetEndRound()
        {
            var milliseconds = (timespan).TotalMilliseconds;
            var timer = new System.Timers.Timer(milliseconds);
            timer.Elapsed += EndRoundEvent;
            timer.AutoReset = false;
            timer.Enabled = true;
        }

        private void EndRoundEvent(Object source, ElapsedEventArgs e)
        {
            PostMessage(triviaChannelId, EndRound());
        }






    }
}
