using System;
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

        static async Task<string> GetRDVContent()
        {

            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true
            });
            var page = await browser.NewPageAsync();
            await page.GoToAsync("http://www.hauts-de-seine.gouv.fr/booking/create/13525").ConfigureAwait(false);
            await page.ClickAsync("input#condition").ConfigureAwait(false);
            await page.ClickAsync("input.Bbutton[name='nextButton']").ConfigureAwait(false);
            var contentNode = await page.QuerySelectorAsync("form#FormBookingCreate").ConfigureAwait(false);
            var content = await contentNode.EvaluateFunctionAsync<string>("(element) => {return element.innerText}").ConfigureAwait(false);
            return content;
        }

        static void SendAlert(string content)
        {
            Log.Debug("Detect Changed: " + ArrayDisplayer.DefaultEllipsis(content, 60, "..."));
        }

        static async Task<int> Run()
        {
            string rdvContent = await GetRDVContent().ConfigureAwait(false);
            if (rdvContent.StartsWith(NOT_AVAILABLE_MSG))
            {
                Log.Debug("no");
            }
            else
            {
                SendAlert(rdvContent);
            }
            return 0;
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
