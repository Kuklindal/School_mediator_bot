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
        private static readonly long AdminChatId = long.Parse(Environment.GetEnvironmentVariable("ADMIN_CHAT_ID") ?? "796409454");
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

                string botToken = Environment.GetEnvironmentVariable("BOT_TOKEN") ?? "7498059198:AAHYyadAbssQsSVVe6jKh9uIuYjl931QdJI";
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
            if (update.CallbackQuery is CallbackQuery)
            {
                var callbackQuery = update.CallbackQuery;
                await HandleCallbackQueryAsync(botClient, callbackQuery);
                return;
            }
            if (update.Message is not { } message)
                return;

            // Only process text messages
            if (message.Text is not { } messageText)
                return;

            var chatId = message.Chat.Id;
            if (_awaitingQuery.TryGetValue(chatId, out bool isWaiting) && isWaiting)
            {
                _awaitingQuery.TryRemove(chatId, out _);
                await ProcessSearchQuery(botClient, chatId, messageText, cancellationToken);
                return;
            }
            if (_awaitingQuestion.TryGetValue(chatId, out bool isWaitingForQuestion) && isWaitingForQuestion)
            {
                _awaitingQuestion.TryRemove(chatId, out _);
                await ForwardQuestionToAdmin(botClient, message);
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "✅ Ваш вопрос принят! Спасибо, мы ответим в ближайшее время.",
                    cancellationToken: cancellationToken);
                return;
            }
            Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");
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
        private static async Task ForwardQuestionToAdmin(ITelegramBotClient botClient, Message message)
        {
            if (AdminChatId == 0)
            {
                Console.WriteLine("Admin chat ID is not set!");
                return;
            }

            try
            {
                // Формируем информацию о пользователе
                var userInfo = $"Новый вопрос от пользователя:\n" +
                               $"👤 {message.Chat.FirstName} {message.Chat.LastName} (@{message.Chat.Username})\n" +
                               $"🆔 ID: {message.Chat.Id}\n\n" +
                               $"✉️ Вопрос:";

                // Отправляем информацию о пользователе
                await botClient.SendMessage(
                    chatId: AdminChatId,
                    text: userInfo,
                    cancellationToken: default);

                // Пересылаем оригинальное сообщение
                await botClient.ForwardMessage(
                    chatId: AdminChatId,
                    fromChatId: message.Chat.Id,
                    messageId: message.MessageId,
                    cancellationToken: default);

                Console.WriteLine($"Question forwarded from {message.Chat.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error forwarding question: {ex}");
            }
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
                      "🎓 /young_students -  младшие школьники\n",
                cancellationToken: cancellationToken);
        }

        private static async Task SendLiteratureAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
{
                new []
                {
                    InlineKeyboardButton.WithCallbackData("1", "callback11"),
                    InlineKeyboardButton.WithCallbackData("2", "callback12"),
                    InlineKeyboardButton.WithCallbackData("3", "callback13"),
                    InlineKeyboardButton.WithCallbackData("4", "callback14"),
                    InlineKeyboardButton.WithCallbackData("5", "callback18"),
                    InlineKeyboardButton.WithCallbackData("6", "callback19")
                }
            });
            string text = "📚 *Рекомендуемая литература по конфликтологии*\n\n";
            text += "1. Абраменкова В.В.\n" +
                    "   Социальная психология детства. М., 2008.\n" +
                    "   Рассматривает особенности социального развития детей, включая формирование навыков взаимодействия и разрешения конфликтов.\n\n";
            text += "2. Немов Р.С.\n" +
                    "   Психология: Учебник для студентов высших педагогических заведений. Т. 2. М., 2007.\n" +
                    "   Содержит разделы, посвящённые межличностным отношениям и конфликтам в образовательной среде.\n\n";
            text += "3. Хасан Б.И.\n" +
                    "   Психотехника конфликта и конфликтная компетентность. Красноярск, 1996.\n" +
                    "   Предлагает психотехнические подходы к развитию конфликтной компетентности.\n\n";
            text += "4. Соколов С. В.\n" +
                    "   Социальная конфликтология. Москва, 2001.\n" +
                    "   Рассматриваются природа и классификация социальных конфликтов.\n\n";
            text += "5. Реан А. А.\n" +
                    "   ПСИХОЛОГИЯ ДЕВИАНТНОСТИ. Дети, Общество, Закон. Москва, 2022.\n" +
                    "   Книга дает развернутую психологическую характеристику отклоняющегося поведения, обращается к различным формам его проявления.\n\n";
            text += "6. Деркач А. А.\n" +
                    "   Акмеология. Москва, 2004.\n" +
                    "   В книге рассмотрены основные акмеологические понятия, методологические подходы и принципы акмеологии, методы акмеологического исследования и практики, акмеологические стратегии оптимизации развития личности и социума и др.\n\n";
            text += "📥 Хотите скачать какую-нибудь книгу? Выберите её номер ниже.";
            await botClient.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Markdown,
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }

        private static async Task SendConceptInfoAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
{
                new []
                {
                    InlineKeyboardButton.WithCallbackData("1", "callback8"),
                    InlineKeyboardButton.WithCallbackData("2", "callback9"),
                    InlineKeyboardButton.WithCallbackData("3", "callback10")
                }
            });
            string text = "📚 *Рекомендуемая литература по конфликтной компетентности младших школьников*\n\n";
            text += "1. Гришина Н.В.\n" +
                    "   Психология конфликта. СПб, 2008.\n" +
                    "   Обобщает теоретические и практические аспекты конфликтов, включая их проявления в школьной среде.\n\n";

            text += "2. Анцупов А. Я., Баклановский С. В.\n" +
                    "   Конфликтология в схемах и комментариях. СПб, 2009.\n" +
                    "   Учебное пособие, в котором отражены результаты применения системного подхода к исследованию конфликтов.\n\n";

            text += "3. Хван А.А., Зайцев Ю.А., Кузнецова  Ю.А.\n" +
                    "   Стандартизированный опросник измерения агрессивных и враждебных реакций А.Басса и А.Дарки. М., 2005.\n" +
                    "   Пособие содержит данные по стандартизации широко известной методики исследования агрессивных и враждебных реакций Басса-Дарки.\n\n";
            text += "📥 Хотите скачать какую-нибудь книгу? Выберите её номер ниже.";

            await botClient.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Markdown,
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }

        private static async Task SendMethodologiesAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("1", "callback1"),
                    InlineKeyboardButton.WithCallbackData("2", "callback2"),
                    InlineKeyboardButton.WithCallbackData("3", "callback3"),
                    InlineKeyboardButton.WithCallbackData("4", "callback15"),
                    InlineKeyboardButton.WithCallbackData("5", "callback16"),
                    InlineKeyboardButton.WithCallbackData("6", "callback17")
                }
            });

            await botClient.SendMessage(
                chatId: chatId,
                text: "🛠 *Упражнения и тренинги для развития конфликтологической компетентности*\n\n" +
                      "1. 🔸 *Психологический тренинг - «Пробуждение»*\n" +
                      "Цель: повышение психологической компетентности педагогов в вопросах воспитания и развитие эффективных навыков коммуникации с коллегами и  родителями.\n\n" +
                      "2. 🔸 *«Методика управления конфликтами»*\n" +
                      "Цель: научить слушателей анализировать конфликт, понимать его и уметь управлять им, применяя эффективные поведенческие стратегии в профилактике и разрешении конфликтных ситуаций.\n\n" +
                      "3. 🔸 *«Формирование конфликтологической компетентности»*\n" +
                      "Цель: предоставление возможности участникам тренинга получить опыт конструктивного решения конфликтных ситуаций.\n\n" +
                      "4. 🔸 *«Как научить детей сотрудничать? Психологические игры и упражнения»*\n" +
                      "Цель: показать упражнения для развития навыков сотрудничества и разрешения конфликтов.\n\n" +
                      "5. 🔸 *«Детская психология»*\n" +
                      "Цель: описать этапы психологического развития детей, включая аспекты, связанные с конфликтами.\n\n" +
                      "6. 🔸 *«Формирование конфликтологической компетентности»*\n" +
                      "Цель: предоставление возможности участникам тренинга получить опыт конструктивного решения конфликтных ситуаций.\n\n" +
                      "📥 Хотите скачать какую-нибудь методичку? Выберите её номер ниже.",
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
                    case "callback4":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Формирование личности ребенка в общении.pdf";
                        break;
                    case "callback5":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Приглашение в мир общения.pdf";
                        break;
                    case "callback6":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Межличностные отношения дошкольников.pdf";
                        break;
                    case "callback7":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Тропинка к своему Я.pdf";
                        break;
                    case "callback8":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Психология конфликта.pdf";
                        break;
                    case "callback9":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Конфликтология в схемах и комментариях.pdf";
                        break;
                    case "callback10":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Стандартизированный опросник.pdf";
                        break;
                    case "callback11":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Социальная психология детства.pdf";
                        break;
                    case "callback12":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Психология - yчебник для студентов высших педагогических заведений.pdf";
                        break;
                    case "callback13":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Психотехника конфликта и конфликтная компетентность.pdf";
                        break;
                    case "callback14":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Социальная конфликтология.pdf";
                        break;
                    case "callback15":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Как научить детей сотрудничать.pdf";
                        break;
                    case "callback16":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Детская психология.pdf";
                        break;
                    case "callback17":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Психологические игры для детей.pdf";
                        break;
                    case "callback18":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/ПСИХОЛОГИЯ ДЕВИАНТНОСТИ.pdf";
                        break;
                    case "callback19":
                        await botClient.AnswerCallbackQuery(e.Id, showAlert: false);
                        filePath = "/root/mediator/Literature/Акмеология.pdf";
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
        private static readonly ConcurrentDictionary<long, bool> _awaitingQuestion = new();
        private static async Task SendAskQuestionAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            _awaitingQuestion[chatId] = true;
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
                response.AppendLine("------------------------");
            }
            await botClient.SendMessage(
                chatId: chatId,
                text: response.ToString(),
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }

        private static async Task SendYoungerStudentsInfoAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
{
                new []
                {
                    InlineKeyboardButton.WithCallbackData("1", "callback4"),
                    InlineKeyboardButton.WithCallbackData("2", "callback5"),
                    InlineKeyboardButton.WithCallbackData("3", "callback6"),
                    InlineKeyboardButton.WithCallbackData("4", "callback7")
                }
            });
            string text = "📚 *Рекомендуемая литература по конфликтной компетентности младших школьников*\n\n";
            text += "1. Лисина М.М.\n" +
                    "   Формирование личности ребенка в общении. СПб, 2009.\n" +
                    "   Рассматривает роль общения в развитии личности и навыков разрешения конфликтов у детей.\n\n";

            text += "2. Пилипко Н.В.\n" +
                    "   Приглашение в мир общения. Ч. 1, 2. М., 1999, 2001.\n" +
                    "   Пособие по развитию коммуникативной компетентности у детей.\n\n";

            text += "3. Смирнова Е.О.\n" +
                    "   Межличностные отношения дошкольников: диагностика, проблемы, коррекция. М., 2005.\n" +
                    "   Исследует особенности межличностных отношений и конфликтов у дошкольников.\n\n";

            text += "4. Хухлаева О.В.\n" +
                    "   Тропинка к своему Я: уроки психологии в начальной школе\n(1–4). М., 2009.\n" +
                    "   Пособие по развитию самопознания и эмоционального интеллекта у младших школьников.\n\n";

            text += "📥 Хотите скачать какую-нибудь книгу? Выберите её номер ниже.";

            await botClient.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Markdown,
                replyMarkup: inlineKeyboard,
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