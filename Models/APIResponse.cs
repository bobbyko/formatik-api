using System.Runtime.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace Octagon.Formatik.API
{
    public class APIResponse
    {
        [BsonIgnore]
        
        public string Status { get; set; } = "OK";
        
        [BsonIgnore]        
        public string Error { get; set; }

        [BsonIgnore]        
        public string ErrorCode { get; set; }

        public static APIResponse GetError(ErrorCode code, string error)
        {
            return new APIResponse() { 
                Status = "Error",
                Error = error,
                ErrorCode = code.ToString()
            };
        }
    }
}