using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace PDPTelegramSupportBot;

class Program
{
    private static TelegramBotClient bot;
    private static HashSet<long> GroupIds = new();
    private const string GroupsFile = "groups.txt";

    private static HashSet<long> WaitingUsers = new();
    private static Dictionary<long, string> PendingMessages = new();
    private static Dictionary<long, HashSet<long>> PendingSelections = new();

    static async Task Main()
    {
        string token = "YOUR_TOKEN"; 
        bot = new TelegramBotClient(token);

        LoadGroupsFromFile();

        var me = await bot.GetMe();
        Console.WriteLine($"Бот запущен: @{me.Username}");

        await bot.SetMyCommands(new[]
        {
            new BotCommand { Command = "start", Description = "Запустить бота / показать меню" },
            new BotCommand { Command = "send", Description = "Отправить сообщение в группы" },
            new BotCommand { Command = "groups", Description = "Посмотреть список групп" }
        });

        bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync);
        Console.ReadLine();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var me = await bot.GetMe();

        if (update.Type == UpdateType.CallbackQuery)
        {
            var cq = update.CallbackQuery!;
            long userId = cq.From.Id;

            if (cq.Data == "confirm" && PendingMessages.TryGetValue(userId, out var text))
            {
                PendingSelections[userId] = new();
                await ShowGroupSelection(userId, text, cancellationToken);
            }
            else if (cq.Data == "cancel")
            {
                PendingMessages.Remove(userId);
                PendingSelections.Remove(userId);
                WaitingUsers.Remove(userId);
                await bot.SendMessage(userId, "❌ Рассылка отменена.", cancellationToken: cancellationToken);
            }
            else if (cq.Data.StartsWith("toggle_"))
            {
                if (PendingMessages.TryGetValue(userId, out var text1))
                {
                    long gid = long.Parse(cq.Data.Split("_")[1]);
                    if (!PendingSelections.ContainsKey(userId))
                        PendingSelections[userId] = new();

                    if (PendingSelections[userId].Contains(gid))
                        PendingSelections[userId].Remove(gid);
                    else
                        PendingSelections[userId].Add(gid);

                    await ShowGroupSelection(userId, text1, cancellationToken);
                }
            }
            else if (cq.Data == "send_selected")
            {
                if (PendingMessages.TryGetValue(userId, out var text1))
                {
                    var selected = PendingSelections.ContainsKey(userId) ? PendingSelections[userId] : new();
                    var targets = selected.Count > 0 ? selected : GroupIds;

                    List<long> toRemove = new();
                    foreach (var groupId in targets.ToList())
                    {
                        try
                        {
                            var memberInfo = await bot.GetChatMember(groupId, me.Id, cancellationToken);
                            if (memberInfo.Status is ChatMemberStatus.Administrator or ChatMemberStatus.Creator)
                            {
                                await bot.SendMessage(groupId, text1, cancellationToken: cancellationToken);
                            }
                            else
                            {
                                Console.WriteLine($"⚠ Бот больше не админ в {groupId}, удаляю.");
                                toRemove.Add(groupId);
                            }
                        }
                        catch
                        {
                            Console.WriteLine($"❌ Ошибка при отправке в {groupId}, удаляю.");
                            toRemove.Add(groupId);
                        }
                    }

                    foreach (var gid in toRemove) RemoveGroup(gid);

                    await bot.SendMessage(userId, $"✅ Сообщение отправлено в {targets.Count} групп.", cancellationToken: cancellationToken);

                    PendingMessages.Remove(userId);
                    PendingSelections.Remove(userId);
                }
            }
            else if (cq.Data == "send_all")
            {
                if (PendingMessages.TryGetValue(userId, out var text1))
                {
                    List<long> toRemove = new();
                    foreach (var groupId in GroupIds.ToList())
                    {
                        try
                        {
                            var memberInfo = await bot.GetChatMember(groupId, me.Id, cancellationToken);
                            if (memberInfo.Status is ChatMemberStatus.Administrator or ChatMemberStatus.Creator)
                                await bot.SendMessage(groupId, text1, cancellationToken: cancellationToken);
                            else
                                toRemove.Add(groupId);
                        }
                        catch
                        {
                            toRemove.Add(groupId);
                        }
                    }

                    foreach (var gid in toRemove) RemoveGroup(gid);

                    await bot.SendMessage(userId, $"✅ Сообщение отправлено во все группы.", cancellationToken: cancellationToken);

                    PendingMessages.Remove(userId);
                    PendingSelections.Remove(userId);
                }
            }

            await bot.AnswerCallbackQuery(cq.Id, cancellationToken: cancellationToken);
            return;
        }

        if (update.Type != UpdateType.Message) return;
        var message = update.Message;
        if (message == null) return;

