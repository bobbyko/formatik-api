using MongoDB.Bson.Serialization.Attributes;

namespace Octagon.Formatik.API
{
    public class APICall
    {
        public string Request { get; set; }
        public string Response { get; set; }
        public int Duration { get; set; }
    }
}