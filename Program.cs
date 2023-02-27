using Microsoft.Playwright;

Console.WriteLine("Spotify username for target account:");
var username = Console.ReadLine();
Console.WriteLine("Spotify password for target account:");
var password = Console.ReadLine();

var pw = await Playwright.CreateAsync();
var page = await (await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false })).NewPageAsync();
await page.GotoAsync("https://accounts.spotify.com/en/login?continue=https%3A%2F%2Fopen.spotify.com%2F%3F");
await page.Locator("[id=\"login-username\"]").First.FillAsync(username);
await page.Locator("[id=\"login-password\"]").First.FillAsync(password);
await page.Locator("[id=\"login-button\"]").First.ClickAsync();

await Wait(TimeSpan.FromSeconds(3));
await page.Locator("#onetrust-pc-btn-handler").ClickAsync();
await page.GetByRole(AriaRole.Button, new() { Name = "Confirm My Choices" }).ClickAsync();
await Wait(TimeSpan.FromSeconds(3));
await page.GotoAsync("https://open.spotify.com/collection/playlists");
const string playlistsSelector = "xpath=//section/div/div/div//a";
await page.WaitForSelectorAsync(playlistsSelector);
var playlistTasks = (await page.Locator(playlistsSelector).AllAsync()).Select(x => x.EvaluateAsync<string>("(el) => el.getAttribute('href')")).ToArray();
var playlists = (await Task.WhenAll(playlistTasks)).Where(x => x.Contains("/playlist")).Select(x => "https://open.spotify.com" + x);
foreach (var playlist in playlists)
{
    await CopyPlaylist(page, playlist);
}


async Task AddSongsToPlaylist(string playlistName)
{
    var songsCount = int.Parse((await page.Locator("span[data-encore-id]", new PageLocatorOptions { HasTextRegex = new System.Text.RegularExpressions.Regex("[0-9]* songs, (about|[0-9]*)") }).TextContentAsync()).Split("songs").First().Trim());
    for (int i = 2; i < songsCount + 2; i++)
    {
        await AddSong(playlistName, page, i);
    }

    static async Task AddSong(string playlistName, IPage page, int i)
    {
        while (true)
        {
            var track = page.Locator($"[aria-rowindex='{i}']").First;
            try
            {
                await track.HoverAsync();
                await Wait(TimeSpan.FromMilliseconds(100));
                await track.Locator("button").Last.ClickAsync();
                await page.HoverAsync("text=Add to playlist");
                try
                {
                    await page.Locator("button[role=\"menuitem\"]", new PageLocatorOptions { HasTextRegex = new System.Text.RegularExpressions.Regex($"^({playlistName})") }).Last.ClickAsync(new LocatorClickOptions { Timeout = ((float)TimeSpan.FromSeconds(1).TotalMilliseconds) });
                }
                catch (Exception)
                {
                    var all = await page.Locator("button[role=\"menuitem\"]").AllAsync();
                    foreach (var item in all)
                    {
                        var text = await item.InnerTextAsync();
                        if (text.StartsWith(playlistName))
                        {
                            await item.ClickAsync();
                            break;
                        }
                    }
                }
                await page.Keyboard.DownAsync("ArrowDown");
                await Wait(TimeSpan.FromMilliseconds(200));
                return;
            }
            catch (Exception ex)
            {
                // probably handle situations where it hs gone too far and the virtual dom has lready cut it off, not really a problem tho, just find all that have the same attribute and get the lowest
                Logger.Log("@ i = " + i);
                var texts = await page.Locator("button[role=\"menuitem\"]").AllTextContentsAsync();
                Logger.Log(ex.Message);
                await page.Keyboard.DownAsync("ArrowDown");
            }
        }
    }
}

async Task CopyPlaylist(IPage page, string? playlist)
{
    await page.GotoAsync(playlist);
    var title = await page.TextContentAsync("[data-testid=\"entityTitle\"]");
    var tempTitle = title + "_temp";
    await page.ClickAsync("text=Create Playlist");

    await ChangePlaylistTitle(page, tempTitle);
    var newPlaylist = page.Url;

    await page.GotoAsync(playlist);
    await AddSongsToPlaylist(tempTitle);
    await page.ClickAsync("[data-testid=\"more-button\"]");
    await page.ClickAsync("text=Remove from Your Library");

    await Wait(TimeSpan.FromSeconds(2));
    await page.GotoAsync(newPlaylist);
    await Wait(TimeSpan.FromSeconds(2));
    await ChangePlaylistTitle(page, title);

    static async Task ChangePlaylistTitle(IPage page, string? title)
    {
        await Wait(TimeSpan.FromSeconds(2));
        await page.ClickAsync("[data-testid=\"entityTitle\"]");
        const string selector = "[placeholder=\"Add a name\"]";
        await page.FillAsync(selector, string.Empty);
        await page.FillAsync(selector, title);
        await page.ClickAsync("[data-testid=\"playlist-edit-details-save-button\"]");
    }
}

static async Task Wait(TimeSpan wait) =>
    await Task.Delay(wait);

class Logger
{
    public static void Log(string message) => Console.WriteLine(message);
}