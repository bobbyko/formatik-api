using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Octagon.Formatik.API
{
    public static class DBInit
    {
        public static void Convention()
        {
            var pack = new ConventionPack()
            {
                new EnumRepresentationConvention(BsonType.String),
                new CamelCaseElementNameConvention()
            };

            ConventionRegistry.Register("FormatikConventions", pack, t => true);
        }

        public static async void EnsureDbIndexes(IConfigurationRoot configuration)
        {
            var db = Common.GetDB(configuration.GetValue<string>("Formatik:DbConnection"));

            // Formats Collection
            
            var formatIndexes = db
                .GetCollection<Format>("Formats")
                .Indexes
                .List()
                .ToEnumerable()
                .ToArray();

            if (!formatIndexes.Any(rec => rec["name"].AsString == "userId_evaluation.hash"))
            {
                await db.GetCollection<Format>("Formats").Indexes.CreateOneAsync(
                    new IndexKeysDefinitionBuilder<Format>()
                        .Ascending(f => f.UserId)
                        .Ascending(f => f.Formatik.Hash),
                    new CreateIndexOptions() { Version = 2, Unique = true, Name = "userId_evaluation.hash", Background = true });
            }

            if (!formatIndexes.Any(rec => rec["name"].AsString == "userId_name"))
            {
                await db.GetCollection<Format>("Formats").Indexes.CreateOneAsync(
                    new IndexKeysDefinitionBuilder<Format>()
                        .Ascending(f => f.UserId)
                        .Ascending(f => f.Name),
                    new CreateIndexOptions() { Version = 2, Unique = true, Name = "userId_name", Background = true });
            }

            if (!formatIndexes.Any(rec => rec["name"].AsString == "temporary"))
            {
                await db.GetCollection<Format>("Formats").Indexes.CreateOneAsync(
                    new IndexKeysDefinitionBuilder<Format>()
                        .Ascending(f => f.Temporary),
                    new CreateIndexOptions()
                    {
                        Version = 2,
                        Sparse = true,
                        Name = "temporary",
                        Background = true,
                        ExpireAfter = TimeSpan.FromSeconds(configuration.GetValue<int>("Formatik:TemporaryFormatDurationSec"))
                    });
            }

            // InputCache collection 

            var inputCacheIndexes = db
                .GetCollection<InputCache>("InputCache")
                .Indexes
                .List()
                .ToEnumerable()
                .ToArray();

            if (!inputCacheIndexes.Any(rec => rec["name"].AsString == "created"))
            {
                await db.GetCollection<InputCache>("InputCache").Indexes.CreateOneAsync(
                    new IndexKeysDefinitionBuilder<InputCache>()
                        .Ascending(c => c.Created),
                    new CreateIndexOptions()
                    {
                        Version = 2,
                        Name = "created",
                        Background = true,
                        ExpireAfter = TimeSpan.FromSeconds(configuration.GetValue<int>("Formatik:InputCacheDurationSec"))
                    });
            }
        }
    }
}