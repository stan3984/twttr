namespace twttr.Configuration;

public class RateLimitingOptions
{
    public const string Section = "RateLimiting";
    public WindowOptions Global { get; set; } = new() { Permits = 300, Seconds = 60 };
    public WindowOptions Login { get; set; } = new() { Permits = 10, Seconds = 300 };
    public WindowOptions Register { get; set; } = new() { Permits = 5, Seconds = 3600 };
    public WindowOptions Post { get; set; } = new() { Permits = 5, Seconds = 60 };

    public class WindowOptions
    {
        public int Permits { get; set; }
        public int Seconds { get; set; }
    }
}
