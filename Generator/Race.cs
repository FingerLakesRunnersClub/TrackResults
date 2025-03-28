namespace FLRC.TrackResults.Generator;

public class Race
{
	public string Name { get; init; }
	public DateTime Date { get; init; }
	public Dictionary<string, Result[]> Events { get; init; }
}