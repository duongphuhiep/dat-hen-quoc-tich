using PuppeteerSharp;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ToolsPack.Config;
using ToolsPack.String;

namespace nghe
{
    internal class Program
    {
        private static readonly string NOT_AVAILABLE_MSG = ConfigReader.Read("NOT_AVAILABLE_MSG", "Il n'existe plus de plage horaire libre pour votre demande de rendez-vous. Veuillez recommencer ultérieurement.");
        private static readonly string SCREENSHOT_FOLDER = ConfigReader.Read("SCREENSHOT_FOLDER", "./logs");
        private static readonly string PREFECTURE_URL = ConfigReader.Read("PREFECTURE_URL", "http://www.hauts-de-seine.gouv.fr/booking/create/13525");
        private static readonly ViberAlertSender AlertSender = new ViberAlertSender(ConfigReader.Read("ViberAuthToken", "secret"));

        private static async Task<int> TakeScreenShot()
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true
            });
            var page = await browser.NewPageAsync();
            await page.GoToAsync("http://www.hauts-de-seine.gouv.fr/booking/create/13525").ConfigureAwait(false);
            await page.ScreenshotAsync("./rdv.jpg").ConfigureAwait(false);
            return 0;
        }

        private static async Task<string> CaptureScreen(Page page)
        {
            string screenCaptureFileName = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".jpg";
            var screenCaptureFilePath = Path.Combine(SCREENSHOT_FOLDER, screenCaptureFileName);
            await page.ScreenshotAsync(screenCaptureFilePath).ConfigureAwait(false);
            Log.Debug($"Screen captured {screenCaptureFilePath}");
            return screenCaptureFilePath;
        }

        private static async Task SendAlert(string message)
        {
            //cut off the message in case it is too long
            message = ArrayDisplayer.DefaultEllipsis(message, 100, "...");

            Log.Information(message);

            var receivers = Settings.Default.ViberReceiverIDs.Cast<string>().ToArray();
            var sendResult = await AlertSender.Send(message, receivers).ConfigureAwait(false);

            Log.Information("Send alert: " + sendResult);
        }

        /// <summary>
        /// Load the page, send the form, check the response page:
        /// return "no" if detect the message "Il n'existe plus de plage horaire libre pour votre demande de rendez-vous. Veuillez recommencer ultérieurement."
        /// Otherwise take a screen shot then return the Alert message
        /// </summary>
        /// <returns></returns>
        private static async Task<CrawlResult> Crawl()
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision).ConfigureAwait(false);
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true
            }).ConfigureAwait(false);
            await using (browser.ConfigureAwait(false))
            {
                var page = await browser.NewPageAsync().ConfigureAwait(false);
                await using (page.ConfigureAwait(false))
                {
                    await page.GoToAsync(PREFECTURE_URL).ConfigureAwait(false);
                    await page.ClickAsync("input#condition").ConfigureAwait(false);
                    await Task.Delay(1000).ConfigureAwait(false); //make the browser breath
                    await page.ClickAsync("input.Bbutton[name='nextButton']").ConfigureAwait(false);
                    await Task.Delay(1000).ConfigureAwait(false); //make the browser breath

                    var contentNode = await page.QuerySelectorAsync("form#FormBookingCreate").ConfigureAwait(false);
                    if (contentNode != null)
                    {
                        string content = await contentNode.EvaluateFunctionAsync<string>("(element) => {return element.innerText}").ConfigureAwait(false);
                        if (content.StartsWith(NOT_AVAILABLE_MSG))
                        {
                            return new CrawlResult
                            {
                                Category = eCrawlResultCategory.NothingNew,
                            };
                        }
                        else
                        {
                            var screenCaptureFileName = await CaptureScreen(page).ConfigureAwait(false);
                            return new CrawlResult
                            {
                                Category = eCrawlResultCategory.NeedToCheck,
                                Message = $"Go to the website NOW! Checkout '{screenCaptureFileName}' {content}"
                            };
                        }
                    }
                    else
                    {
                        var screenCaptureFileName = await CaptureScreen(page).ConfigureAwait(false);
                        var hasSomeContent = (await page.QuerySelectorAsync("#container").ConfigureAwait(false)) != null;
                        if (hasSomeContent)
                        {
                            return new CrawlResult
                            {
                                Category = eCrawlResultCategory.NeedToCheck,
                                Message = $"Go to the website NOW! Checkout '{screenCaptureFileName}' form#FormBookingCreate empty"
                            };
                        }
                        else
                        {
                            return new CrawlResult
                            {
                                Category = eCrawlResultCategory.ServerDown,
                                Message = $"Checkout '{screenCaptureFileName}'"
                            };
                        }
                    }
                }
            }
        }

        private static async Task Run()
        {
            CrawlResult alertContent = null;
            bool crawlOk = false;

            for (int i = 1; i <= 3; i++)
            {
                try
                {
                    alertContent = await Crawl().ConfigureAwait(false);
                    crawlOk = true;
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to crawl (attempt {i}): {ex.Message}");
                }
            }

            if (crawlOk)
            {
                if (alertContent.Category == eCrawlResultCategory.NeedToCheck)
                {
                    await SendAlert(alertContent.Message).ConfigureAwait(false);
                }
                else
                {
                    Log.Debug($"{alertContent.Category} {alertContent.Message}");
                }
            }
        }

        private static async Task Main()
        {
            Log.Logger = new LoggerConfiguration()
            .ReadFrom.AppSettings()
            .CreateLogger();

            try
            {
                //SendAlert("Test gui tu C#! sorry Nga dung quan tam").Wait();
                await Run().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "");
            }
        }
    }
}