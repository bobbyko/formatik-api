namespace Octagon.Formatik.API
{
    public class FormatikSettings
    {
        public string DbConnection { get; set; }
        public string LogsDbConnection { get; set; }
        public int InputCacheDurationSec { get; set; }
        public int? FileUploadMaxResultSize { get; set; }
    }
}