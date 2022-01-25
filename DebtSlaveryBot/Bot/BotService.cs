using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using DebtSlaveryBot.Helpers;
using DebtSlaveryBot.Model;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.EntityFrameworkCore;

using System.Threading;
using System.Threading.Tasks;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;

using DebtSlaveryBot.Bot.Commands;
using System.Collections.Concurrent;

namespace DebtSlaveryBot.Bot
{

    class BotService : IBotService
    {
        private readonly ILogger<BotService> _logger;
        private const string BotSettingsConfigSection = "Bot-Settings";
        private ITelegramBotClient BotClient = null;
        private List<ExtendedBotCommand> Commands;

        private ConcurrentDictionary<ChatEntry, Scenario.TelegramBotScenario> Executors;

        public BotService(ILogger<BotService> logger)
        {
            _logger = logger;
            Executors = new ConcurrentDictionary<ChatEntry, Scenario.TelegramBotScenario>();
        }

        public void RunScenario(ChatEntry entry, Scenario.TelegramBotScenario scenario)
        {
            _logger.LogDebug($"New scenario for chat entry <{entry.ChatId}, {entry.UserId}>");
            Executors[entry] = scenario;
        }

        public void ResetScenario(ChatEntry entry)
        {
            _logger.LogDebug($"Reset scenario for chat entry <{entry.ChatId}, {entry.UserId}>");
            if (entry.Primary)
            {
                Executors[entry] = null;
            }
            else
            {
                Executors.TryRemove(entry, out _);
            }
        }

        public (bool, long) GetPrimaryChatId(long tgUserId)
        {
            try
            {
                return (true, Executors.Keys.First(e => e.Primary && e.UserId == tgUserId).ChatId);
            }
            catch (InvalidOperationException)
            {
                var manager = Global.Services.GetService<IDebtManager>();
                var user = manager.GetUser(tgUserId);
                if (user == null)
                {
                    return (false, default(long));
                }
                var chat = user.TgDetails.PrivateChatId;
                Executors.TryAdd(new ChatEntry(tgUserId, chat, true), null);
                return (true, chat);
            }
        }

        public void Start(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service starting");

            Global.DbContext.Database.EnsureCreated();

            var botSettings = Global.Config.GetSection(BotSettingsConfigSection);

            BotClient = new TelegramBotClient(botSettings.GetSection("token").Value);
            var me = BotClient.GetMeAsync().Result;

            _logger.LogInformation($"Bot connected with ID {me.Id} and bot name is {me.Username}");

            Commands = new List<ExtendedBotCommand>
            {
                new StartCommand(_logger, me.Username),
                new HelpCommand(_logger, me.Username),
                new AddDebtCommand(_logger, me.Username),
                new ShareDebtCommand(_logger, me.Username),
                new PayOffDebtsCommand(_logger, me.Username),
                new CancelCommand(_logger, me.Username),
                new GetAllDebtsCommand(_logger, me.Username),
                new GetMyDebtsCommand(_logger, me.Username),
                new ImpersonalModeCommand(_logger, me.Username)
            };

            BotClient.SetMyCommandsAsync(Commands).Wait();

            Helpers.Defaults.DefaultEvent = botSettings.GetSection("active_event").Value;

            _logger.LogInformation($"Launching bot with default event: '{Helpers.Defaults.DefaultEvent}'");

            // temp part for defaulting event
            // todo remove
            var manager = Global.Services.GetService<IDebtManager>();
            var _event = manager.GetEvent(Helpers.Defaults.DefaultEvent);
            if (_event == null)
            {
                manager.AddEvent(Helpers.Defaults.DefaultEvent, null);
                _logger.LogInformation($"Default Event '{Helpers.Defaults.DefaultEvent}' added!");
            }

            var db = Global.Services.GetService<DebtDbContext>();

            var receiverOptions = new ReceiverOptions() { AllowedUpdates = { } }; 
            BotClient.StartReceiving(
                HandleUpdateAsync, 
                HandleErrorAsync, 
                receiverOptions,
                cancellationToken);

            _logger.LogInformation($"Bot main loop launched");
            cancellationToken.WaitHandle.WaitOne();

            _logger.LogInformation($"Terminating bot...");
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken token)
        {
            _logger.LogDebug($"Received update of type {update.Type}");

            try
            {
                var handler = update.Type switch
                {
                    UpdateType.Message => OnMessageReceived(update.Message),
                    _ => Undefined(update)
                };
                await handler;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unhandled exception during request processing: {ex}");
            }
        }

        public async Task Undefined(Update update)
        {
            _logger.LogError($"Update type not supported: {update.Type}!");
            return;
        }

        public async Task OnMessageReceived(Message message)
        {
            _logger.LogDebug($"Received message of type {message.Type} from fn={message.From.FirstName} ln={message.From.LastName} un={message.From.Username} id={message.From.Id}");

            var entry = new ChatEntry(message);
            if (entry.Primary)
            {
                Executors.TryAdd(entry, null); // storing all primary chats
            }

            foreach (var command in Commands)
            {
                if (command.Matches(message))
                {
                    await command.Execute(BotClient, this, message);
                }
            }
            if (!Executors.TryGetValue(entry, out Scenario.TelegramBotScenario scenario) || scenario == null)
            {
                _logger.LogDebug($"Scenario for user {entry.UserId} in chat {entry.ChatId} not found!");
                return;
                // todo print some user help info
            }
            var end = await scenario.Execute(message);
            if (end)
            {
                _logger.LogInformation($"Scenario for user {entry.UserId} in chat {entry.ChatId} executed, removing entry!");
                if (!entry.Primary)
                {
                    Executors.TryRemove(entry, out _);
                }
                else
                {
                    Executors[entry] = null; // saving primary entries forever (for notifications)
                }
            }
        }

        public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exc, CancellationToken token)
        {
            var errorMessage = exc switch 
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exc.ToString()
            };
            _logger.LogError(errorMessage);
        }
    }
}
