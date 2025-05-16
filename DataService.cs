using System;
using System.Collections.Generic;
using System.Linq;

namespace ConflictResolutionBot
{
    public class DataService
    {
        // Literature database
        public static readonly List<LiteratureItem> Literature = new List<LiteratureItem>
        {
            new LiteratureItem
            {
                Title = "Рабочая программа по внеурочной деятельности курса «Психология» для 2 класса",
                Authors = "Герасимова Н.И.",
                Year = 2016,
                Description = "Курс «Психология» - это комплекс психологических занятий в начальной школе направленных на формирование и сохранение психологического здоровья младших школьников.",
                Category = "Методическая",
                Keywords = new[] { "сотрудничество", "упражнения", "дети" }
            },
            new LiteratureItem
            {
                Title = "Конфликтология\nМетодические указания",
                Authors = "Титова Л.Г.",
                Year = 2009,
                Description = "В методических указаниях рассмотрены теоретико-методические основы изучения конфликтов.",
                Category = "Методическая",
                Keywords = new[] { "сотрудничество", "упражнения", "дети" }
            },
            new LiteratureItem
            {
                Title = "Детская конфликтологическая компетентность: онтогенез и развитие, теория и практика",
                Authors = "С.А. Котова, В.И. Максим",
                Year = 2013,
                Description = "В своей статье авторы дают всесторонний анализ пробле мы конфликтологической компетентности младших школьников и предлагают авторскую программу по работе с ней.",
                Category = "Научная",
                Keywords = new[] { "сотрудничество", "упражнения", "дети" }
            },
            new LiteratureItem
            {
                Title = "Детская психология. М.",
                Authors = "Мухина В.С.",
                Year = 1985,
                Description = "Описывает этапы психологического развития детей, включая аспекты, связанные с конфликтами.",
                Category = "Научная",
                Keywords = new[] { "сотрудничество", "упражнения", "дети" }
            },
            new LiteratureItem
            {
                Title = "Психотехника конфликта и конфликтная компетентность.",
                Authors = "Хасан Б.И.",
                Year = 1996,
                Description = "Предлагает психотехнические подходы к развитию конфликтной компетентности.",
                Category = "Методическая",
                Keywords = new[] { "сотрудничество", "упражнения", "дети" }
            },
            new LiteratureItem
            {
                Title = "Психология девиантности. Дети. Общество. Закон. М.",
                Authors = "Реан А.А.",
                Year = 2016,
                Description = "Рассматривает девиантное поведение детей и подростков, включая конфликтные проявления.",
                Category = "Научная",
                Keywords = new[] { "сотрудничество", "упражнения", "дети" }
            },
            new LiteratureItem
            {
                Title = "Как научить детей сотрудничать? Психологические игры и упражнения. М.",
                Authors = "Фопель К.",
                Year = 1998,
                Description = "Предлагает упражнения для развития навыков сотрудничества и разрешения конфликтов.",
                Category = "Методическая",
                Keywords = new[] { "сотрудничество", "упражнения", "дети" }
            },
            new LiteratureItem
            {
                Title = "Психология конфликта.",
                Authors = "Гришина Н.В.",
                Year = 2008,
                Description = "Обобщает теоретические и практические аспекты конфликтов, включая их проявления в школьной среде.",
                Category = "Научная",
                Keywords = new[] { "подросток", "психология", "развитие" }
            },
            new LiteratureItem
            {
                Title = "Межличностные отношения дошкольников: диагностика, проблемы, коррекция. М.",
                Authors = "Смирнова Е.О., Холмогорова В.М.",
                Year = 2005,
                Description = "Исследует особенности межличностных отношений и конфликтов у дошкольников.",
                Category = "Научная",
                Keywords = new[] { "сотрудничество", "упражнения", "дети" }
            },
            new LiteratureItem
            {
                Title = "Психология: Учебник для студентов высших педагогических заведений. Т. 2. М.",
                Authors = "Немов Р.С.",
                Year = 2007,
                Description = "Содержит разделы, посвящённые межличностным отношениям и конфликтам в образовательной среде.",
                Category = "Научная",
                Keywords = new[] { "сотрудничество", "упражнения", "дети" }
            },
            new LiteratureItem
            {
                Title = "Психологические игры для детей. М.",
                Authors = "Образцова Т.Н.",
                Year = 2008,
                Description = "Сборник игр, направленных на развитие коммуникативных навыков и снижение конфликтности.",
                Category = "Методическая",
                Keywords = new[] { "сотрудничество", "упражнения", "дети" }
            },
            new LiteratureItem
            {
                Title = "Приглашение в мир общения. Ч. 1, 2. М.",
                Authors = "Пилипко Н.В.",
                Year = 1999,
                Description = "Пособие по развитию коммуникативной компетентности у детей.",
                Category = "Методическая",
                Keywords = new[] { "конфликт", "теория", "анализ" }
            },
            new LiteratureItem
            {
                Title = "Акмеология",
                Authors = "А.А.Деркач",
                Year = 2004,
                Description = "Интегрируя и обобщая знания о прогрессивном развитии зрелой личности, путях самореализации и самоактуализации, акмеология оказалась концептуальным звеном в системе наук о человеке.",
                Category = "Методическая",
                Keywords = new[] { "сотрудничество", "упражнения", "дети" }
            },
            new LiteratureItem
            {
                Title = "Социальная конфликтология",
                Authors = "С.В. Соколов",
                Year = 2001,
                Description = "Учебное пособие «Социальная конфликтология» дает представление о природе, структуре, развитии, классификации социальных конфликтов.",
                Category = "Методическая",
                Keywords = new[] { "сотрудничество", "упражнения", "дети" }
            },
            new LiteratureItem
            {
                Title = "Социальная психология детства. М.",
                Authors = "Абраменкова В.В.",
                Year = 2008,
                Description = "Рассматривает особенности социального развития детей, включая формирование навыков взаимодействия и разрешения конфликтов.",
                Category = "Научная",
                Keywords = new[] { "конфликт", "теория", "анализ" }
            },
            new LiteratureItem
            {
                Title = "Конфликтология: искусство спора, ведения переговоров, разрешения конфликтов. М.",
                Authors = "Андреев В.И.",
                Year = 1995,
                Description = "Предлагает методики и подходы к обучению навыкам конструктивного разрешения конфликтов.",
                Category = "Научная",
                Keywords = new[] { "психология", "конфликт", "анализ" }
            },
            new LiteratureItem
            {
                Title = "Психологическое отчуждение личности и преступное поведение.",
                Authors = "Антонян Ю.М.",
                Year = 1987,
                Description = "Анализирует причины девиантного поведения, включая конфликтные ситуации в подростковой среде.",
                Category = "Методическая",
                Keywords = new[] { "практика", "разрешение", "конфликт" }
            },
            new LiteratureItem
            {
                Title = "Детский тест «Рисуночной фрустрации» С. Розенцвейга: Практическое руководство. М.",
                Authors = "Данилова Е.Е.",
                Year = 1992,
                Description = "Описывает методику диагностики фрустрационных реакций у детей, что важно для понимания их конфликтного поведения.",
                Category = "Научная",
                Keywords = new[] { "подросток", "возраст", "психология" }
            },
            new LiteratureItem
            {
                Title = "Конфликтная компетентность младших школьников // Герценовские чтения. Начальное образование. ",
                Authors = "Котова С.А., Костикова В.И.",
                Year = 2011,
                Description = "Исследуют особенности формирования конфликтной компетентности у младших школьников.",
                Category = "Методическая",
                Keywords = new[] { "сотрудничество", "упражнения", "дети" }
            },
        };

