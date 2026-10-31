using Microsoft.Extensions.Configuration;
using RemindMe.Core;

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