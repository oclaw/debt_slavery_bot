using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using DebtSlaveryBot.Model;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.EntityFrameworkCore;

using System.Threading;
using System.Threading.Tasks;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;

using DebtSlaveryBot.Bot.Commands;

using System.Collections.Concurrent;

using Microsoft.Extensions.Configuration;

namespace DebtSlaveryBot.Bot
{
    class BotService : IBotService
    {
        private readonly ILogger<BotService> _logger;
        private readonly IServiceProvider _services;
        private readonly IConfiguration _config;

        private ITelegramBotClient BotClient => _services.GetService<ITelegramBotClient>();
        private DbContext DbContext => _services.GetService<DebtDbContext>();
        private List<ExtendedBotCommand> Commands = new List<ExtendedBotCommand>();

        private ConcurrentDictionary<ChatEntry, Scenario.TelegramBotScenario> Executors;

        private const string BotSettingsConfigSection = "Bot-Settings";

        private string BotName;

        public BotService(ILogger<BotService> logger, IServiceProvider provider, IConfiguration config)
        {
            _logger = logger;
            _services = provider;
            _config = config;
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
                var manager = _services.GetService<IDebtManager>();
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

        private void RegisterCoomands()
        {
            var commands = Helpers.Boot.GetActiveCommands(System.Reflection.Assembly.GetExecutingAssembly());
            foreach (var command in commands)
            {
                Commands.Add(
                    (ExtendedBotCommand)Activator.CreateInstance(
                        command, _logger, _services, BotName));
            }
            _logger.LogInformation($"Registered {Commands.Count} commands");
            BotClient.SetMyCommandsAsync(Commands).Wait();
        }

        public void Start(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service starting");

            DbContext.Database.EnsureCreated();

            var botSettings = _config.GetSection(BotSettingsConfigSection);

            var me = BotClient.GetMeAsync().Result;

            BotName = me.Username;

            _logger.LogInformation($"Bot connected with ID {me.Id} and bot name is {me.Username}");

            RegisterCoomands();

            Helpers.Defaults.DefaultEvent = botSettings.GetSection("active_event").Value;

            _logger.LogInformation($"Launching bot with default event: '{Helpers.Defaults.DefaultEvent}'");

            // temp part for defaulting event
            // todo remove
            var manager = _services.GetService<IDebtManager>();
            var _event = manager.GetEvent(Helpers.Defaults.DefaultEvent);
            if (_event == null)
            {
                manager.AddEvent(Helpers.Defaults.DefaultEvent, null);
                _logger.LogInformation($"Default Event '{Helpers.Defaults.DefaultEvent}' added!");
            }

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
                    await command.Execute(message);
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
