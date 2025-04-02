using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using NUnit.Framework;
using OpenQA.Selenium.Support.UI;
using System;
using DotNetEnv;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Font;
using iText.IO.Font;
using System;
using System.IO;
using iText.Layout.Borders;
using iText.Layout.Properties;
using iText.Kernel.Pdf.Canvas.Draw;
using System;
using System.IO;
using iText.Kernel.Pdf.Action;

namespace Parser
{
    public class SiteParser
    {
        private ChromeDriver driver;
        private string? login = string.Empty;
        private string? password = string.Empty;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Env.Load("/home/max/learn/AI/parser/project/SiteParser/.env");

            login = Env.GetString("LOGIN_EMAIL");
            password = Env.GetString("LOGIN_PASSWORD");

            if (login == null || password == null){
                throw new InvalidOperationException("Логин и пароль не загружены");
            }

            var options = new ChromeOptions();
            options.AddArgument("--blink-settings=imagesEnabled=false");
            driver = new ChromeDriver(options);
            driver.Url = "https://lk.neural-university.ru/";
        }

        [OneTimeTearDown]
        public void OneTearDown()
        {
            driver.Quit();
            driver.Dispose();
        }

        [Test]
        public void Test1()
        {
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
            wait.PollingInterval = TimeSpan.FromMilliseconds(500);
            
             // Ожидание появления поля ввода логина
            wait.Until(driver => driver.FindElement(By.Id("login_email")).Displayed);

            IWebElement loginField = driver.FindElement(By.Id("login_email"));
            loginField.SendKeys(login);

            IWebElement passwordField = driver.FindElement(By.Id("login_password"));
            passwordField.SendKeys(password);

            IWebElement loginButton = driver.FindElement(By.XPath("//button[text()='Вход']"));
            loginButton.Click();

            // Ожидаем, пока не будет загружен профиль пользователя
            wait.Until(d => d.Url.Contains("/profile/"));

            // Переходим на страницу с курсами
            driver.Navigate().GoToUrl("https://lk.neural-university.ru/learning-program-v2/learn");
            wait.Until(d => d.Url.Contains("learning-program-v2/learn"));

            // Получаем все ссылки на курсы
            var courseLinks = driver.FindElements(By.CssSelector("section .accordionItem .title-h2 a"));
            foreach (var link in courseLinks)
            {
                string courseUrl = link.GetAttribute("href");
                Console.WriteLine($"Переход к курсу: {courseUrl}");
                driver.Navigate().GoToUrl(courseUrl);

                // Получаем все строки уроков
                var lessonRows = driver.FindElements(By.CssSelector("tr"));
                foreach (var row in lessonRows)
                {
                    var statusElements = row.FindElements(By.CssSelector("td.text-center .status._gray._normal"));
                    if (statusElements.Count > 0)
                    {
                        Console.WriteLine("Урок закрыт, пропускаем.");
                        continue;
                    }

                    // Получаем ссылку на урок
                    var linkElement = row.FindElement(By.CssSelector("td a.link-hover"));
                    string lessonUrl = linkElement.GetAttribute("href");
                    Console.WriteLine($"Нашел урок: {lessonUrl}");
                    driver.Navigate().GoToUrl(lessonUrl);
                    
                    // Парсим описание урока и домашку
                    string lessonDescription = ParseLessonDescription();
                    string liteDescription, proDescription, liteLink, proLink;
                    ParseHomework(out liteDescription, out proDescription, out liteLink, out proLink);
                    // Извлечь название урока
                    var lessonTitleElement = driver.FindElement(By.CssSelector("h1.title-h1"));
                    string lessonTitle = lessonTitleElement.Text;

                    // Извлечь категорию
                    var categoryElement = driver.FindElement(By.CssSelector("div.breadcrumbs .breadcrumbs__item span[itemprop='name']"));
                    string categoryTitle = categoryElement.Text;
                    

                    // Сохраняем урок в PDF
                    SaveLessonAsPdf(lessonUrl, lessonDescription, liteDescription, proDescription, liteLink, proLink, lessonTitle, categoryTitle);
                    return; 

                    // Возвращаемся на предыдущую страницу
                    driver.Navigate().Back();
                }

                // Возвращаемся на страницу с курсами
                driver.Navigate().Back();
            }
        }


        private string ParseLessonDescription()
        {
            try
            {
                var descriptionElement = driver.FindElement(By.CssSelector("div.subsection .mb_30"));
                return descriptionElement.Text;
            }
            catch (NoSuchElementException)
            {
                Console.WriteLine("Описание урока не найдено.");
                return string.Empty;
            }
        }

