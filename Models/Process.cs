using MongoDB.Bson.Serialization.Attributes;

namespace Octagon.Formatik.API
{
    public class Process : APIResponse
    {
        public string FormatId { get; set; }
        public string Name { get; set; }
        public string Result { get; set; }
        public int Processed { get; set; }

        [BsonIgnore]
        public string InputCacheId { get; set; }

        public new static Process GetError(ErrorCode code, string error)
        {
            return new Process() {
                Status = "ERROR",
                Error = error,
                ErrorCode = code.ToString()
            };
        }        
    }
}