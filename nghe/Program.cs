using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        static readonly ViberAlertSender AlertSender = new ViberAlertSender(ConfigReader.Read("ViberAuthToken", "secret"));

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
            return screenCaptureFilePath;
        }

        static async Task SendAlert(string message)
        {
            //cut off the message in case it is too long
            message = ArrayDisplayer.DefaultEllipsis(message, 100, "...");

            Log.Information(message);

            var receivers = Settings.Default.ViberReceiverIDs.Cast<string>().ToArray();
            var sendResult = await AlertSender.Send(message, receivers).ConfigureAwait(false);

            Log.Information("Send alert: "+sendResult);
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
            await Task.Delay(1000).ConfigureAwait(false); //make the browser breath
            await page.ClickAsync("input.Bbutton[name='nextButton']").ConfigureAwait(false);

            await Task.Delay(1000).ConfigureAwait(false); //make the browser breath
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
                    var screenCaptureFileName = await CaptureScreen(page).ConfigureAwait(false);
                    return $"Go to the website NOW! Checkout '{screenCaptureFileName}' {content}";
                }
            }
            else
            {
                var screenCaptureFileName = await CaptureScreen(page).ConfigureAwait(false);
                return $"Go to the website NOW! Checkout '{screenCaptureFileName}' form#FormBookingCreate empty";
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
                await SendAlert(alertContent).ConfigureAwait(false);
            }
        }

        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
            .ReadFrom.AppSettings()
            .CreateLogger();

            try
            {
                //SendAlert("Test gui tu C#! sorry Nga dung quan tam").Wait();
                Run().Wait();
            }
            catch (Exception ex)
            {
                Log.Error("{Exception}", ex);
            }
        }
    }
}
