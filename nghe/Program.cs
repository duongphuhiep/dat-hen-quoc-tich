using System;
using System.IO;
using System.Threading.Tasks;
using PuppeteerSharp;
using Serilog;
using ToolsPack.Config;
using ToolsPack.Displayer;

namespace nghe
{
    class Program
    {
        static readonly string NOT_AVAILABLE_MSG = ConfigReader.Read("NOT_AVAILABLE_MSG", "Il n'existe plus de plage horaire libre pour votre demande de rendez-vous. Veuillez recommencer ultérieurement.");
        static readonly string SCREENSHOT_FOLDER = ConfigReader.Read("SCREENSHOT_FOLDER", "./");

        static async Task<int> TakeScreenShot()
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

        static async Task<string> CaptureScreen(Page page)
        {
            string screenCaptureFileName = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".jpg";
            var screenCaptureFilePath = Path.Combine(SCREENSHOT_FOLDER, screenCaptureFileName);
            await page.ScreenshotAsync(screenCaptureFilePath).ConfigureAwait(false);
            Log.Debug($"Screen captured {screenCaptureFilePath}");
            return screenCaptureFileName;
        }

        static void SendAlert(string message)
        {
            Log.Information(ArrayDisplayer.DefaultEllipsis(message, 100, "..."));
            //TODO send to viber / zalo
        }

        /// <summary>
        /// Load the page, send the form, check the response page: 
        /// return "no" if detect the message "Il n'existe plus de plage horaire libre pour votre demande de rendez-vous. Veuillez recommencer ultérieurement."
        /// Otherwise take a screenshot then return the Alert message
        /// </summary>
        /// <returns></returns>
        static async Task<string> Crawl()
        {
            const string URL = "http://www.hauts-de-seine.gouv.fr/booking/create/13525";
            //const string URL = "haha.com";

            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision).ConfigureAwait(false);
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true
            }).ConfigureAwait(false);

            var page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(URL).ConfigureAwait(false);
            await page.ClickAsync("input#condition").ConfigureAwait(false);
            await page.ClickAsync("input.Bbutton[name='nextButton']").ConfigureAwait(false);

            // var pha = await page.QuerySelectorAsync("form#FormBookingCreate").ConfigureAwait(false);
            // string pha2 = await pha.EvaluateFunctionAsync<string>("(element) => {return element.innerText}").ConfigureAwait(false);

            var contentNode = await page.QuerySelectorAsync("form#FormBookingCreate").ConfigureAwait(false);
            if (contentNode != null)
            {
                string content = await contentNode.EvaluateFunctionAsync<string>("(element) => {return element.innerText}").ConfigureAwait(false);
                if (content.StartsWith(NOT_AVAILABLE_MSG))
                {
                    return "no"; //nothing to alert
                }
                else
                {
                    var screenCaptureFileName = CaptureScreen(page);
                    return $"Detect changed. Checkout '{screenCaptureFileName}' {content}";
                }
            }
            else
            {
                var screenCaptureFileName = CaptureScreen(page);
                return "Detect changed. Checkout '{screenCaptureFileName}' form#FormBookingCreate empty";
            }
        }

        static async Task Run()
        {
            string alertContent = "no";
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
                if ("no".Equals(alertContent, StringComparison.InvariantCulture))
                {
                    Log.Debug("no");
                    return;
                }
                SendAlert(alertContent);
            }
        }

        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
            .ReadFrom.AppSettings()
            .CreateLogger();

            try
            {
                Run().Wait();
            }
            catch (Exception ex)
            {
                Log.Error("{Exception}", ex);
            }
        }
    }
}
