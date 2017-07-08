namespace Octagon.Formatik.API
{
    public class FormatikConfiguration
    {
        public string DbConnection { get; set; }
        public string LogsDbConnection { get; set; }
        public int InputCacheDurationSec { get; set; }
    }
}