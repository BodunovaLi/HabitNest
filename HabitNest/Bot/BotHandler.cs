using HabitNest.Models;
using HabitNest.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using User = HabitNest.Models.User;

namespace HabitNest.Bot
{
    public class BotHandler
    {
        private readonly UserService _userService;
        private readonly HabitService _habitService;

        private readonly Dictionary<long, UserSession> _userSessions = new();

        public BotHandler(UserService userService, HabitService habitService)
        {
            _userService = userService;
            _habitService = habitService;
        }

        public async Task HandleUpdate(
            ITelegramBotClient bot,
            Update update,
            CancellationToken cancellationToken)
        {
            if (update.CallbackQuery is CallbackQuery callback)
            {
                await HandleCallback(bot, callback, cancellationToken);
                return;
            }

            if (update.Message?.Text != null && update.Message.From != null)
            {
                await HandleText(
                    bot,
                    update.Message.From.Id,
                    update.Message.Chat.Id,
                    update.Message.Text,
                    cancellationToken);
            }
        }

        private async Task HandleCallback(
            ITelegramBotClient bot,
            CallbackQuery callback,
            CancellationToken cancellationToken)
        {
            await bot.AnswerCallbackQuery(
                callbackQueryId: callback.Id,
                cancellationToken: cancellationToken);

            if (callback.Message == null)
            {
                return;
            }

            long telegramId = callback.From.Id;
            long chatId = callback.Message.Chat.Id;
            UserSession session = GetOrCreateSession(telegramId);

            if (callback.Data == "menu:back")
            {
                await ReturnToMenu(bot, chatId, session, cancellationToken);
                return;
            }

            if (callback.Data == "cancel_delete")
            {
                await CancelHabitDeletion(bot, callback.Message.Id, chatId, cancellationToken);
                return;
            }

            string? menuCommand = callback.Data switch
            {
                "menu:add" => "➕ Добавить привычку",
                "menu:habits" => "📋 Мои привычки",
                "menu:complete" => "✅ Отметить выполнение",
                "menu:stats" => "📊 Статистика",
                "menu:change_name" => "👤 Изменить имя",
                _ => null
            };

            if (menuCommand != null)
            {
                if (session.State != UserState.None)
                {
                    await bot.SendMessage(
                        chatId: chatId,
                        text: "Сначала заверши ввод или отправь /start, чтобы отменить его и вернуться в меню.",
                        cancellationToken: cancellationToken);
                    return;
                }

                await HandleMenuCommand(bot, telegramId, chatId, session, menuCommand, cancellationToken);
                return;
            }

            string[] parts = (callback.Data ?? "").Split(':');
            if (parts.Length != 2)
            {
                return;
            }

            switch (parts[0])
            {
                case "delete":
                    await RequestHabitDeletion(bot, parts, telegramId, chatId, cancellationToken);
                    break;
                case "confirm_delete":
                    await ConfirmHabitDeletion(bot, callback.Message.Id, parts, telegramId, chatId, cancellationToken);
                    break;
                case "mark":
                    await MarkHabitCompleted(bot, parts, telegramId, chatId, cancellationToken);
                    break;
                case "edit":
                    await BeginEditHabit(bot, parts, telegramId, chatId, session, cancellationToken);
                    break;
            }
        }

        private async Task HandleText(
            ITelegramBotClient bot, long telegramId, long chatId,
            string messageText, CancellationToken cancellationToken)
        {
            UserSession session = GetOrCreateSession(telegramId);

            if (messageText == "/start")
            {
                await StartRegistrationOrMenu(bot, telegramId, chatId, session, cancellationToken);
                return;
            }

            if (session.State == UserState.None)
            {
                await HandleMenuCommand(bot, telegramId, chatId, session, messageText, cancellationToken);
                return;
            }

            await HandleDialogInput(bot, telegramId, chatId, session, messageText, cancellationToken);
        }

