namespace FLRC.TrackResults.Generator;

public class Result
{
	public string? Event { get; set; }
	public string Name { get; init; }
	public char Division { get; set; }
	public byte Age { get; init; }
	public TimeSpan? Time { get; init; }
	public Distance? Distance { get; init; }
}