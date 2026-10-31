using Microsoft.Extensions.Configuration;
using Telegram.Bot;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var botToken = config["Telegram:BotToken"]
    ?? throw new InvalidOperationException("Telegram:BotToken is not configured");
var allowedUserId = config.GetValue<long>("Telegram:AllowedUserId");
var notesPath = config["Storage:NotesFilePath"]
    ?? throw new InvalidOperationException("Storage:NotesFilePath is not configured");

Console.WriteLine($"Config OK. Notes path: {notesPath}");
Console.WriteLine($"Allowed user: {allowedUserId}");
Console.WriteLine($"Token present: {!string.IsNullOrWhiteSpace(botToken)}");

var bot = new TelegramBotClient(botToken);
var me = await bot.GetMe();
Console.WriteLine($"Bot OK: @{me.Username}");

var offset = 0;
while (true)
{
    var updates = await bot.GetUpdates(offset: offset, timeout: 10);
    foreach (var update in updates)
    {
        offset = update.Id + 1;
        if (update.Message?.From is { } from)
        {
            Console.WriteLine($"Message from: Id={from.Id}, Username=@{from.Username}, Name={from.FirstName}");
            if (from.Id == allowedUserId)
                Console.WriteLine("  → ALLOWED (совпадает с конфигом)");
            else
                Console.WriteLine("  → REJECTED (не совпадает с конфигом)");
        }
    }
}