        private async Task HandleMenuCommand(
            ITelegramBotClient bot, long telegramId, long chatId, UserSession session,
            string command, CancellationToken cancellationToken)
        {
            switch (command)
            {
                case "➕ Добавить привычку":
                    await BeginAddHabit(bot, telegramId, chatId, session, cancellationToken);
                    break;
                case "📋 Мои привычки":
                    await ShowHabits(bot, telegramId, chatId, cancellationToken);
                    break;
                case "✅ Отметить выполнение":
                    await ShowCompletionChoices(bot, telegramId, chatId, cancellationToken);
                    break;
                case "📊 Статистика":
                    await ShowStatistics(bot, telegramId, chatId, cancellationToken);
                    break;
                case "👤 Изменить имя":
                    await ChangeName(bot, telegramId, chatId, session, cancellationToken);
                    break;
            }
        }

        private async Task HandleDialogInput(
            ITelegramBotClient bot, long telegramId, long chatId, UserSession session,
            string messageText, CancellationToken cancellationToken)
        {
            switch (session.State)
            {
                case UserState.WaitingForUserName:
                    await RegisterUser(bot, telegramId, chatId, session, messageText, cancellationToken);
                    break;
                case UserState.WaitingForHabitName:
                    await ReadNewHabitName(bot, chatId, session, messageText, cancellationToken);
                    break;
                case UserState.WaitingForHabitDuration:
                    await SaveNewHabit(bot, telegramId, chatId, session, messageText, cancellationToken);
                    break;
                case UserState.WaitingForEditHabitName:
                    await ReadEditedHabitName(bot, chatId, session, messageText, cancellationToken);
                    break;
                case UserState.WaitingForEditHabitDuration:
                    await SaveEditedHabit(bot, telegramId, chatId, session, messageText, cancellationToken);
                    break;
                case UserState.WaitingForNewUserName:
                    await SaveNewUserName(bot, telegramId, chatId, session, messageText, cancellationToken);
                    break;
            }
        }


        private async Task StartRegistrationOrMenu(
            ITelegramBotClient bot,
            long telegramId,
            long chatId,
            UserSession session,
            CancellationToken cancellationToken)
        {
            session.TempHabitName = null;
            session.EditingHabitId = null;

            User? user = _userService.GetByTelegramId(telegramId);

            if (user == null)
            {
                session.State = UserState.WaitingForUserName;

                await bot.SendMessage(
                    chatId: chatId,
                    text: "Привет! Добро пожаловать в HabitNest! Как тебя зовут?",
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                session.State = UserState.None;

                await bot.SendMessage(
                    chatId: chatId,
                    text: $"С возвращением, {user.Name}!",
                    cancellationToken: cancellationToken
                );

                await ShowMainMenu(bot, chatId, cancellationToken);
            }

            return;
        }

        private async Task RegisterUser(
            ITelegramBotClient bot,
            long telegramId,
            long chatId,
            UserSession session,
            string messageText,
            CancellationToken cancellationToken)
        {
            string name = messageText.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Имя не может быть пустым. Попробуй ещё раз.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            _userService.CreateUser(name, telegramId);

            session.State = UserState.None;

            await bot.SendMessage(
                chatId: chatId,
                text: $"Регистрация завершена, {name}!",
                cancellationToken: cancellationToken
            );

            await ShowMainMenu(bot, chatId, cancellationToken);

            return;
        }

        private async Task ChangeName(
            ITelegramBotClient bot, 
            long telegramId, 
            long chatId, 
            UserSession session,
            CancellationToken cancellationToken)
        {
            User? user = _userService.GetByTelegramId(telegramId);

            if (user == null)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Сначала зарегистрируйся через /start.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            session.State = UserState.WaitingForNewUserName;

            await bot.SendMessage(
                chatId: chatId,
                text: "Напиши свое новое имя",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken
            );
        }

        private async Task SaveNewUserName(
           ITelegramBotClient bot,
           long telegramId,
           long chatId,
           UserSession session,
           string messageText,
           CancellationToken cancellationToken)
        {
            string name = messageText.Trim();

            try
            {
                User? user = _userService.ChangeName(name, telegramId);

                if (user == null)
                {
                    session.State = UserState.None;

                    await bot.SendMessage(
                        chatId: chatId,
                        text: "Сначала зарегистрируйся через /start.",
                        cancellationToken: cancellationToken
                    );

                    return;
                }
            }
            catch (ArgumentException ex)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: ex.Message,
                    cancellationToken: cancellationToken
                );

                return;
            }

