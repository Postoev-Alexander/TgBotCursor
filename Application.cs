using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using System.Diagnostics;

namespace TgBotCursor;

public class Application
{
    private const string SERVERS_MANAGEMENT = "servers_management";
    private const string DEPLOY_SERVICES = "deploy_services";
    private const string CREATE_SERVER = "create_server";
    private const string LIST_SERVERS = "list_servers";
    private const string DEPLOY_PASSBOLT = "deploy_passbolt";
    private const string DEPLOY_VPN = "deploy_vpn";
    private const string DEPLOY_PROXY = "deploy_proxy";
    private const string BACK = "back";

    private readonly TelegramBotClient _botClient;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public Application()
    {
        _botClient = new TelegramBotClient("6408930022:AAFPLT5Onn9IXfTZONpu3G8ktldCovduapo");
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public static async Task Main()
    {
        var app = new Application();
        await app.Run();
    }

    public async Task Run()
    {
        _botClient.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync);
        Console.WriteLine("Bot started. Press Ctrl+C to exit");

        // Ждем завершения через CancellationToken
        try
        {
            await Task.Delay(Timeout.Infinite, _cancellationTokenSource.Token);
        }
        catch (TaskCanceledException)
        {
            // Graceful shutdown
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.CallbackQuery is not null)
            {
                await HandleCallbackQueryAsync(update.CallbackQuery, cancellationToken);
                return;
            }

            if (update.Message?.Text == "/start")
            {
                await ShowMainMenu(update.Message.Chat.Id, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private async Task ShowMainMenu(long chatId, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("Управление серверами", SERVERS_MANAGEMENT) },
            new[] { InlineKeyboardButton.WithCallbackData("Деплой сервисов", DEPLOY_SERVICES) }
        });

        await _botClient.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(CallbackQuery query, CancellationToken cancellationToken)
    {
        var chatId = query.Message.Chat.Id;
        var messageId = query.Message.MessageId;

        switch (query.Data)
        {
            case SERVERS_MANAGEMENT:
                await ShowServersMenu(chatId, messageId, cancellationToken);
                break;
            case DEPLOY_SERVICES:
                await ShowServicesMenu(chatId, messageId, cancellationToken);
                break;
            case CREATE_SERVER:
                await _botClient.SendTextMessageAsync(chatId, "🔄 Создание сервера...", cancellationToken: cancellationToken);
                break;
            case LIST_SERVERS:
                await _botClient.SendTextMessageAsync(chatId, "📋 Список серверов:\n- Server 1\n- Server 2", cancellationToken: cancellationToken);
                break;
            case DEPLOY_PASSBOLT:
                await ExecuteAnsiblePlaybook("nginx.yml", "hosts.ini", chatId, cancellationToken);
                break;
            case DEPLOY_VPN:
                await ExecuteAnsiblePlaybook("-m shell -a \"uptime\"", "hosts.ini", chatId, cancellationToken);
                break;
            case DEPLOY_PROXY:
                await ExecuteAnsiblePlaybook("ping.yml", "hosts.ini", chatId, cancellationToken);
                break;
            case BACK:
                await ShowMainMenu(chatId, cancellationToken);
                break;
        }

        await _botClient.AnswerCallbackQueryAsync(query.Id, cancellationToken: cancellationToken);
    }

    private async Task ShowServersMenu(long chatId, int messageId, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("Создать сервер", CREATE_SERVER) },
            new[] { InlineKeyboardButton.WithCallbackData("Показать список серверов", LIST_SERVERS) },
            new[] { InlineKeyboardButton.WithCallbackData("« Назад", BACK) }
        });

        await _botClient.EditMessageTextAsync(chatId, messageId, "Управление серверами:", replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    private async Task ShowServicesMenu(long chatId, int messageId, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("Установить Passbolt", DEPLOY_PASSBOLT) },
            new[] { InlineKeyboardButton.WithCallbackData("Установить Outline VPN", DEPLOY_VPN) },
            new[] { InlineKeyboardButton.WithCallbackData("Установить Proxy", DEPLOY_PROXY) },
            new[] { InlineKeyboardButton.WithCallbackData("« Назад", BACK) }
        });

        await _botClient.EditMessageTextAsync(chatId, messageId, "Деплой сервисов:", replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    private async Task ExecuteAnsiblePlaybook(string inventory, string playbook, long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendTextMessageAsync(chatId, $"🔄 Начинаю установку... {playbook}", cancellationToken: cancellationToken);
        try 
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ansible-playbook",
                    Arguments = $"all -i {inventory} {playbook} ",
                    WorkingDirectory = "/app/ansible-bot",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);
            
            if (process.ExitCode != 0 || error.Length > 0 )
            {
                await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка:\n\nЛог:\n{output}\n\nОшибка:\n{error}", cancellationToken: cancellationToken);
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, $"✅ Установка завершена успешно!\n\nЛог:\n{output} \n---------\n{error}", cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await _botClient.SendTextMessageAsync(chatId, $"❌ Ошибка: {ex.Message}", cancellationToken: cancellationToken);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Error occurred: {exception.Message}");
        return Task.CompletedTask;
    }
} 