        private void ParseHomework(out string liteDescription, out string proDescription, out string liteLink, out string proLink)
        {
            liteDescription = "Не найдено";
            liteLink = "Не найдено";
            proDescription = "Не найдено";
            proLink = "Не найдено";

            try
            {
                // Парсим Lite задание
                var liteTab = driver.FindElement(By.CssSelector("div.tabContent[data-tab='2']"));
                if (liteTab.GetAttribute("class").Contains("hidden"))
                {
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].classList.remove('hidden');", liteTab);
                }
                liteDescription = liteTab.FindElement(By.XPath(".//div[contains(@class, 'subsection') and contains(., 'Задание Lite')]")).Text.Trim();
                var liteLinkElement = liteTab.FindElement(By.XPath(".//p/a[contains(@href, 'docs.google.com')]"));
                liteLink = liteLinkElement.GetAttribute("href");

                // Парсим Pro задание
                var proTab = driver.FindElement(By.CssSelector("div.tabContent[data-tab='3']"));
                if (proTab.GetAttribute("class").Contains("hidden"))
                {
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].classList.remove('hidden');", proTab);
                }
                proDescription = proTab.FindElement(By.XPath(".//div[contains(@class, 'subsection') and contains(., 'Задание Pro')]")).Text.Trim();
                var proLinkElement = proTab.FindElement(By.XPath(".//p/a[contains(@href, 'docs.google.com')]"));
                proLink = proLinkElement.GetAttribute("href");
            }
            catch (NoSuchElementException ex)
            {
                Console.WriteLine($"Ошибка при парсинге домашки: {ex.Message}");
            }
        }

        private void SaveLessonAsPdf(
            string lessonUrl, 
            string lessonDescription, 
            string liteDescription, 
            string proDescription,
            string liteLink, 
            string proLink, 
            string lessonTitle, 
            string categoryTitle
        ) {
            try
            {
                // Генерация корректного имени файла
                string fileName = $"{SanitizeFileName(lessonTitle)}_{SanitizeFileName(categoryTitle)}_{Guid.NewGuid()}.pdf";

                using (var writer = new PdfWriter(fileName))
                {
                    using var pdf = new PdfDocument(writer);
                    var document = new Document(pdf);

                    string fontPath = "/usr/share/fonts/truetype/msttcorefonts/arial.ttf";      // Обычный Arial
                    string fontBoldPath = "/usr/share/fonts/truetype/msttcorefonts/arialbd.ttf"; // Жирный Arial
                    string fontItalicPath = "/usr/share/fonts/truetype/msttcorefonts/ariali.ttf"; // Курсивный Arial

                    PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);
                    PdfFont boldFont = PdfFontFactory.CreateFont(fontBoldPath, PdfEncodings.IDENTITY_H);
                    PdfFont italicFont = PdfFontFactory.CreateFont(fontItalicPath, PdfEncodings.IDENTITY_H);

                    // Заголовок документа
                    document.Add(new Paragraph(lessonTitle)
                        .SetFont(boldFont)
                        .SetFontSize(18)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetMarginBottom(15));

                    document.Add(new Paragraph("Урок: ").SetFont(boldFont));
                    var lessonUrlLink = new Link(lessonUrl, PdfAction.CreateURI(lessonUrl));
                    document.Add(new Paragraph().Add(lessonUrlLink).SetFont(font));

                    // Описание урока
                    document.Add(new Paragraph("Описание урока:")
                        .SetFont(boldFont)
                        .SetFontSize(14)
                        .SetMarginBottom(5));

                    document.Add(new Paragraph(lessonDescription)
                        .SetFont(font)
                        .SetFontSize(12)
                        .SetMarginBottom(15));

                    // Разделительная линия
                    document.Add(new LineSeparator(new SolidLine()).SetMarginBottom(10));

                    // Домашнее задание
                    document.Add(new Paragraph("Домашнее задание:")
                        .SetFont(boldFont)
                        .SetFontSize(14)
                        .SetMarginBottom(5));

                    document.Add(new Paragraph(liteDescription)
                        .SetFont(font)
                        .SetFontSize(12)
                        .SetMarginBottom(15));

                    // Ссылки на задания
                    document.Add(new Paragraph("Ссылки на домашку:")
                        .SetFont(boldFont)
                        .SetFontSize(14)
                        .SetMarginBottom(5));

                     // Добавление кликабельных ссылок на домашку
                    var liteLinkParagraph = new Paragraph("Lite задание: ").Add(new Link(liteLink, PdfAction.CreateURI(liteLink))).SetFont(font);
                    document.Add(liteLinkParagraph);


                    document.Add(new Paragraph(proDescription)
                        .SetFont(font)
                        .SetFontSize(12)
                        .SetMarginBottom(15));

                    // Ссылки на задания
                    document.Add(new Paragraph("Ссылки на домашку:")
                        .SetFont(boldFont)
                        .SetFontSize(14)
                        .SetMarginBottom(5));
                    var proLinkParagraph = new Paragraph("Pro задание: ").Add(new Link(proLink, PdfAction.CreateURI(proLink))).SetFont(font);
                    document.Add(proLinkParagraph);

                    // Закрытие документа
                    document.Close();
                }

                // Проверка размера файла
                var fileInfo = new FileInfo(fileName);
                if (fileInfo.Length == 0)
                {
                    Console.WriteLine("PDF файл пустой!");
                }
                else
                {
                    Console.WriteLine($"PDF файл сохранен: {fileName}, размер: {fileInfo.Length} байт");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при сохранении PDF: {ex.Message}");
            }
        }


        private string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c.ToString(), "");
            }
            return fileName;
        }
    }
}
