using System.Text.Json.Serialization;

namespace Web.Data.Config;

public class TravelEventConfig
{
    public int ID { get; set; }
    public int Type { get; set; }
    public int Quantity { get; set; }
    public int Event { get; set; }
    public int[]? ResourceRandom { get; set; }
    public int[]? DropRandom { get; set; }
    public int Direction { get; set; }
    public int Distance { get; set; }
    public int Angle { get; set; }
    public int PlayerDistance { get; set; }
    public int SustainedDistance { get; set; }
}

