using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConflictResolutionBot
{
    class Program
    {
        private static TelegramBotClient? botClient;
        private static CancellationTokenSource cts = new CancellationTokenSource();
        private static readonly ConcurrentDictionary<long, bool> _awaitingQuery = new ConcurrentDictionary<long, bool>();
        static async Task Main(string[] args)
        {
            try
            {
                // Проверка на дублирующиеся процессы
                if (System.Diagnostics.Process.GetProcessesByName(
                    System.Diagnostics.Process.GetCurrentProcess().ProcessName).Length > 1)
                {
                    Console.WriteLine("⚠️ Bot is already running!");
                    return;
                }

                string botToken = Environment.GetEnvironmentVariable("BOT_TOKEN") ?? "";
                botClient = new TelegramBotClient(botToken);

                var receiverOptions = new ReceiverOptions
                {
                    AllowedUpdates = Array.Empty<UpdateType>()
                };

                await botClient.DeleteWebhook();

                // Запускаем обработчик отмены
                cts = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                botClient.StartReceiving(
                    updateHandler: HandleUpdateAsync,
                    errorHandler: HandleErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: cts.Token
                );

                var me = await botClient.GetMe();
                Console.WriteLine($"Bot @{me.Username} started");

                // Бесконечное ожидание
                await Task.Delay(-1, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Bot stopped gracefully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical error: {ex}");
            }
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Only process Message updates
            if (update.Message is not { } message)
                return;

            // Only process text messages
            if (message.Text is not { } messageText)
                return;
            if (update.CallbackQuery is { } callbackQuery)
            {
                await HandleCallbackQueryAsync(botClient, callbackQuery);
                return;
            }
            var chatId = message.Chat.Id;
            if (_awaitingQuery.TryGetValue(chatId, out bool isWaiting) && isWaiting)
            {
                _awaitingQuery.TryRemove(chatId, out _);
                await ProcessSearchQuery(botClient, chatId, messageText, cancellationToken);
                return;
            }
            Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");
            if (message.ReplyToMessage?.Text?.Contains("Введите поисковый запрос") == true)
            {
                string searchQuery = message.Text.StartsWith("/find ")
                    ? message.Text.Substring(6)
                    : message.Text;

                await SearchInfoAsync(botClient, chatId, searchQuery, cancellationToken);
                return;
            }
            // Handle commands
            if (messageText.StartsWith("/"))
            {
                await HandleCommandAsync(botClient, message, cancellationToken);
                return;
            }

            // Handle regular messages or keywords
            if (messageText.Contains("конфликт", StringComparison.OrdinalIgnoreCase))
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Я заметил, что вы интересуетесь темой конфликтов. Могу предложить вам изучить раздел /методики для практических упражнений или /литература для теоретических материалов.",
                    cancellationToken: cancellationToken);
                return;
            }

            // Default response for unrecognized messages
            await botClient.SendMessage(
                chatId: chatId,
                text: "Не совсем понимаю ваш запрос. Пожалуйста, воспользуйтесь командами из меню или напишите /start для получения списка доступных команд.",
                cancellationToken: cancellationToken);
        }

        private static async Task HandleCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var chatId = message.Chat.Id;
            var command = message.Text!.Split(' ')[0].ToLower();

            switch (command)
            {
                case "/start":
                    await SendStartMessageAsync(botClient, chatId, cancellationToken);
                    break;

                case "/literature":
                    await SendLiteratureAsync(botClient, chatId, cancellationToken);
                    break;

                case "/concept":
                    await SendConceptInfoAsync(botClient, chatId, cancellationToken);
                    break;

                case "/methods":
                    await SendMethodologiesAsync(botClient, chatId, cancellationToken);
                    break;

                case "/question":
                    await SendAskQuestionAsync(botClient, chatId, cancellationToken);
                    break;

                case "/find":
                    string searchQuery = message.Text.Contains(" ") ? message.Text.Substring(message.Text.IndexOf(' ') + 1) : "";
                    await HandleSearchCommand(botClient, chatId, searchQuery, cancellationToken, message);
                    break;

                case "/psychological_games":
                    await SendPsychologicalGamesAsync(botClient, chatId, cancellationToken);
                    break;

                case "/young_students":
                    await SendYoungerStudentsInfoAsync(botClient, chatId, cancellationToken);
                    break;

                default:
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "Неизвестная команда. Пожалуйста, используйте /start для получения списка доступных команд.",
                        cancellationToken: cancellationToken);
                    break;
            }

        }

        private static async Task SendStartMessageAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text:
                      "Здравствуйте! 👋\n" +
                      "Я — ваш помощник по вопросам конфликтологической компетентности современных школьников.\n" +
                      "Чем могу помочь?\n\n" +
                      "📚 /literature – список рекомендуемой литературы\n" +
                      "🧠 /concept – Психология подростка\n" +
                      "🛠 /methods – практические приёмы и упражнения\n" +
                      "❓ /question – задать свой вопрос\n" +
                      "🔎 /find – найти информацию по теме\n" +
                      "🎓 /young_students -  младшие школьники\n" +
                      "📝 /psychological_games - психологический игры\n",
                cancellationToken: cancellationToken);
        }

        private static async Task SendLiteratureAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            string text = "📚 *Рекомендуемая литература по конфликтологии*\n\n";
            int count = 1;
            foreach (LiteratureItem item in DataService.Literature)
            {
                if (count > 5)
                {
                    break;
                }
                text += $"{count}. {item.Authors}\n \"{item.Title}\" - {item.Description}, {item.Year}\n";
                count++;
            }
            text += "\n💡 Вы можете ввести /find [фамилия автора или тема] для быстрого поиска.";
            await botClient.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }

        private static async Task SendConceptInfoAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "🧠 *Психология подростка и конфликтологическая компетентность*\n\n" +
                      "*Что такое конфликтологическая компетентность?*\n" +
                      "Конфликтологическая компетентность — это способность человека эффективно взаимодействовать в конфликтных ситуациях, предупреждать, управлять и конструктивно разрешать конфликты.\n\n" +
                      "*Компоненты конфликтологической компетентности:*\n" +
                      "• Когнитивный (знания о природе конфликта)\n" +
                      "• Эмоциональный (управление эмоциями)\n" +
                      "• Поведенческий (владение стратегиями поведения)\n" +
                      "• Мотивационный (готовность к разрешению конфликта)\n" +
                      "• Ценностный (ориентация на сотрудничество)\n\n" +
                      "*Особенности подросткового возраста:*\n" +
                      "• Стремление к самостоятельности\n" +
                      "• Обостренное чувство справедливости\n" +
                      "• Эмоциональная нестабильность\n" +
                      "• Формирование идентичности\n" +
                      "• Значимость мнения сверстников\n\n" +
                      "Хотите узнать больше о методах диагностики конфликтности? Используйте команду /methods",
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }

        private static async Task SendMethodologiesAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Психологический тренинг - «Пробуждение»", "callback1"),
                    InlineKeyboardButton.WithCallbackData("Методика управления конфликтами", "callback2"),
                    InlineKeyboardButton.WithCallbackData("Формирование конфликтологической компетентности", "callback3")
                }
            });

            await botClient.SendMessage(
                chatId: chatId,
                text: "🛠 *Упражнения и тренинги для развития конфликтологической компетентности*\n\n" +
                      "🔸 *Психологический тренинг - «Пробуждение»*\n" +
                      "Цель: повышение психологической компетентности педагогов в вопросах воспитания и развитие эффективных навыков коммуникации с коллегами и  родителями.\n\n" +
                      "🔸 *«Методика управления конфликтами»*\n" +
                      "Цель: научить слушателей анализировать конфликт, понимать его и уметь управлять им, применяя эффективные поведенческие стратегии в профилактике и разрешении конфликтных ситуаций." +
                      "🔸 *«Формирование конфликтологической компетентности»*\n" +
                      "Цель: предоставление возможности участникам тренинга получить опыт конструктивного решения конфликтных ситуаций.\n\n" +
                      "📥 Хотите скачать какую-нибудь методичку? Выберите книгу, которую хотите скачать ниже.",
                parseMode: ParseMode.Markdown,
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }
        private static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery e)
        {
            string filePath = "";
            try
            {
                switch (e.Data)
                {
                    case "callback1":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Пробуждение.pdf";
                        break;
                    case "callback2":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Методика управления конфликтами.pdf";
                        break;
                    case "callback3":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Формирование конфликтологической компетентности.pdf";
                        break;
                    default:
                        Console.WriteLine($"Unknown callback data: {e.Data}");
                        return; // Неправильное значение
                }

                // Проверяем существует ли файл
                if (!System.IO.File.Exists(filePath))
                {
                    await botClient.SendMessage(e.Message.Chat.Id, "Файл не найден.");
                    return;
                }

                // Отправляем файл
                await using (var stream = System.IO.File.OpenRead(filePath))
                {
                    await botClient.SendDocument(
                        chatId: e.Message.Chat.Id,
                        document: new InputFileStream(stream, Path.GetFileName(filePath)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling callback query: {ex.Message}");
                await botClient.SendMessage(e.Message.Chat.Id, "Произошла ошибка при отправке файла.");
            }
        }
        private static async Task SendAskQuestionAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "✍️ Пожалуйста, напишите свой вопрос в свободной форме.\n" +
                      "Например:\n" +
                      "— «Как провести тренинг по развитию эмпатии?»\n" +
                      "— «Есть ли диагностика уровня конфликтности?»\n" +
                      "— «Что делать с постоянными конфликтами в 8 классе?»\n\n" +
                      "🕓 Ответ будет направлен в течение 24 часов.",
                cancellationToken: cancellationToken);
        }

        private static FileSearchService _fileSearchService = new FileSearchService("/root/mediator/Literature");

        private static async Task HandleSearchCommand(ITelegramBotClient botClient, long chatId, string query, CancellationToken cancellationToken, Message message)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _awaitingQuery[chatId] = true;
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "🔍 *Введите ваш поисковый запрос по ключевым словам:*\nПример: `Конфликт`",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken);
                return;
            }
            await ProcessSearchQuery(botClient, chatId, query, cancellationToken);
        }
        private static async Task ProcessSearchQuery(ITelegramBotClient botClient, long chatId, string query, CancellationToken cancellationToken)
        {
            var processingMessage = await botClient.SendMessage(
            chatId: chatId,
            text: "🔍 *Минутку, ищем ответ...*",
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken);
            await botClient.SendChatAction(chatId, ChatAction.Typing);
            var results = await _fileSearchService.SearchInFilesAsync(query);
            await botClient.DeleteMessage(chatId, processingMessage.MessageId, cancellationToken);
            if (results.Count == 0)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"😞 По запросу \"{query}\" ничего не найдено.\nПопробуйте использовать другие ключевые слова.",
                    cancellationToken: cancellationToken);
                return;
            }
            var response = new StringBuilder("🔍 Результаты поиска:\n\n");
            var data = DataService.Literature;
            int count = 1;
            foreach (var result in results)
            {
                switch (result.FileName)
                {
                    case "1.pdf":
                        result.FileName = $"{count}. {data[1].Authors}\n \"{data[1].Title}\" - {data[1].Description}, {data[1].Year}\n";
                        break;
                    case "2.pdf":
                        result.FileName = $"{count}. {data[2].Authors}\n \"{data[2].Title}\" - {data[2].Description}, {data[2].Year}\n";
                        break;
                    case "3.pdf":
                        result.FileName = $"{count}. {data[3].Authors}\n \"{data[3].Title}\" - {data[3].Description}, {data[3].Year}\n";
                        break;
                    case "4.pdf":
                        result.FileName = $"{count}. {data[4].Authors}\n \"{data[4].Title}\" - {data[4].Description}, {data[4].Year}\n";
                        break;
                    case "5.pdf":
                        result.FileName = $"{count}. {data[5].Authors}\n \"{data[5].Title}\" - {data[5].Description}, {data[5].Year}\n";
                        break;
                    case "6.pdf":
                        result.FileName = $"{count}. {data[6].Authors}\n \"{data[6].Title} \" -  {data[6].Description},  {data[6].Year}\n";
                        break;
                    case "7.pdf":
                        result.FileName = $"{count}. {data[7].Authors}\n \"{data[7].Title}\" - {data[7].Description}, {data[7].Year}\n";
                        break;
                    case "8.pdf":
                        result.FileName = $"{count}. {data[8].Authors}\n \"{data[8].Title} \" -  {data[8].Description} ,  {data[8].Year}\n";
                        break;
                    case "9.pdf":
                        result.FileName = $"{count}. {data[9].Authors}\n \"{data[9].Title}\" - {data[9].Description} ,  {data[9].Year}\n";
                        break;
                    case "10.pdf":
                        result.FileName = $"{count}. {data[10].Authors}\n \"{data[10].Title}\" - {data[10].Description}, {data[10].Year}\n";
                        break;
                    case "11.pdf":
                        result.FileName = $"{count}. {data[11].Authors}\n \"{data[11].Title}\" - {data[11].Description} ,  {data[11].Year}\n";
                        break;
                    case "12.pdf":
                        result.FileName = $"{count}. {data[12].Authors}\n \"{data[12].Title}\" - {data[12].Description}, {data[12].Year}\n";
                        break;
                    case "13.pdf":
                        result.FileName = $"{count}. {data[13].Authors}\n \"{data[13].Title} \" -  {data[13].Description} ,  {data[13].Year}\n";
                        break;
                    case "14.pdf":
                        result.FileName = $"{count}. {data[14].Authors}\n \"{data[14].Title} \" -  {data[14].Description} ,  {data[14].Year}\n";
                        break;
                    case "15.pdf":
                        result.FileName = $"{count}. {data[15].Authors}\n \"{data[15].Title}\" - {data[15].Description}, {data[15].Year}\n";
                        break;
                }
                count++;
                response.AppendLine($"{result.FileName}");
                //if (result.PageNumber > 0) response.AppendLine($"📖 Страница: {result.PageNumber}");
                //response.AppendLine($"💬 Найдено: {result.Excerpt}");
                response.AppendLine("------------------------");
            }
            await botClient.SendMessage(
                chatId: chatId,
                text: response.ToString(),
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }
        private static async Task SearchInfoAsync(ITelegramBotClient botClient, long chatId, string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                // Отправляем сообщение с ForceReply и примером
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "🔍 *Введите ваш поисковый запрос:*\nПример: `/find эмпатия`",
                    parseMode: ParseMode.Markdown,
                    replyMarkup: new ForceReplyMarkup { InputFieldPlaceholder = "/find [ваш запрос]" },
                    cancellationToken: cancellationToken);
                return;
            }
            // Simple search implementation - in a real app, this would query a database
            string response;

            if (query.Contains("эмпат", StringComparison.OrdinalIgnoreCase))
            {
                response = "*Результаты поиска по запросу \"эмпатия\":*\n\n" +
                           "📚 *Литература:*\n" +
                           "1. Гиппенрейтер Ю.Б. \"Общаться с ребенком. Как?\" - глава об эмпатическом слушании\n" +
                           "2. Роджерс К. \"Эмпатия\" - классическая работа о природе эмпатии\n\n" +
                           "🛠 *Методики:*\n" +
                           "1. Упражнение \"Зеркало чувств\" - тренировка распознавания эмоций\n" +
                           "2. Тренинг \"В чужих ботинках\" - развитие способности видеть ситуацию глазами другого\n\n" +
                           "Хотите получить полную информацию о методиках развития эмпатии? Используйте команду /methods";
            }
            else if (query.Contains("подрост", StringComparison.OrdinalIgnoreCase))
            {
                response = "*Результаты поиска по запросу \"подросток\":*\n\n" +
                           "📚 *Литература:*\n" +
                           "1. Райс Ф. \"Психология подросткового возраста\"\n" +
                           "2. Реан А.А. \"Психология подростка\"\n" +
                           "3. Фельдштейн Д.И. \"Психология взросления\"\n\n" +
                           "🧠 *Особенности подросткового возраста:*\n" +
                           "• Стремление к самостоятельности\n" +
                           "• Обостренное чувство справедливости\n" +
                           "• Эмоциональная нестабильность\n" +
                           "• Формирование идентичности\n" +
                           "• Значимость мнения сверстников\n\n" +
                           "Для получения полной информации о психологии подростка используйте команду /понятие";
            }
            else
            {
                response = $"По запросу \"{query}\" найдено недостаточно информации. Попробуйте использовать другие ключевые слова или обратитесь к основным разделам:\n\n" +
                           "📚 /литература – список рекомендуемой литературы\n" +
                           "🧠 /понятие – Психология подростка\n" +
                           "🛠 /методики – практические приёмы и упражнения";
            }

            await botClient.SendMessage(
                chatId: chatId,
                text: response,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }

        private static async Task SendPsychologicalGamesAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "📝 *Психологические игры для развития конфликтологической компетентности*\n\n" +
                      "1. «Острова»*\n" +
                      "Цель: развитие навыков сотрудничества и поиска компромисса.\n" +
                      "Описание: Участники делятся на группы, каждая из которых получает \"остров\" (лист бумаги). По мере игры \"острова\" уменьшаются, и группам необходимо размещаться на всё меньшей территории, не выталкивая друг друга.\n\n" +
                      "2. «Конфликтные ситуации»*\n" +
                      "Цель: анализ типичных конфликтных ситуаций и поиск конструктивных решений.\n" +
                      "Описание: Участники получают карточки с описанием конфликтных ситуаций и должны предложить несколько вариантов их разрешения.\n\n" +
                      "3. «Поводырь и слепой»*\n" +
                      "Цель: развитие доверия и ответственности.\n" +
                      "Описание: Участники работают в парах, один с закрытыми глазами, другой выступает в роли поводыря. Затем участники меняются ролями и обсуждают свои ощущения.\n\n" +
                      "4. «Четыре угла»*\n" +
                      "Цель: осознание различных стратегий поведения в конфликте.\n" +
                      "Описание: Каждый угол комнаты обозначает определенную стратегию (соперничество, сотрудничество, компромисс, избегание). Участники выбирают угол в зависимости от своего обычного поведения в конфликте и обсуждают свой выбор.\n\n",
                cancellationToken: cancellationToken);
        }

        private static async Task SendYoungerStudentsInfoAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "Котова С.А., Костикова В.И.\r\nКонфликтная компетентность младших школьников // Герценовские чтения. Начальное образование. СПб, 2011.\r\nИсследуют особенности формирования конфликтной компетентности у младших школьников." +
                      "Лисина М.М.\r\nФормирование личности ребенка в общении. СПб, 2009.\r\nРассматривает роль общения в развитии личности и навыков разрешения конфликтов у детей." +
                      "Агафонова И.Н.\r\nУроки общения для детей 6–10 лет. Программа «Я и Мы». СПб, 2003.\r\nРазрабатывает программу обучения детей навыкам общения и разрешения конфликтов." +
                      "Пилипко Н.В.\r\nПриглашение в мир общения. Ч. 1, 2. М., 1999, 2001.\r\nПособие по развитию коммуникативной компетентности у детей." +
                      "Смирнова Е.О.\r\nМежличностные отношения дошкольников: диагностика, проблемы, коррекция. М., 2005.\r\nИсследует особенности межличностных отношений и конфликтов у дошкольников."+
                      "Хухлаева О.В.\r\nТропинка к своему Я: уроки психологии в начальной школе (1–4). М., 2009.\r\nПособие по развитию самопознания и эмоционального интеллекта у младших школьников.",
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }

        private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }
    }
}