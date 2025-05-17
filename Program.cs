using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConflictResolutionBot
{
    class Program
    {
        private static TelegramBotClient? botClient;
        private static readonly ConcurrentDictionary<long, bool> _awaitingQuery = new ConcurrentDictionary<long, bool>();
        private static FileSearchService _fileSearchService = new FileSearchService("C:/Users/79025/source/repos/2 курс/3 семестр/School_mediator_bot/Literature");
        private static HttpListener? _listener;
        private static string _url = "http://localhost:8443/";
        private static bool _isRunning = true;
        private static ManualResetEvent _exitEvent = new ManualResetEvent(false);
        static async Task Main(string[] args)
        {
            try
            {
                string botToken = Environment.GetEnvironmentVariable("BOT_TOKEN") ?? "YOUR_BOT_TOKEN";
                botClient = new TelegramBotClient(botToken);

                // IP вашего сервера и порт для вебхуков
                string serverIp = "82.147.71.182";
                int port = 8443;
                string webhookUrl = $"https://{serverIp}:{port}/bot";

                // Настраиваем локальный сервер
                _url = $"http://localhost:{port}/";
                _listener = new HttpListener();
                _listener.Prefixes.Add(_url);

                // Удаляем старый вебхук
                await botClient.DeleteWebhook();

                // Путь к сертификату
                string certPath = "/root/certs/cert.pem";

                // Устанавливаем новый вебхук с сертификатом
                if (File.Exists(certPath))
                {
                    using (var certStream = File.OpenRead(certPath))
                    {
                        await botClient.SetWebhook(
                            url: webhookUrl,
                            certificate: new InputFileStream(certStream, "cert.pem")
                        );
                    }
                    Console.WriteLine($"Вебхук установлен на {webhookUrl} с сертификатом");
                }
                else
                {
                    await botClient.SetWebhook(webhookUrl);
                    Console.WriteLine($"Вебхук установлен на {webhookUrl} без сертификата");
                }

                // Проверяем информацию о вебхуке
                var webhookInfo = await botClient.GetWebhookInfo();
                Console.WriteLine($"Webhook URL: {webhookInfo.Url}");
                Console.WriteLine($"Webhook has certificate: {webhookInfo.HasCustomCertificate}");
                if (webhookInfo.LastErrorDate != null)
                {
                    Console.WriteLine($"Last error: {webhookInfo.LastErrorDate} - {webhookInfo.LastErrorMessage}");
                }

                var me = await botClient.GetMe();
                Console.WriteLine($"Start listening for @{me.Username}");

                // Запускаем сервер
                _listener.Start();
                Console.WriteLine($"Сервер запущен на {_url}");

                // Обработка сигналов завершения
                Console.CancelKeyPress += (sender, e) => {
                    e.Cancel = true;
                    _isRunning = false;
                    _exitEvent.Set();
                };

                // Запускаем обработку запросов в отдельном потоке
                Task.Run(async () => {
                    while (_isRunning)
                    {
                        try
                        {
                            var context = await _listener.GetContextAsync();
                            _ = ProcessRequestAsync(context);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка при обработке запроса: {ex.Message}");
                            File.AppendAllText("/var/log/mybot-error.log",
                                $"{DateTime.Now}: {ex.Message}\n{ex.StackTrace}\n\n");
                        }
                    }
                });
                // Блокируем завершение программы
                _exitEvent.WaitOne();

                // Код очистки при завершении
                _listener.Stop();
                await botClient.DeleteWebhook();
                Console.WriteLine("Bot stopped");
            }
            catch (Exception ex)
            {
                // Логируем ошибку в файл
                File.AppendAllText("/var/log/mybot-error.log",
                    $"{DateTime.Now}: {ex.Message}\n{ex.StackTrace}\n\n");
                throw; // Перебрасываем исключение для systemd
            }
        }
        private static async Task StartWebhookServer()
        {
            // Создаем HTTP-сервер
            _listener = new HttpListener();
            _listener.Prefixes.Add(_url);
            _listener.Start();
            Console.WriteLine($"Сервер запущен на {_url}");
            Console.WriteLine("Нажмите Ctrl+C для остановки");

            // Обработка Ctrl+C для корректного завершения
            Console.CancelKeyPress += (sender, e) => {
                e.Cancel = true;
                _isRunning = false;
                Console.WriteLine("Останавливаем сервер...");
            };

            // Обрабатываем входящие запросы
            while (_isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при обработке запроса: {ex.Message}");
                }
            }

            // Останавливаем сервер и удаляем вебхук
            _listener.Stop();
            await botClient.DeleteWebhook();
            Console.WriteLine("Сервер остановлен");
        }

        private static async Task ProcessRequestAsync(HttpListenerContext context)
        {
            try
            {
                // Проверяем, что это запрос к нашему боту
                if (context.Request.Url.AbsolutePath == "/bot")
                {
                    // Читаем тело запроса
                    string requestBody;
                    using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                    {
                        requestBody = await reader.ReadToEndAsync();
                    }

                    // Десериализуем обновление
                    var update = JsonConvert.DeserializeObject<Update>(requestBody);
                    if (update != null)
                    {
                        // Обрабатываем обновление асинхронно
                        _ = Task.Run(() => HandleUpdateAsync(update));
                    }

                    // Отправляем ответ
                    context.Response.StatusCode = 200;
                    byte[] buffer = Encoding.UTF8.GetBytes("OK");
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    // Для других путей отправляем 404
                    context.Response.StatusCode = 404;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при обработке запроса: {ex.Message}");
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.Close();
            }
        }

        private static async Task HandleUpdateAsync(Update update)
        {
            try
            {
                // Only process Message updates
                if (update.Message is not { } message)
                    return;

                // Only process text messages
                if (message.Text is not { } messageText)
                    return;

                var chatId = message.Chat.Id;
                if (_awaitingQuery.TryGetValue(chatId, out bool isWaiting) && isWaiting)
                {
                    _awaitingQuery.TryRemove(chatId, out _);
                    await ProcessSearchQuery(botClient, chatId, messageText, CancellationToken.None);
                    return;
                }
                Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");
                if (message.ReplyToMessage?.Text?.Contains("Введите поисковый запрос") == true)
                {
                    string searchQuery = message.Text.StartsWith("/find ")
                        ? message.Text.Substring(6)
                        : message.Text;

                    await SearchInfoAsync(botClient, chatId, searchQuery, CancellationToken.None);
                    return;
                }
                // Handle commands
                if (messageText.StartsWith("/"))
                {
                    await HandleCommandAsync(botClient, message, CancellationToken.None);
                    return;
                }

                // Handle regular messages or keywords
                if (messageText.Contains("конфликт", StringComparison.OrdinalIgnoreCase))
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "Я заметил, что вы интересуетесь темой конфликтов. Могу предложить вам изучить раздел /методики для практических упражнений или /литература для теоретических материалов.");
                    return;
                }

                // Default response for unrecognized messages
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Не совсем понимаю ваш запрос. Пожалуйста, воспользуйтесь командами из меню или напишите /start для получения списка доступных команд.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при обработке обновления: {ex.Message}");
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
                text: "Составителем данного бота является студентка РГПУ им. А.И. Герцена института психологии направления: «Развитие личностного потенциала» - Хромова Анастасия Германовна.\n_________________________________________________________\n\n" +
                      "Здравствуйте! 👋\n" +
                      "Я — ваш помощник по вопросам конфликтологической компетентности современных школьников.\n" +
                      "Чем могу помочь?\n\n" +
                      "📚 /literature – список рекомендуемой литературы\n" +
                      "🧠 /concept – Психология подростка\n" +
                      "🛠 /methods – практические приёмы и упражнения\n" +
                      "❓ /question – задать свой вопрос\n" +
                      "🔎 /find – найти информацию по теме\n",
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
                    InlineKeyboardButton.WithUrl("Скачать методичку", "https://example.com/conflict_methods.pdf")
                }
            });

            await botClient.SendMessage(
                chatId: chatId,
                text: "🛠 *Методики и упражнения для развития конфликтологической компетентности*\n\n" +
                      "*Эффективные методики:*\n\n" +
                      "🔸 *«Конфликт — это точка зрения»*\n" +
                      "Упражнение на рассмотрение конфликтной ситуации с разных позиций. Участники анализируют ситуацию с точки зрения всех вовлеченных сторон.\n\n" +
                      "🔸 *«Мост вместо стены»*\n" +
                      "Упражнение на развитие эмпатии и понимания чувств других людей. Участники учатся строить \"мосты\" понимания вместо \"стен\" отчуждения.\n\n" +
                      "🔸 *Ролевая игра «Медиация»*\n" +
                      "Моделирование процесса медиации, где участники по очереди выступают в роли конфликтующих сторон и медиатора.\n\n" +
                      "🔸 *Тренинг «Я-высказывания»*\n" +
                      "Обучение конструктивным способам выражения претензий и недовольства через формулу \"Я-высказывания\".\n\n" +
                      "📥 Хотите получить полную методичку с упражнениями? Нажмите на кнопку \"Скачать методичку\" ниже.",
                parseMode: ParseMode.Markdown,
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
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
                text: "🎓 *Особенности работы с младшими школьниками*\n\n" +
                      "*Психологические особенности младших школьников:*\n" +
                      "• Высокая эмоциональность и импульсивность\n" +
                      "• Недостаточно развитая саморегуляция\n" +
                      "• Конкретное мышление\n" +
                      "• Авторитет взрослого (особенно учителя)\n" +
                      "• Потребность в одобрении\n\n" +
                      "*Рекомендуемые методики:*\n" +
                      "1. *Сказкотерапия* - использование сказочных сюжетов для обсуждения конфликтных ситуаций\n" +
                      "2. *Игры-драматизации* - проигрывание конфликтных ситуаций с последующим обсуждением\n" +
                      "3. *«Волшебные очки»* - упражнение на развитие эмпатии и умения видеть хорошее в других\n" +
                      "4. *«Мирилки»* - разучивание стихотворных формул примирения\n\n" +
                      "*Литература:*\n" +
                      "• Фопель К. \"Как научить детей сотрудничать\"\n" +
                      "• Кривцова С.В. \"Жизненные навыки. Уроки психологии в начальной школе\"\n" +
                      "• Хухлаева О.В. \"Тропинка к своему Я: уроки психологии в начальной школе\"\n\n" +
                      "Для получения конкретных упражнений используйте команду /methods",
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }
    }
}