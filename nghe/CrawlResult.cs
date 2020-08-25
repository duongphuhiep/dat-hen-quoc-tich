namespace nghe
{
    public enum eCrawlResultCategory
    {
        NothingNew,
        ServerDown,
        NeedToCheck
    }

    public class CrawlResult
    {
        public eCrawlResultCategory Category { get; set; }
        public string Message { get; set; }
    }
}