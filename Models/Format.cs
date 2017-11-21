using System;
using System.Runtime.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Octagon.Formatik.API
{
    public class Format : APIResponse
    {
        [IgnoreDataMember]
        public ObjectId _id { get; set; }
        
        [BsonIgnore]
        public string FormatId { get {return _id != ObjectId.Empty ? _id.ToString() : null; } }

        [IgnoreDataMember]
        public ObjectId UserId { get; set; }

        public string Name { get; set; }

        [BsonIgnore]
        public string InputCacheId { get; set; }

        public DateTime Created { get; set; }

        [BsonIgnoreIfDefault]
        public DateTime? LastUpdated { get; set; }

        [BsonIgnore]
        public int InputSize { get; set; }

        [BsonIgnore]
        public int InputRecords { get; set; }

        // Important to NOT save null or false values for this field
        // This field is used in a sparse TTL index "temporary", which clears up any expired temporary documents.
        // Permenent documents (aka documents without the field temporary will not be removed)
        [IgnoreDataMember]
        [BsonDefaultValue(null)]
        [BsonIgnoreIfDefault]
        public DateTime? Temporary { get; set; }

        public BsonFormatik Formatik { get; set; }

        public new static Format GetError(ErrorCode code, string error)
        {
            return new Format() {
                Status = "ERROR",
                Error = error,
                ErrorCode = code.ToString()
            };
        }        
    }
}