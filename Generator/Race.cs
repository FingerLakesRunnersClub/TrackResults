namespace FLRC.TrackResults.Generator;

public class Race
{
	public required string Name { get; init; }
	public DateTime Date { get; init; }
	public required Dictionary<string, Result[]> Events { get; init; }
}