        // Methodologies database
        private static readonly List<MethodologyItem> Methodologies = new List<MethodologyItem>
        {
            new MethodologyItem
            {
                Title = "Конфликт — это точка зрения",
                Type = "Упражнение",
                Description = "Упражнение на рассмотрение конфликтной ситуации с разных позиций. Участники анализируют ситуацию с точки зрения всех вовлеченных сторон.",
                AgeGroup = "Подростки",
                Keywords = new[] { "точка зрения", "анализ", "позиция" }
            },
            new MethodologyItem
            {
                Title = "Мост вместо стены",
                Type = "Упражнение",
                Description = "Упражнение на развитие эмпатии и понимания чувств других людей. Участники учатся строить \"мосты\" понимания вместо \"стен\" отчуждения.",
                AgeGroup = "Все возрасты",
                Keywords = new[] { "эмпатия", "понимание", "коммуникация" }
            },
            new MethodologyItem
            {
                Title = "Медиация",
                Type = "Ролевая игра",
                Description = "Моделирование процесса медиации, где участники по очереди выступают в роли конфликтующих сторон и медиатора.",
                AgeGroup = "Подростки, старшие школьники",
                Keywords = new[] { "медиация", "посредничество", "разрешение" }
            },
            new MethodologyItem
            {
                Title = "Я-высказывания",
                Type = "Тренинг",
                Description = "Обучение конструктивным способам выражения претензий и недовольства через формулу \"Я-высказывания\".",
                AgeGroup = "Все возрасты",
                Keywords = new[] { "коммуникация", "выражение", "чувства" }
            },
            new MethodologyItem
            {
                Title = "Волшебные очки",
                Type = "Упражнение",
                Description = "Упражнение на развитие эмпатии и умения видеть хорошее в других людях.",
                AgeGroup = "Младшие школьники",
                Keywords = new[] { "эмпатия", "позитив", "восприятие" }
            },
            new MethodologyItem
            {
                Title = "Острова",
                Type = "Игра",
                Description = "Игра на развитие навыков сотрудничества и поиска компромисса.",
                AgeGroup = "Все возрасты",
                Keywords = new[] { "сотрудничество", "компромисс", "взаимодействие" }
            }
        };

