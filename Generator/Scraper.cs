using System.Globalization;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace FLRC.TrackResults.Generator;

public class Scraper
{
	private readonly HttpClient _http;
	private readonly HtmlParser _parser;

	public Scraper(HttpClient http, HtmlParser parser)
	{
		_http = http;
		_parser = parser;
	}

	public async Task<Race[]> Run(string baseURL)
	{
		var pageMarker = baseURL.LastIndexOf('/') + 1;
		var pageCount = await GetPageCount(baseURL);
		var pages = Enumerable.Range(1, pageCount)
			.Select(page => baseURL.Insert(pageMarker, $"page/{page}"));

		var results = await GetAllResults(pages);
		return results.SelectMany(r => r).ToArray();
	}

	private async Task<byte> GetPageCount(string url)
	{
		var html = await _http.GetStringAsync(url);
		var document = await _parser.ParseDocumentAsync(html);
		var lastPage = document.QuerySelectorAll("ul.page-numbers li:not(:has(.next))").LastOrDefault();

		return lastPage is not null
			? byte.Parse(lastPage.TextContent.Trim())
			: (byte)0;
	}

	private async Task<Race[][]> GetAllResults(IEnumerable<string> pages)
	{
		var results = new List<Race[]>();
		foreach (var page in pages)
		{
			var result = await GetRaces(page);
			results.Add(result);
		}

		return results.ToArray();
	}

	private async Task<Race[]> GetRaces(string pageURL)
	{
		var html = await _http.GetStringAsync(pageURL);
		var document = await _parser.ParseDocumentAsync(html);
		var races = document.QuerySelectorAll(".race-title");
		var results = await GetResults(races);
		return results.Where(r => r is not null).Cast<Race>().ToArray();
	}

	private async Task<Race?[]> GetResults(IHtmlCollection<IElement> races)
	{
		var results = new List<Race?>();
		foreach (var race in races)
		{
			var result = await GetRace(race);
			results.Add(result);
		}

		return results.ToArray();
	}

	private async Task<Race?> GetRace(IElement info)
	{
		var link = info.QuerySelector("a");
		var start = info.QuerySelector(".race-start");
		if (link is null || start is null)
			return null;

		var url = link.GetAttribute("href");
		var html = await _http.GetStringAsync(url);
		var document = await _parser.ParseDocumentAsync(html);

		var events = document.QuerySelectorAll(".result-section");
		if (events.Length == 0)
			return null;

		var results = link.TextContent.Contains("Mile", StringComparison.InvariantCultureIgnoreCase) ? ParseResultsForMileMeet(events)
			: events.Length == 1 ? ParseResultsFromSingleTable(events[0].QuerySelector("table"))
			: ParseResultsForStandardMeet(events);

		return new Race
		{
			Name = link.TextContent.Trim(),
			Date = DateTime.Parse(start.TextContent.Trim()),
			Events = results.Where(FilterEvents).ToDictionary(e => Cleanup(e.Key), e => e.Value)
		};
	}

	private static Dictionary<string, Result[]> ParseResultsForStandardMeet(IHtmlCollection<IElement> events)
	{
		var allResults = GetResultsTables(events);
		var dividedResults = allResults.Where(r => r.Key.Contains("Men", StringComparison.InvariantCultureIgnoreCase)).GroupBy(r => r.Key.Split(' ')[1]).ToArray();
		if (dividedResults.Length == 0)
			return allResults;

		var finalResults = allResults.Where(r => !r.Key.Contains("Men", StringComparison.InvariantCultureIgnoreCase)).ToDictionary();
		foreach (var results in dividedResults)
		{
			var combinedResults = CombineDividedResults(results.ToDictionary());
			if (finalResults.TryGetValue(results.Key, out var originalResults))
			{
				var resultsList = originalResults.ToList();
				resultsList.AddRange(combinedResults);
				finalResults[results.Key] = resultsList.OrderBy(r => r.Time).ThenByDescending(r => r.Distance).ToArray();
			}
			else
			{
				finalResults.Add(results.Key, combinedResults);
			}
		}

		return finalResults;
	}

	private static Dictionary<string, Result[]> GetResultsTables(IHtmlCollection<IElement> events)
		=> events.ToDictionary(
			e => ParseEventName(e.QuerySelector(".result-section-heading")),
			e => ParseResults(e.QuerySelector("table"))
		);

	private static string ParseEventName(IElement? element)
		=> CultureInfo.InvariantCulture.TextInfo.ToTitleCase(element?.TextContent.Trim().ToLowerInvariant() ?? string.Empty).Replace(" Results", "").Replace("M", "m");

	private static bool FilterEvents(KeyValuePair<string, Result[]> r)
		=> !r.Key.Contains('x', StringComparison.InvariantCultureIgnoreCase)
		   && !r.Key.Contains("sprint", StringComparison.InvariantCultureIgnoreCase)
		   && !r.Key.Contains("medley", StringComparison.InvariantCultureIgnoreCase)
		   && !r.Key.Contains("relay", StringComparison.InvariantCultureIgnoreCase)
		   && !r.Key.Contains("SMR", StringComparison.InvariantCultureIgnoreCase);

