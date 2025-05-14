using Microsoft.Playwright;
using System.Threading.Tasks;

public class PlaywrightExample
{
  public async Task<string> GetPageHtmlAsync(string url)
  {
    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
    {
      Headless = true
    });

    var context = await browser.NewContextAsync();
    var page = await context.NewPageAsync();
    await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

    return await page.ContentAsync();
  }
}
