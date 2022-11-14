using System;
using Serilog;
using System.Threading.Tasks;
using PuppeteerSharp;
using Xunit;
using Xunit.Abstractions;

namespace nghe.tests
{
    public class TakeScreenShotTests
    {
        [Fact]
        private static async Task TakeScreenShot()
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true
            });
            var page = await browser.NewPageAsync();
            await page.GoToAsync("http://www.hauts-de-seine.gouv.fr/booking/create/13525").ConfigureAwait(false);
            await page.ScreenshotAsync($"./rdv{DateTime.Now.ToString("_HH_mm_ss")}.jpg").ConfigureAwait(false);
        }
    }
}