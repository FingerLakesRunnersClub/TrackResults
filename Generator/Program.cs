using System.IO.Abstractions;
using AngleSharp.Html.Parser;

namespace FLRC.TrackResults.Generator;

public static class Program
{
	private const string BaseURL = "https://fingerlakesrunners.org/race/?race-surface=track";

	private static readonly Action<string> Log = Console.WriteLine;
	public static async Task Main()
	{
		Log("Scraping...");
		var scraper = new Scraper(new HttpClient(), new HtmlParser(), Log);
		var results = await scraper.Run(BaseURL);
		Log("Done scraping!");

		Log("Writing files...");
		var fileWriter = new FileWriter(new FileSystem());
		await fileWriter.SaveAll(results);
		Log("Done!");
	}
}