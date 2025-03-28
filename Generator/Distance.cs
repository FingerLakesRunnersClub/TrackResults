namespace FLRC.TrackResults.Generator;

public class Distance
{
	public Distance(string distance)
	{
		{
			var split = distance.Split("-");
			Feet = byte.Parse(split[0]);
			Inches = split.Length > 1 ? decimal.Parse(split[1]) : 0;
		}

	}

	private byte Feet { get; }
	private decimal Inches { get; }

	public override string ToString() => $"{Feet}'{Inches:00.00}\"";
}