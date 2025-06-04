using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
    public class QuestionContext
    {
        public long UserId { get; set; }
        public int UserMessageId { get; set; }
        public int AdminMessageId { get; set; }
    }

    class Program
    {
        private static TelegramBotClient? botClient;
        private static CancellationTokenSource cts = new CancellationTokenSource();
        private static readonly ConcurrentDictionary<int, QuestionContext> _activeQuestions = new();
        private static readonly long AdminChatId = long.Parse(Environment.GetEnvironmentVariable("ADMIN_CHAT_ID") ?? "796409454");
        private static readonly ConcurrentDictionary<long, long> _awaitingAdminReply = new();
        private static readonly ConcurrentDictionary<long, bool> _awaitingQuestion = new();
        private static readonly ConcurrentDictionary<long, bool> _awaitingQuery = new();
        private static long _adminChatId = long.Parse(Environment.GetEnvironmentVariable("ADMIN_CHAT_ID") ?? "796409454");
        private static FileSearchService _fileSearchService = new FileSearchService("/root/mediator/Literature");

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
            if (update.CallbackQuery is CallbackQuery callbackQuery)
            {
                if (callbackQuery.Data?.StartsWith("reply_") == true)
                {
                    await HandleQuickReplyCallback(botClient, callbackQuery);
                    return;
                }
                await HandleCallbackQueryAsync(botClient, callbackQuery);
                return;
            }

            if (update.Message is { } adminMessage && adminMessage.Chat.Id == _adminChatId)
            {
                if (_awaitingAdminReply.TryGetValue(adminMessage.From?.Id ?? 0, out long targetUserId))
                {
                    _awaitingAdminReply.TryRemove(adminMessage.From?.Id ?? 0, out _);

                    await botClient.SendMessage(
                        chatId: targetUserId,
                        text: $"📨 *Ответ от администратора:*\n\n{adminMessage.Text}",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: cancellationToken);

                    await botClient.SendMessage(
                        chatId: _adminChatId,
                        text: $"✅ Ответ отправлен пользователю",
                        replyParameters: new ReplyParameters { MessageId = adminMessage.MessageId },
                        cancellationToken: cancellationToken);

                    return;
                }
                // Ответ администратора на пересланное сообщение
                if (adminMessage.ReplyToMessage is { } repliedMessage)
                {
                    await ProcessAdminReply(botClient, adminMessage, repliedMessage);
                    return;
                }
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
                    text: "Я заметил, что вы интересуетесь темой конфликтов. Могу предложить вам изучить раздел /methods для практических упражнений или /literature для теоретических материалов.",
                    cancellationToken: cancellationToken);
                return;
            }

            // Default response for unrecognized messages
            await botClient.SendMessage(
                chatId: chatId,
                text: "Не совсем понимаю ваш запрос. Пожалуйста, воспользуйтесь командами из меню или напишите /start для получения списка доступных команд.",
                cancellationToken: cancellationToken);
        }

        private static async Task ProcessAdminReply(ITelegramBotClient botClient, Message adminMessage, Message repliedMessage)
        {
            // Ищем контекст вопроса
            if (!_activeQuestions.TryGetValue(repliedMessage.MessageId, out var context))
                return;

            try
            {
                // Отправляем ответ пользователю
                await botClient.SendMessage(
                    chatId: context.UserId,
                    text: $"📨 *Ответ от администратора:*\n\n{adminMessage.Text}",
                    parseMode: ParseMode.Markdown,
                    replyParameters: new ReplyParameters { MessageId = context.UserMessageId });

                // Подтверждение администратору
                await botClient.SendMessage(
                    chatId: _adminChatId,
                    text: $"✅ Ответ отправлен пользователю",
                    replyParameters: new ReplyParameters { MessageId = adminMessage.MessageId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending reply: {ex}");
                await botClient.SendMessage(
                    chatId: _adminChatId,
                    text: $"❌ Ошибка: {ex.Message}");
            }
        }

        private static async Task HandleQuickReplyCallback(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            var parts = callbackQuery.Data?.Split('_');
            if (parts?.Length < 2 || !long.TryParse(parts[1], out long userId))
                return;

            // Устанавливаем состояние ожидания ответа
            _awaitingAdminReply[callbackQuery.From.Id] = userId;

            await botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "Введите ответ для пользователя:");

            await botClient.SendMessage(
                chatId: _adminChatId,
                text: $"✍️ Введите ответ для пользователя:",
                replyParameters: new ReplyParameters { MessageId = callbackQuery.Message?.MessageId ?? 0 });
        }

        private static async Task ForwardQuestionToAdmin(ITelegramBotClient botClient, Message message)
        {
            if (_adminChatId == 0)
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
                var adminMessage = await botClient.SendMessage(
                    chatId: AdminChatId,
                    text: userInfo);

                // Пересылаем оригинальное сообщение
                var forwardedMessage = await botClient.ForwardMessage(
                    chatId: AdminChatId,
                    fromChatId: message.Chat.Id,
                    messageId: message.MessageId);

                var context = new QuestionContext
                {
                    UserId = message.Chat.Id,
                    UserMessageId = message.MessageId,
                    AdminMessageId = forwardedMessage.MessageId
                };
                Console.WriteLine($"Question forwarded from {message.Chat.Id}");
                _activeQuestions.TryAdd(adminMessage.MessageId, context);

                // Добавляем кнопку для быстрого ответа
                var replyMarkup = new InlineKeyboardMarkup(new[]
                {
                    InlineKeyboardButton.WithCallbackData("📝 Ответить", $"reply_{context.UserId}")
                });

                await botClient.EditMessageReplyMarkup(
                    chatId: _adminChatId,
                    messageId: adminMessage.MessageId,
                    replyMarkup: replyMarkup);
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

            string text = "📚 *Рекомендуемая литература по конфликтологии (обновлено май 2025)*\n\n";
            text += "1. Абраменкова В.В.\n" +
                    "   Социальная психология детства. М., 2024.\n" +
                    "   Рассматривает особенности социального развития детей, включая формирование навыков взаимодействия и разрешения конфликтов.\n\n";
            text += "2. Немов Р.С.\n" +
                    "   Психология: Учебник для студентов высших педагогических заведений. Т. 2. М., 2024.\n" +
                    "   Содержит разделы, посвящённые межличностным отношениям и конфликтам в образовательной среде.\n\n";
            text += "3. Хасан Б.И.\n" +
                    "   Психотехника конфликта и конфликтная компетентность. Красноярск, 2023.\n" +
                    "   Предлагает психотехнические подходы к развитию конфликтной компетентности.\n\n";
            text += "4. Соколов С. В.\n" +
                    "   Социальная конфликтология. Москва, 2024.\n" +
                    "   Рассматриваются природа и классификация социальных конфликтов.\n\n";
            text += "5. Реан А. А.\n" +
                    "   ПСИХОЛОГИЯ ДЕВИАНТНОСТИ. Дети, Общество, Закон. Москва, 2024.\n" +
                    "   Книга дает развернутую психологическую характеристику отклоняющегося поведения, обращается к различным формам его проявления.\n\n";
            text += "6. Деркач А. А.\n" +
                    "   Акмеология. Москва, 2025.\n" +
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

            string text = "📚 *Рекомендуемая литература по конфликтной компетентности подростков (май 2025)*\n\n";
            text += "1. Гришина Н.В.\n" +
                    "   Психология конфликта. СПб, 2024.\n" +
                    "   Обобщает теоретические и практические аспекты конфликтов, включая их проявления в школьной среде.\n\n";

            text += "2. Анцупов А. Я., Баклановский С. В.\n" +
                    "   Конфликтология в схемах и комментариях. СПб, 2024.\n" +
                    "   Учебное пособие, в котором отражены результаты применения системного подхода к исследованию конфликтов.\n\n";

            text += "3. Хван А.А., Зайцев Ю.А., Кузнецова  Ю.А.\n" +
                    "   Стандартизированный опросник измерения агрессивных и враждебных реакций А.Басса и А.Дарки. М., 2024.\n" +
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
                text: "🛠 *Упражнения и тренинги для развития конфликтологической компетентности (май 2025)*\n\n" +
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
                      "6. 🔸 *«Психологические игры для детей»*\n" +
                      "Цель: показать разнообразные игры, способствующие правильному разностороннему психологическому развитию детей.\n\n" +
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
                        filePath = "/root/mediator/Literature/Психология - yчебник.pdf";
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
                        return;
                }

                // Проверяем существует ли файл
                if (!System.IO.File.Exists(filePath))
                {
                    await botClient.SendMessage(e.Message?.Chat.Id ?? 0, "Файл не найден.");
                    return;
                }

                // Отправляем файл
                await using (var stream = System.IO.File.OpenRead(filePath))
                {
                    await botClient.SendDocument(
                        chatId: e.Message?.Chat.Id ?? 0,
                        document: InputFile.FromStream(stream, Path.GetFileName(filePath)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling callback query: {ex.Message}");
                await botClient.SendMessage(e.Message?.Chat.Id ?? 0, "Произошла ошибка при отправке файла.");
            }
        }

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

            await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);
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

            var response = new StringBuilder("🔍 Вот книги, которые вам подойдут:");
            await botClient.SendMessage(
                        chatId: chatId,
                        text: response.ToString(),
                        parseMode: ParseMode.Markdown,
                        cancellationToken: cancellationToken);
            foreach (var result in results)
            {
                string filePath = "/root/mediator/Literature/" + result.FileName;
                await using (var stream = System.IO.File.OpenRead(filePath))
                {
                    await botClient.SendDocument(
                        chatId: chatId,
                        document: InputFile.FromStream(stream, Path.GetFileName(filePath)),
                        cancellationToken: cancellationToken);
                }
            }
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

            string text = "📚 *Рекомендуемая литература по конфликтной компетентности младших школьников (май 2025)*\n\n";
            text += "1. Лисина М.М.\n" +
                    "   Формирование личности ребенка в общении. СПб, 2024.\n" +
                    "   Рассматривает роль общения в развитии личности и навыков разрешения конфликтов у детей.\n\n";

            text += "2. Пилипко Н.В.\n" +
                    "   Приглашение в мир общения. Ч. 1, 2. М., 2024.\n" +
                    "   Пособие по развитию коммуникативной компетентности у детей.\n\n";

            text += "3. Смирнова Е.О.\n" +
                    "   Межличностные отношения дошкольников: диагностика, проблемы, коррекция. М., 2024.\n" +
                    "   Исследует особенности межличностных отношений и конфликтов у дошкольников.\n\n";

            text += "4. Хухлаева О.В.\n" +
                    "   Тропинка к своему Я: уроки психологии в начальной школе\n(1–4). М., 2025.\n" +
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

    

    public class SearchResult
    {
        public string FileName { get; set; } = "";
        public string Content { get; set; } = "";
    }
}