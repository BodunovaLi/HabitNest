using HabitNest.Bot;
using HabitNest.Services;
using Telegram.Bot;

string token = Environment.GetEnvironmentVariable("HABITNEST_BOT_TOKEN")
    ?? throw new InvalidOperationException("Не задан токен Telegram-бота");

TelegramBotClient botClient = new TelegramBotClient(token);

UserService userService = new UserService();
HabitService habitService = new HabitService();

BotHandler handler = new BotHandler(userService, habitService);

using CancellationTokenSource cts = new();

botClient.StartReceiving(
    updateHandler: handler.HandleUpdate,
    errorHandler: HandleError,
    cancellationToken: cts.Token
);

Console.WriteLine("Бот запущен. Нажмите Enter для остановки.");
Console.ReadLine();

cts.Cancel();

Task HandleError(
    ITelegramBotClient bot,
    Exception exception,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"Ошибка: {exception.Message}");
    return Task.CompletedTask;
}