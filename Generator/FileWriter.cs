using System.IO.Abstractions;
using System.Text;

namespace FLRC.TrackResults.Generator;

public class FileWriter
{
	private const string BaseDir = "Results";

	private readonly IFileSystem _fs;

	public FileWriter(IFileSystem fs)
	{
		_fs = fs;
	}

	public async Task SaveAll(Race[] races)
	{
		foreach (var race in races)
			await Save(race);
	}

	private async Task Save(Race race)
	{
		var directory = _fs.Path.Join(BaseDir, $"{race.Date:yyyy-MM-dd}");
		_fs.Directory.CreateDirectory(directory);

		foreach (var e in race.Events)
		{
			var filename = _fs.Path.Join(directory, $"{e.Key}.txt");
			var data = GenerateFile(race, e.Key, e.Value);
			await _fs.File.WriteAllTextAsync(filename, data);
		}
	}

	private static string GenerateFile(Race race, string e, Result[] results)
	{
		const byte TotalWidth = 72;

		const byte NameWidth = 32;
		const byte DivisionWidth = 8;
		const byte AgeWidth = 4;
		const byte ResultWidth = 12;

		const byte GapWidth = 4;
		var gap = string.Empty.PadRight(GapWidth);
		var halfGap = string.Empty.PadRight(GapWidth / 2);

		var data = new StringBuilder();

		data.AppendLine(Center($"{race.Date:yyyy-MM-dd}", TotalWidth));
		data.AppendLine(Center(e, TotalWidth));
		data.AppendLine(Center(string.Empty, TotalWidth));

		var header = halfGap
			+ "Name".PadRight(NameWidth) + gap
	        + "Division".PadRight(DivisionWidth) + gap
	        + "Age".PadLeft(AgeWidth) + gap
	        + UnitType(e.ToLowerInvariant()).PadLeft(ResultWidth)
	        + halfGap;

		data.AppendLine(header);
		data.AppendLine(string.Empty.PadRight(TotalWidth, '-'));

		foreach (var result in results)
		{
			var name = result.Name.PadRight(NameWidth).PadRight(GapWidth);
			var division = result.Division.ToString().PadRight(DivisionWidth).PadRight(GapWidth);
			var age = result.Age.ToString().PadLeft(AgeWidth).PadRight(GapWidth);
			var time = Display(result.Time).PadLeft(ResultWidth).TrimEnd();
			var distance = Display(result.Distance).PadLeft(ResultWidth).TrimEnd();

			var line = halfGap + name + gap + division + gap + age + gap + time + distance + halfGap;
			data.AppendLine(line);
		}

		return data.ToString();
	}

	private static string UnitType(string e)
	{
		return e.EndsWith('m') || e.EndsWith("mi") || e.EndsWith("mh") || e.EndsWith("hurdles") || e.EndsWith("racewalk") ? "Time"
			: e is "pole vault" or "high jump" ? "Height"
			: "Distance";
	}

	private static string Display(Distance? distance)
		=> distance?.ToString() ?? string.Empty;

	private static string Display(TimeSpan? time)
		=> time?.ToString(time.Value.TotalSeconds > 60 ? @"m\:ss\.ff": @"s\.ff") ?? string.Empty;

	private static string Center(string data, byte length)
		=> data.PadLeft((data.Length + length) / 2).PadRight(length);
}