	private static string Cleanup(string e)
		=> e.Replace("mile", "mi")
			.Replace(" mi", "mi")
			.Replace("meters", "m")
			.Replace("Race Walk", "Racewalk");

	private static Dictionary<string, Result[]> ParseResultsForMileMeet(IHtmlCollection<IElement> events)
	{
		var allTables = GetResultsTables(events);
		if (allTables.Count == 1)
			return new Dictionary<string, Result[]> { { "1mi", allTables.First().Value.OrderBy(r => r.Time).ToArray() } };

		var divisions = allTables.Where(t => t.Key.StartsWith("All ")).ToDictionary();
		return new Dictionary<string, Result[]> { { "1mi", CombineDividedResults(divisions) } };
	}

	private static Result[] CombineDividedResults(IDictionary<string, Result[]> divisions)
	{
		foreach (var race in divisions)
		{
			var division = race.Key.Contains("Women", StringComparison.InvariantCultureIgnoreCase) ? 'F' : 'M';
			foreach (var result in race.Value)
				result.Division = division;
		}

		return divisions.SelectMany(d => d.Value).OrderBy(r => r.Time).ThenByDescending(r => r.Distance).ToArray();
	}

	private static Dictionary<string, Result[]> ParseResultsFromSingleTable(IElement? table)
		=> ParseResults(table).GroupBy(r => r.Event!.Replace(" m", "m"))
			.ToDictionary(r => r.Key, r => r.ToArray());

	private static Result[] ParseResults(IElement? table)
	{
		if (table is null)
			return [];

		var columns = table.QuerySelectorAll("th");
		var eventColumn = FindColumn("Event");
		var firstNameColumn = FindColumn("First Name");
		var lastNameColumn = FindColumn("Last Name");
		var nameColumn = FindColumn("Name");
		var sexColumn = FindColumn("Sex");
		var genderColumn = FindColumn("Gender");
		var combinedSexPlaceColumn = FindColumn("Sex/Place");
		var ageColumn = FindColumn("Age");
		var timeColumn = FindColumn("Time");
		var distanceColumn = FindColumn("Distance");
		var heightColumn = FindColumn("Height");

		var results = new List<Result>();

		foreach (var row in table.QuerySelectorAll("tbody tr").Where(r => r.TextContent.Trim() != string.Empty))
		{
			var race = eventColumn is not null ? row.QuerySelector($".{eventColumn}")?.TextContent.Trim() : null;
			var name = firstNameColumn is not null && lastNameColumn is not null
				? row.QuerySelector($".{firstNameColumn}")?.TextContent.Trim() + " " + row.QuerySelector($".{lastNameColumn}")?.TextContent.Trim()
				: nameColumn is not null
					? row.QuerySelector($".{nameColumn}")?.TextContent.Trim() ?? string.Empty
					: string.Empty;

			var nameParts = name.Split(',');
			if (nameParts.Length > 1)
				name = $"{nameParts[1].Trim()} {nameParts[0].Trim()}";

			var division = sexColumn is not null ? row.QuerySelector($".{sexColumn}")?.TextContent.Trim()
				: genderColumn is not null ? row.QuerySelector($".{genderColumn}")?.TextContent.Trim()
				: combinedSexPlaceColumn is not null ? row.QuerySelector($".{combinedSexPlaceColumn}")?.TextContent[..1]
				: ageColumn is not null ? row.QuerySelector($".{ageColumn}")?.TextContent[..1]
				: null;

			if (division == "W")
				division = "F";

			var age = ageColumn is not null ? row.QuerySelector($".{ageColumn}")?.TextContent.Replace("M", "").Replace("F", "").Replace("W", "").Trim() : null;
			var time = timeColumn is not null ? row.QuerySelector($".{timeColumn}")?.TextContent.Trim() : null;
			var distance = distanceColumn is not null ? row.QuerySelector($".{distanceColumn}")?.TextContent.Trim()
				: heightColumn is not null ? row.QuerySelector($".{heightColumn}")?.TextContent.Trim()
				: null;

			var result = new Result
			{
				Event = race,
				Name = name.Trim(),
				Division = division != string.Empty ? division?.ToCharArray()[0] ?? ' ' : ' ',
				Age = age is not null && age != string.Empty && byte.TryParse(age, out var actualAge) ? actualAge : (byte)0,
				Time = time is not null ? ParseTime(time) : null,
				Distance = distance is not null
					? new Distance(distance)
					: null
			};
			results.Add(result);
		}

		return results.ToArray();

		string? FindColumn(string title) => columns.FirstOrDefault(c => c.TextContent.Trim().Equals(title, StringComparison.InvariantCultureIgnoreCase))?.ClassList.FirstOrDefault(c => c.StartsWith("column-"));
	}

	private static TimeSpan? ParseTime(string time)
		=> time is "DNF" or "DNS" or "DQ" or "?" or "" ? null
			: time.Split(":").Length == 2 ? TimeSpan.Parse("0:0" + time.Split(" ")[0].Replace("*", ""))
			: TimeSpan.FromSeconds(double.Parse(time));
}