            session.State = UserState.None;

            await bot.SendMessage(
                chatId: chatId,
                text: $"Имя изменено на «{name}».",
                cancellationToken: cancellationToken
            );

            await ShowMainMenu(bot, chatId, cancellationToken);
        }


        private async Task BeginAddHabit(
            ITelegramBotClient bot,
            long telegramId,
            long chatId,
            UserSession session,
            CancellationToken cancellationToken)
        {
            User? user = _userService.GetByTelegramId(telegramId);

            if (user == null)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Сначала зарегистрируйся через /start.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            session.State = UserState.WaitingForHabitName;

            await bot.SendMessage(
                chatId: chatId,
                text: "Как назовём привычку?",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken
            );

            return;
        }

        private async Task ReadNewHabitName(
            ITelegramBotClient bot,
            long chatId,
            UserSession session,
            string messageText,
            CancellationToken cancellationToken)
        {
            string habitName = messageText.Trim();

            if (string.IsNullOrWhiteSpace(habitName))
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Название привычки не может быть пустым. Попробуй ещё раз.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            session.TempHabitName = habitName;
            session.State = UserState.WaitingForHabitDuration;

            await bot.SendMessage(
                chatId: chatId,
                text: "На сколько дней хочешь создать привычку?",
                cancellationToken: cancellationToken
            );

            return;
        }

        private async Task SaveNewHabit(
            ITelegramBotClient bot,
            long telegramId,
            long chatId,
            UserSession session,
            string messageText,
            CancellationToken cancellationToken)
        {
            string text = messageText.Trim();

            if (!int.TryParse(text, out int durationDays) || durationDays <= 0)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Введи положительное целое число дней.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            if (string.IsNullOrWhiteSpace(session.TempHabitName))
            {
                session.State = UserState.None;
                session.TempHabitName = null;

                await bot.SendMessage(
                    chatId: chatId,
                    text: "Название привычки потеряно. Начни добавление заново.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            User? user = _userService.GetByTelegramId(telegramId);

            if (user == null)
            {
                session.State = UserState.None;
                session.TempHabitName = null;

                await bot.SendMessage(
                    chatId: chatId,
                    text: "Сначала зарегистрируйся через /start.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            string habitName = session.TempHabitName;

            try
            {
                _habitService.AddHabit(habitName, user.Id, durationDays);
            }
            catch (ArgumentException ex) when (ex.ParamName == "name")
            {
                session.TempHabitName = null;
                session.State = UserState.WaitingForHabitName;

                await bot.SendMessage(
                    chatId: chatId,
                    text: "У тебя уже есть привычка с таким названием. Введи другое название.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            session.TempHabitName = null;
            session.State = UserState.None;

            await bot.SendMessage(
                chatId: chatId,
                text: $"Привычка «{habitName}» создана на {durationDays} дней!",
                cancellationToken: cancellationToken
            );

            await ShowMainMenu(bot, chatId, cancellationToken);

            return;
        }


        private async Task ShowHabits(
            ITelegramBotClient bot,
            long telegramId,
            long chatId,
            CancellationToken cancellationToken)
        {
            User? user = _userService.GetByTelegramId(telegramId);

            if (user == null)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Сначала зарегистрируйся через /start.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            List<Habit> habits = _habitService.GetHabits(user.Id);

            if (habits.Count == 0)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "У тебя пока нет привычек.",
                    cancellationToken: cancellationToken
                );

                await ShowMainMenu(bot, chatId, cancellationToken);
                return;
            }

            await bot.SendMessage(
                 chatId: chatId,
                 text: "Твои привычки:",
                 cancellationToken: cancellationToken
                 );

            foreach (Habit habit in habits)
            {
                InlineKeyboardMarkup keyboard = new InlineKeyboardMarkup(
                    new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                    "✏️ Редактировать",
                                    $"edit:{habit.Id}"
                                ),
                            InlineKeyboardButton.WithCallbackData(
                                    "🗑 Удалить",
                                    $"delete:{habit.Id}"
                            )
                        },
                    }
                    );

                await bot.SendMessage(
                     chatId: chatId,
                     text: $"📌 {habit.Name} — {habit.DurationDays} дней",
                     replyMarkup: keyboard,
                     cancellationToken: cancellationToken
                     );
            }

            InlineKeyboardMarkup backKeyboard = new InlineKeyboardMarkup(
                new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(
                            "⬅️ В меню",
                            "menu:back"
                        )
                    }
                }
            );

