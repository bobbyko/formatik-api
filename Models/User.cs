using System;
using System.Runtime.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace  Octagon.Formatik.API
{
    public class User: APIResponse
    {
        [IgnoreDataMember]
        public ObjectId _id { get; set; }
        
        [BsonIgnore]
        public string id { get {return _id != ObjectId.Empty ? _id.ToString() : null; } }

        public string Name { get; set; }

        public Boolean? Active { get; set; }

        public int? MaxRecordCount { get; set; }

        public DateTime Created { get; set; }

        public DateTime LastModified { get; set; }

        public new static User GetError(string error)
        {
            return new User() {
                Status = "ERROR",
                Error = error
            };
        }
    }
}