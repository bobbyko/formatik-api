using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;

namespace Octagon.Formatik.API
{
    public static class Common
    {
        private static IDictionary<string, IMongoDatabase> dbs;

        static Common()
        {
            dbs = new Dictionary<string, IMongoDatabase>();
        }

        public static IMongoDatabase GetDB(string connection)
        {
            if (!dbs.TryGetValue(connection, out var client))
            {
                lock (dbs)
                {
                    if (!dbs.TryGetValue(connection, out client))
                    {
                        var conn = new Uri(connection);
                        var server = new MongoClient(connection);

                        client = server.GetDatabase(
                            string.IsNullOrEmpty(conn.LocalPath.StartsWith("/") ? conn.LocalPath.Substring(1) : conn.LocalPath) ? 
                                server.ListDatabases().First().Elements.First(el => el.Name == "name").Value.ToString() : 
                                conn.LocalPath.Substring(1));

                        dbs.Add(connection, client);
                        return client;
                    }
                    else
                        return client;
                }
            }
            else
                return client;
        }
    }
}