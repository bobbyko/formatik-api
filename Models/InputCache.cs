using System;
using MongoDB.Bson;

namespace Octagon.Formatik.API
{
    public class InputCacheIdType
    {
        public ObjectId UserId { get; set; }
        public string InputHash { get; set; }
    }

    public class InputCache
    {
        public InputCacheIdType _id { get; set; }
        public string Input { get; set; }
        public DateTime Created { get; set; }
    }
}