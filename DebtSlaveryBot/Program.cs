using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using DebtSlaveryBot.Bot;
using DebtSlaveryBot.Model;
using Telegram.Bot;


namespace DebtSlaveryBot
{
    class Program
    {
        private static IConfigurationRoot _config = null;
        private static ILogger _logger = null;
        private static IServiceProvider _serviceProvider;

        static void LogFatal(string message)
        {
            if (_logger != null)
            {
                _logger.LogCritical(message);
            }
            else
            {
                Console.WriteLine($"FATAL_ERROR (_logger not initialized): {message}");
            }
        }

        static void Main(string[] args)
        {
            try
            {
                MainAsync(args).Wait();

                _logger.LogInformation("Application exited normally");
            }
            catch (AggregateException aex)
            {
                LogFatal("AggregateException");
                foreach (var ex in aex.InnerExceptions)
                {
                    LogFatal($"Main service terminated with exception: {ex}");
                    LogFatal($"StackTrace: {ex.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                LogFatal($"Main service terminated with exception: {ex}");
                LogFatal($"StackTrace: {ex.StackTrace}");
            }
        }

        const string DbEngineSection = "DbEngine";

        const string PgSqlConnString = "PgSql";
        const string InMemConnString = "InMemory";
        const string MySqlConnString = "MySql";

        private const string BotSettingsConfigSection = "Bot-Settings";


        static IServiceCollection InitPgSql(IServiceCollection services, ILoggerFactory logFac)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            return services.AddDbContext<DebtDbContext>(opts =>
                opts
                    .UseNpgsql(_config.GetConnectionString(PgSqlConnString),
                        o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))    // for lazy loading support (PgSQL does not support MARS)
                    .UseSnakeCaseNamingConvention()                                             // for proper table names (PgSQL constraint)
                    .UseLazyLoadingProxies()
                    .UseLoggerFactory(logFac));
        }

        static IServiceCollection InitInMemDb(IServiceCollection services, ILoggerFactory logFac)
        {

            return services.AddDbContext<DebtDbContext>(opts =>
                opts.UseInMemoryDatabase(_config.GetConnectionString(InMemConnString))
                    .UseLoggerFactory(logFac));
        }

        static IServiceCollection InitMySqlDatabse(IServiceCollection services, ILoggerFactory logFac)
        {
            // TODO: support mysql backend
            throw new NotImplementedException();
        }

        static IServiceCollection InitDatabase(IServiceCollection serviceCollection, ILoggerFactory logFac)
        {
            var dbEngine = _config.GetValue<string>(DbEngineSection);
            return dbEngine switch
            {
                PgSqlConnString => InitPgSql(serviceCollection, logFac),
                InMemConnString => InitInMemDb(serviceCollection, logFac),
                MySqlConnString => InitMySqlDatabse(serviceCollection, logFac),
                _ => throw new Exception($"Not supported db engine: {dbEngine}")
            };
        }

        static void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddLogging(configure => configure.AddConfiguration(_config.GetSection("Logging"))
                                                               .AddConsole());

            var provider = serviceCollection.BuildServiceProvider();

            var loggerFactory = provider.GetService<ILoggerFactory>();

            _logger = loggerFactory.CreateLogger<Program>();

            _logger.LogInformation("Initializing services...");

            InitDatabase(serviceCollection, loggerFactory);

            serviceCollection.AddSingleton<IConfiguration>(_config)         // app settings service
                             .AddSingleton<IBotService, BotService>()       // bot scenario receiver & executor
                             .AddTransient<IDebtManager, DebtManager>()     // data model service
                             .AddSingleton<ITelegramBotClient>(sp => {      // telegram transport service
                    var botSettings = _config.GetSection(BotSettingsConfigSection);
                    var token = botSettings.GetValue<string>("token");
                    return new TelegramBotClient(token);
            });

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        static async Task MainAsync(string[] args)
        {
            _config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("appsettings.json", false)
                .Build();

            var serviceCollection = new ServiceCollection();

            ConfigureServices(serviceCollection);

            var mainService = _serviceProvider.GetService<IBotService>();

            _logger.LogInformation("Starting main service");

            CancellationTokenSource cts = new CancellationTokenSource();
            var token = cts.Token;

            mainService.Start(token);
        }
    }
}
