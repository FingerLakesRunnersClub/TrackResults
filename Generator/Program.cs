using System.IO.Abstractions;
using AngleSharp.Html.Parser;

namespace FLRC.TrackResults.Generator;

public static class Program
{
	private const string BaseURL = "https://fingerlakesrunners.org/race/?race-surface=track";

	public static async Task Main()
	{
		var scraper = new Scraper(new HttpClient(), new HtmlParser());
		var results = await scraper.Run(BaseURL);

		var fileWriter = new FileWriter(new FileSystem());
		await fileWriter.SaveAll(results);
	}
}