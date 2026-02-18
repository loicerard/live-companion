namespace LiveCompanion.Core.Models;

public class Setlist
{
    public const int DefaultPpqn = 480;

    public string Name { get; set; } = string.Empty;
    public int Ppqn { get; set; } = DefaultPpqn;
    public List<Song> Songs { get; set; } = [];
}