        if (message.NewChatMembers != null)
        {
            foreach (var member in message.NewChatMembers)
            {
                if (member.Id == me.Id)
                {
                    Console.WriteLine($"Бота добавили в группу: {message.Chat.Title} ({message.Chat.Id})");

                    AddGroup(message.Chat.Id);
                    await bot.SendMessage(message.Chat.Id, "✅ Я добавлен в список групп. Дайте мне админку, чтобы я мог отправлять рассылку.");
                }
            }
            return;
        }

        if (message.Chat.Type == ChatType.Private && message.Text != null)
        {
            long userId = message.From?.Id ?? message.Chat.Id;

            if (message.Text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                var sb = new StringBuilder();
                sb.AppendLine("👋 Привет! Я бот для рассылки сообщений в группы.");
                sb.AppendLine();
                sb.AppendLine("Доступные команды:");
                sb.AppendLine("📋 /groups — показать список групп, куда я добавлен.");
                sb.AppendLine("✉️ /send — отправить сообщение в группы.");
                sb.AppendLine("ℹ️ /start — показать это меню.");

                await bot.SendMessage(message.Chat.Id, sb.ToString(), cancellationToken: cancellationToken);
                return;
            }

            if (message.Text.StartsWith("/groups", StringComparison.OrdinalIgnoreCase))
            {
                if (GroupIds.Count == 0)
                {
                    await bot.SendMessage(message.Chat.Id, "Список групп пуст.", cancellationToken: cancellationToken);
                }
                else
                {
                    var sb = new StringBuilder("📋 Группы:\n");
                    foreach (var gid in GroupIds)
                    {
                        try
                        {
                            var chat = await bot.GetChat(gid, cancellationToken);
                            string title = chat.Title ?? "(без названия)";
                            sb.AppendLine($"- {title}");
                        }
                        catch
                        {
                            sb.AppendLine($"- [Ошибка доступа к {gid}]");
                        }
                    }
                    await bot.SendMessage(message.Chat.Id, sb.ToString(), cancellationToken: cancellationToken);
                }
                return;
            }

            if (message.Text.StartsWith("/send", StringComparison.OrdinalIgnoreCase))
            {
                WaitingUsers.Add(userId);
                await bot.SendMessage(message.Chat.Id, "✍️ Отправь сообщение для рассылки.", cancellationToken: cancellationToken);
                return;
            }

            if (WaitingUsers.Contains(userId))
            {
                WaitingUsers.Remove(userId);
                PendingMessages[userId] = message.Text;

                var buttons = new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("✅ Подтвердить", "confirm"),
                        InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel")
                    }
                });

                await bot.SendMessage(
                    message.Chat.Id,
                    $"Вы хотите подготовить это сообщение к рассылке?\n\n\"{message.Text}\"",
                    replyMarkup: buttons,
                    cancellationToken: cancellationToken
                );
            }
        }
    }

    static async Task ShowGroupSelection(long userId, string text, CancellationToken cancellationToken)
    {
        var buttons = new List<List<InlineKeyboardButton>>();
        foreach (var gid in GroupIds)
        {
            string title;
            try
            {
                var chat = await bot.GetChat(gid, cancellationToken);
                title = chat.Title ?? gid.ToString();
            }
            catch
            {
                title = $"[Ошибка {gid}]";
            }

            bool selected = PendingSelections[userId].Contains(gid);
            string label = (selected ? "✅ " : "⬜️ ") + title;

            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(label, $"toggle_{gid}")
            });
        }

        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("🚀 Отправить выбранным", "send_selected")
        });
        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("📢 Отправить во все группы", "send_all")
        });

        await bot.SendMessage(userId,
            $"Выберите группы для рассылки:\n\n\"{text}\"",
            replyMarkup: new InlineKeyboardMarkup(buttons),
            cancellationToken: cancellationToken);
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, System.Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Ошибка: {exception.Message}");
        return Task.CompletedTask;
    }

    static void AddGroup(long chatId)
    {
        if (GroupIds.Add(chatId))
        {
            SaveGroupsToFile();
            Console.WriteLine($"Группа {chatId} добавлена.");
        }
    }

    static void RemoveGroup(long chatId)
    {
        if (GroupIds.Remove(chatId))
        {
            SaveGroupsToFile();
            Console.WriteLine($"Группа {chatId} удалена.");
        }
    }

    static void LoadGroupsFromFile()
    {
        if (File.Exists(GroupsFile))
        {
            foreach (var line in File.ReadAllLines(GroupsFile))
            {
                if (long.TryParse(line, out var id))
                    GroupIds.Add(id);
            }
            Console.WriteLine($"Загружено {GroupIds.Count} групп из файла.");
        }
    }

    static void SaveGroupsToFile()
    {
        File.WriteAllLines(GroupsFile, GroupIds.Select(id => id.ToString()));
    }
}