            await bot.SendMessage(
                chatId: chatId,
                text: "Выбери привычку выше или вернись в меню.",
                replyMarkup: backKeyboard,
                cancellationToken: cancellationToken
            );

            return;
        }

        private async Task ShowStatistics(
            ITelegramBotClient bot,
            long telegramId,
            long chatId,
            CancellationToken cancellationToken)
        {
            User? user = _userService.GetByTelegramId(telegramId);

            if (user == null)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Сначала зарегистрируйся через /start.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            List<Habit> habits = _habitService.GetHabits(user.Id);

            if (habits.Count == 0)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "У тебя пока нет привычек для статистики.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            string message = "📊 Статистика привычек:\n\n";

            foreach (Habit habit in habits)
            {
                message += $"📌 {habit.Name}\n";
                message += $"Продолжительность: {habit.DurationDays} дней\n";
                message += $"Выполнено: {habit.Completions.Count} дней\n";
                message += $"Текущая серия: {habit.GetCurrentStreak()} дней\n";
                message += $"Лучшая серия: {habit.GetBestStreak()} дней\n";

                message += $"{GetProgressBar(habit.Completions.Count, habit.DurationDays)}\n\n";
            }

            await bot.SendMessage(
                chatId: chatId,
                text: message,
                cancellationToken: cancellationToken
            );

            await ShowMainMenu(bot, chatId, cancellationToken);

            return;
        }


        private async Task ShowCompletionChoices(
            ITelegramBotClient bot,
            long telegramId,
            long chatId,
            CancellationToken cancellationToken)
        {
            User? user = _userService.GetByTelegramId(telegramId);

            if (user == null)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Сначала зарегистрируйся через /start.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            List<Habit> habits = _habitService.GetHabits(user.Id);

            if (habits.Count == 0)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "У тебя пока нет привычек.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            string message = "Какую привычку ты выполнила сегодня?\n\n";

            List<InlineKeyboardButton[]> rows = new();

            foreach (Habit habit in habits)
            {
                InlineKeyboardButton button =
                    InlineKeyboardButton.WithCallbackData(
                        habit.Name,
                        $"mark:{habit.Id}"
                    );

                rows.Add(new[] { button });
            }

            InlineKeyboardMarkup keyboard = new InlineKeyboardMarkup(rows);

            await bot.SendMessage(
                chatId: chatId,
                text: message,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken
            );

            return;
        }

        private async Task MarkHabitCompleted(
            ITelegramBotClient bot,
            string[] parts,
            long telegramId,
            long chatId,
            CancellationToken cancellationToken)
        {
            if (!int.TryParse(parts[1], out int habitId))
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Некорректный ID привычки.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            User? user = _userService.GetByTelegramId(telegramId);

            if (user == null)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Сначала зарегистрируйся через /start.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            try
            {
                _habitService.MarkAsCompleted(habitId, user.Id);

                await bot.SendMessage(
                    chatId: chatId,
                    text: "Привычка отмечена",
                    cancellationToken: cancellationToken
                    );

                await ShowMainMenu(bot, chatId, cancellationToken);
            }

            catch (InvalidOperationException ex)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: ex.Message,
                    cancellationToken: cancellationToken
                );

                await ShowMainMenu(bot, chatId, cancellationToken);
            }
        }


        private async Task BeginEditHabit(
            ITelegramBotClient bot,
            string[] parts,
            long telegramId,
            long chatId,
            UserSession session,
            CancellationToken cancellationToken)
        {
            if (!int.TryParse(parts[1], out int habitId))
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Некорректный ID привычки.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            User? user = _userService.GetByTelegramId(telegramId);

            if (user == null)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Сначала зарегистрируйся через /start.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            session.EditingHabitId = habitId;
            session.TempHabitName = null;
            session.State = UserState.WaitingForEditHabitName;

            await bot.SendMessage(
                chatId: chatId,
                text: "Введи новое название привычки.",
                cancellationToken: cancellationToken
            );

            return;
        }

        private async Task ReadEditedHabitName(
            ITelegramBotClient bot,
            long chatId,
            UserSession session,
            string messageText,
            CancellationToken cancellationToken)
        {
            string name = messageText.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Название не может быть пустым. Введи ещё раз.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            session.TempHabitName = name;
            session.State = UserState.WaitingForEditHabitDuration;

            await bot.SendMessage(
                chatId: chatId,
                text: "Введи новую общую продолжительность привычки в днях.",
                cancellationToken: cancellationToken
            );

            return;
        }

        private async Task SaveEditedHabit(
            ITelegramBotClient bot,
            long telegramId,
            long chatId,
            UserSession session,
            string messageText,
            CancellationToken cancellationToken)
        {
            if (!int.TryParse(messageText.Trim(), out int durationDays)
                || durationDays <= 0)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Введи положительное целое число дней.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            if (session.EditingHabitId == null ||
                string.IsNullOrWhiteSpace(session.TempHabitName))
            {
                session.State = UserState.None;
                session.EditingHabitId = null;
                session.TempHabitName = null;

                await bot.SendMessage(
                    chatId: chatId,
                    text: "Данные редактирования потеряны. Начни заново.",
                    cancellationToken: cancellationToken
                );

                await ShowMainMenu(bot, chatId, cancellationToken);

                return;
            }

            User? user = _userService.GetByTelegramId(telegramId);

            if (user == null)
            {
                session.State = UserState.None;
                session.EditingHabitId = null;
                session.TempHabitName = null;

                await bot.SendMessage(
                    chatId: chatId,
                    text: "Сначала зарегистрируйся через /start.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            try
            {
                _habitService.EditHabit(
                    session.EditingHabitId.Value,
                    session.TempHabitName,
                    user.Id,
                    durationDays
                );

                await bot.SendMessage(
                    chatId: chatId,
                    text: "Привычка изменена.",
                    cancellationToken: cancellationToken
                );
            }

            catch (ArgumentException ex) when (ex.ParamName == "name")
            {
                session.TempHabitName = null;
                session.State = UserState.WaitingForEditHabitName;

                await bot.SendMessage(
                    chatId: chatId,
                    text: "У тебя уже есть привычка с таким названием. Введи другое название.",
                    cancellationToken: cancellationToken
                );

                return;
            }
            catch (ArgumentException ex)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: ex.Message,
                    cancellationToken: cancellationToken
                );

                return;
            }

            catch (InvalidOperationException ex)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: ex.Message,
                    cancellationToken: cancellationToken
                );
            }

            session.State = UserState.None;
            session.EditingHabitId = null;
            session.TempHabitName = null;

            await ShowMainMenu(bot, chatId, cancellationToken);

            return;
        }


        private async Task RequestHabitDeletion(
            ITelegramBotClient bot,
            string[] parts,
            long telegramId,
            long chatId,
            CancellationToken cancellationToken)
        {
            if (!int.TryParse(parts[1], out int habitId))
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Некорректный ID привычки.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            User? user = _userService.GetByTelegramId(telegramId);

            if (user == null)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Сначала зарегистрируйся через /start.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            await bot.SendMessage(
                chatId: chatId,
                text: "Удалить привычку вместе с историей выполнения?",
                replyMarkup: new InlineKeyboardMarkup(
                    new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                "🗑 Да, удалить",
                                $"confirm_delete:{habitId}"
                            ),
                            InlineKeyboardButton.WithCallbackData(
                                "Отмена",
                                "cancel_delete"
                            )
                        }
                    }
                ),
                cancellationToken: cancellationToken
            );

            return;
        }

        private async Task ConfirmHabitDeletion(
            ITelegramBotClient bot,
            int messageId,
            string[] parts,
            long telegramId,
            long chatId,
            CancellationToken cancellationToken)
        {
            if (!int.TryParse(parts[1], out int habitId))
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Некорректный ID привычки.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            User? user = _userService.GetByTelegramId(telegramId);

            if (user == null)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Сначала зарегистрируйся через /start.",
                    cancellationToken: cancellationToken
                );

                return;
            }

            try
            {
                _habitService.DeleteHabit(habitId, user.Id);

                await bot.EditMessageText(
                    chatId: chatId,
                    messageId: messageId,
                    text: "Привычка удалена.",
                    replyMarkup: new InlineKeyboardMarkup(
                        Array.Empty<InlineKeyboardButton[]>()
                    ),
                    cancellationToken: cancellationToken
                );
            }

            catch (InvalidOperationException ex)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: ex.Message,
                    cancellationToken: cancellationToken
                );
            }

            await ShowMainMenu(bot, chatId, cancellationToken);

            return;
        }

        private async Task CancelHabitDeletion(
            ITelegramBotClient bot,
            int messageId,
            long chatId,
            CancellationToken cancellationToken)
        {
            await bot.EditMessageText(
                chatId: chatId,
                messageId: messageId,
                text: "Удаление отменено.",
                replyMarkup: new InlineKeyboardMarkup(
                    Array.Empty<InlineKeyboardButton[]>()
                ),
                cancellationToken: cancellationToken
            );

            await ShowMainMenu(bot, chatId, cancellationToken);

            return;
        }


        private async Task ReturnToMenu(
            ITelegramBotClient bot,
            long chatId,
            UserSession session,
            CancellationToken cancellationToken)
        {

            session.State = UserState.None;
            session.TempHabitName = null;
            session.EditingHabitId = null;

            await ShowMainMenu(bot, chatId, cancellationToken);

            return;
        }

        private async Task ShowMainMenu(
            ITelegramBotClient bot,
            long chatId,
            CancellationToken cancellationToken)
        {
            InlineKeyboardMarkup keyboard = new InlineKeyboardMarkup(
                new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(
                            "➕ Добавить привычку",
                            "menu:add"
                        ),
                        InlineKeyboardButton.WithCallbackData(
                            "📋 Мои привычки",
                            "menu:habits"
                        )
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(
                            "✅ Отметить выполнение",
                            "menu:complete"
                        ),
                        InlineKeyboardButton.WithCallbackData(
                            "📊 Статистика",
                            "menu:stats"
                        )
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(
                            "👤 Изменить имя",
                            "menu:change_name"
                        )
                    }
                }
            );

            await bot.SendMessage(
                chatId: chatId,
                text: "Главное меню HabitNest. Выбери действие:",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken
            );
        }


        private UserSession GetOrCreateSession(long telegramId)
        {
            if (!_userSessions.TryGetValue(telegramId, out UserSession? session))
            {
                session = new UserSession
                {
                    TelegramId = telegramId,
                    State = UserState.None
                };

                _userSessions.Add(telegramId, session);
            }
            return session;
        }

        private string GetProgressBar(int completed, int total)
        {
            int filled = (int)Math.Round((double)completed / total * 10);
            filled = Math.Clamp(filled, 0, 10);

            return new string('■', filled)
                 + new string('□', 10 - filled);
        }

    }
}