        // FAQs database
        private static readonly List<FaqItem> Faqs = new List<FaqItem>
        {
            new FaqItem
            {
                Question = "Что такое конфликтологическая компетентность?",
                Answer = "Конфликтологическая компетентность — это способность человека эффективно взаимодействовать в конфликтных ситуациях, предупреждать, управлять и конструктивно разрешать конфликты. Она включает когнитивный, эмоциональный, поведенческий, мотивационный и ценностный компоненты.",
                Keywords = new[] { "компетентность", "определение", "конфликт" }
            },
            new FaqItem
            {
                Question = "Как определить уровень конфликтности у подростка?",
                Answer = "Для определения уровня конфликтности у подростка можно использовать следующие методики:\n1. Тест К. Томаса на стратегии поведения в конфликте (адаптированный для подростков)\n2. Методика \"Личностная агрессивность и конфликтность\" (Е.П. Ильин, П.А. Ковалев)\n3. Опросник \"Диагностика уровня конфликтности личности\"\n4. Наблюдение за поведением подростка в различных ситуациях\n5. Социометрия для выявления конфликтных отношений в группе",
                Keywords = new[] { "диагностика", "уровень", "конфликтность" }
            },
            new FaqItem
            {
                Question = "Какие упражнения и тренинги использовать для развития конфликтологической компетентности?",
                Answer = "Рекомендуемые упражнения и тренинги:\n1. \"Конфликт — это точка зрения\" - анализ ситуаций с разных позиций\n2. \"Мост вместо стены\" - развитие эмпатии\n3. Ролевая игра \"Медиация\" - освоение навыков посредничества\n4. Тренинг \"Я-высказывания\" - конструктивное выражение чувств\n5. \"Острова\" - развитие навыков сотрудничества\n6. \"Конфликтные ситуации\" - анализ и поиск решений\n7. \"Четыре угла\" - осознание стратегий поведения в конфликте",
                Keywords = new[] { "упражнения", "тренинги", "развитие" }
            },
            new FaqItem
            {
                Question = "Что делать с постоянными конфликтами в классе?",
                Answer = "Для работы с постоянными конфликтами в классе рекомендуется:\n1. Провести диагностику социально-психологического климата в классе\n2. Выявить основные причины конфликтов\n3. Организовать тренинги по развитию коммуникативных навыков\n4. Внедрить систему школьной медиации\n5. Проводить регулярные классные часы по теме конструктивного взаимодействия\n6. Работать индивидуально с наиболее конфликтными учениками\n7. Привлечь родителей к решению проблемы\n8. При необходимости обратиться к школьному психологу или внешним специалистам",
                Keywords = new[] { "класс", "постоянные", "конфликты" }
            }
        };

        // Methods to search and retrieve data
        public static List<LiteratureItem> SearchLiterature(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Literature;

            return Literature.Where(item =>
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Authors.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Keywords.Any(k => k.Contains(query, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        public static List<MethodologyItem> SearchMethodologies(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Methodologies;

            return Methodologies.Where(item =>
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Type.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.AgeGroup.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Keywords.Any(k => k.Contains(query, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        public static List<FaqItem> SearchFaqs(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Faqs;

            return Faqs.Where(item =>
                item.Question.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Answer.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Keywords.Any(k => k.Contains(query, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }
    }

    // Data models
    public class LiteratureItem
    {
        public string Title { get; set; } = "";
        public string Authors { get; set; } = "";
        public int Year { get; set; }
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public string[] Keywords { get; set; } = Array.Empty<string>();
    }

    public class MethodologyItem
    {
        public string Title { get; set; } = "";
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public string AgeGroup { get; set; } = "";
        public string[] Keywords { get; set; } = Array.Empty<string>();
    }

    public class FaqItem
    {
        public string Question { get; set; } = "";
        public string Answer { get; set; } = "";
        public string[] Keywords { get; set; } = Array.Empty<string>();
    }
}
