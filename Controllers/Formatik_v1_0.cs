using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExcelDataReader;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Octagon.Formatik.API
{
    [Route("v1.0/")]
    [EnableCors("FullCors")]
    public class Formatik_v1_0 : Controller
    {
        private readonly FormatikSettings configuration;
        private readonly ILogger<Formatik_v1_0> logger;

        public Formatik_v1_0(IOptions<FormatikSettings> configuration, ILogger<Formatik_v1_0> logger)
        {
            this.configuration = configuration.Value;
            this.logger = logger;
        }

        private Task<string> GetCachedInputAsync(ObjectId userId, string inputCacheId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var db = Common.GetDB(configuration.DbConnection);

            return db
                .GetCollection<InputCache>("InputCache")
                .FindAsync(
                    Builders<InputCache>.Filter.And(
                        Builders<InputCache>.Filter.Eq(d => d._id.UserId, userId),
                        Builders<InputCache>.Filter.Eq(d => d._id.InputHash, inputCacheId)),
                    null,
                    cancellationToken)
                .ContinueWith(cursor =>
                {
                    var cached = cursor.Result.FirstOrDefaultAsync(cancellationToken).Result;
                    return cached != null ? cached.Input : null;
                });
        }

        private Task<API.User> GetUserAsync(ObjectId userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var db = Common.GetDB(configuration.DbConnection);

            return db
                .GetCollection<API.User>("Users")
                .FindAsync(
                    Builders<API.User>.Filter.And(
                        Builders<API.User>.Filter.Eq(user => user._id, userId),
                        Builders<API.User>.Filter.Eq(user => user.Active, true)),
                    null,
                    cancellationToken)
                .ContinueWith(cursor => cursor.Result.FirstOrDefaultAsync(cancellationToken).Result);
        }

        private async Task CacheInputAsync(ObjectId userId, string inputHash, string input)
        {
            var db = Common.GetDB(configuration.DbConnection);

            await db.GetCollection<InputCache>("InputCache")
                .UpdateOneAsync(Builders<InputCache>.Filter.And(
                        Builders<InputCache>.Filter.Eq(ic => ic._id.UserId, userId),
                        Builders<InputCache>.Filter.Eq(ic => ic._id.InputHash, inputHash)),
                    Builders<InputCache>.Update
                        .Set(ic => ic.Input, input)
                        .Set(ic => ic.Created, DateTime.Now)
                        .SetOnInsert(ic => ic._id.UserId, userId)
                        .SetOnInsert(ic => ic._id.InputHash, inputHash),
                    new UpdateOptions() { IsUpsert = true }
                );
        }

        // GET api/values/5
        [HttpGet("{userId}")]
        [EnableCors("FullCors")]
        public API.User GetUser(string userId)
        {
            if (!ObjectId.TryParse(userId, out var _id))
            {
                logger.LogInformation($"User not found or not active - {userId}");
                return API.User.GetError(ErrorCode.UserNotFound, "User not found or not active");
            }

            var user = GetUserAsync(new ObjectId(userId)).Result;

            if (user == null)
                logger.LogInformation("User not found or not active", userId);

            return user;
        }

        [HttpPost("{userId}/upload")]
        [EnableCors("FullCors")]
        public InputUpload UploadInput(string userId, IFormFile file)
        {
            if (!ObjectId.TryParse(userId, out var _userId))
            {
                logger.LogInformation($"User not found or not active - {userId}");
                return InputUpload.GetError(ErrorCode.UserNotFound, "User not found or not active");
            }

            using (var asyncQueriesCancelationTokenSource = new CancellationTokenSource())
            {
                var userQuery = GetUserAsync(_userId, asyncQueriesCancelationTokenSource.Token);

                var input = new MemoryStream();
                try
                {
                    file.CopyToAsync(input, asyncQueriesCancelationTokenSource.Token).Wait();

                    var user = userQuery.Result;
                    if (user == null)
                    {
                        asyncQueriesCancelationTokenSource.Cancel();
                        logger.LogInformation($"User not found or not active - {userId}");
                        return InputUpload.GetError(ErrorCode.UserNotFound, "User not found or not active");
                    }

                    input.Seek(0, SeekOrigin.Begin);

                    // try to read as XLS and convert to CSV
                    try
                    {
                        using (var reader = ExcelReaderFactory.CreateReader(input))
                        {
                            var csvInput = new MemoryStream();
                            using (var writer = new StreamWriter(csvInput, Encoding.Unicode, 8192, true))
                            {
                                do
                                {
                                    if (reader.HeaderFooter != null && reader.HeaderFooter.FirstHeader != null)
                                        writer.WriteLine(reader.HeaderFooter.FirstHeader);

                                    while (reader.Read())
                                    {
                                        writer.WriteLine(
                                            string.Join(",", Enumerable
                                                .Range(0, reader.FieldCount)
                                                .Select(i => reader.IsDBNull(i) ? "" : reader.GetValue(i).ToString())
                                                .Select(strVal => strVal.Contains(",") ?
                                                    $"\"{strVal}\"" :
                                                    strVal
                                                )
                                            )
                                        );
                                    }
                                }
                                while (reader.NextResult());
                            }

                            csvInput.Seek(0, SeekOrigin.Begin);
                            input.Dispose();
                            input = csvInput;
                        }
                    }
                    catch (Exception e)
                    {
                        input.Seek(0, SeekOrigin.Begin);
                    }

                    using (var reader = new StreamReader(input))
                    {
                        string data = reader.ReadToEnd();
                        var inputCacheId = Formatik.GetRepeatableBase64HashCode(data);
                        var cacheTask = CacheInputAsync(_userId, inputCacheId, data);

                        (InputFormat InputFormat, int Records) inputDetails;
                        try
                        {
                            inputDetails = Formatik.GetInputFormat(data, user.MaxRecordCount ?? 1000);
                        }
                        catch (UnsupportedFormatException)
                        {
                            return InputUpload.GetError(ErrorCode.UnsupportedFormat, "Unsupported Format");
                        }

                        cacheTask.Wait();

                        return new InputUpload()
                        {
                            InputCacheId = inputCacheId,
                            Input = data.Length > configuration.FileUploadMaxResultSize ? data.Substring(0, configuration.FileUploadMaxResultSize ?? 64000) + "..." : data,
                            Truncated = data.Length > configuration.FileUploadMaxResultSize,
                            InputFormat = inputDetails.InputFormat.ToString(),
                            Size = (int)input.Length,
                            Records = inputDetails.Records
                        };
                    }
                }
                finally
                {
                    input.Dispose();
                }
            }
        }


        // PUT api/values
        /// <summary>
        /// Evaluates a format based on an input and example
        /// </summary>
        /// <param name="id">The User ID</param>
        /// <param name="data">JObject, expected to have:
        /// name            string
        /// input           string (either input or inputCacheId or both)
        /// inputCacheId    string (either input or inputCacheId or both)
        /// example         string
        /// temporary       Boolean (optional)
        /// </param>
        /// <returns></returns>
        [HttpPut("{userId}")]
        [HttpPost("{userId}/evaluate")]
        [EnableCors("FullCors")]
        public Format Evaluate(string userId, [FromBody]EvaluateData data)
        {
            if (!ObjectId.TryParse(userId, out var _userId))
            {
                logger.LogInformation($"User not found or not active - {userId}");
                return Format.GetError(ErrorCode.UserNotFound, "User not found or not active");
            }

            if (string.IsNullOrEmpty(data.Name))
            {
                logger.LogInformation($"Expected \"name\" parameter");
                return Format.GetError(ErrorCode.MissingParameters, "Expected \"name\" parameter");
            }

            if (string.IsNullOrEmpty(data.Input) && string.IsNullOrEmpty(data.InputCacheId))
            {
                logger.LogInformation("Expected at either \"input\" or \"inputCacheId\" parameters, or both.");
                return Format.GetError(ErrorCode.MissingParameters, "Expected at either \"input\" or \"inputCacheId\" parameters, or both.");
            }

            var inputCacheId = data.InputCacheId;

            if (string.IsNullOrEmpty(data.Example))
            {
                logger.LogInformation($"Expected \"example\" parameter");
                return Format.GetError(ErrorCode.MissingParameters, $"Expected \"example\" parameter");
            }

            var db = Common.GetDB(configuration.DbConnection);

            API.User user;
            Format existingFormat;
            Task addToCacheTask = null;

            using (var asyncQueriesCancelationTokenSource = new CancellationTokenSource())
            {
                var userQuery = GetUserAsync(_userId, asyncQueriesCancelationTokenSource.Token);

                if (string.IsNullOrEmpty(inputCacheId))
                    inputCacheId = Formatik.GetRepeatableBase64HashCode(data.Input);

                // getting the cached input will be executed asyncroniously. We don't need the result from it 
                // until we try to run the evaluation
                var getCachedInputQuery = string.IsNullOrEmpty(data.Input) ?
                    GetCachedInputAsync(_userId, inputCacheId, asyncQueriesCancelationTokenSource.Token) :
                    null;

                var existingFormatQuery = db
                    .GetCollection<Format>("Formats")
                    .FindAsync(
                        Builders<Format>.Filter.And(
                            Builders<Format>.Filter.Eq(f => f.UserId, _userId),
                            Builders<Format>.Filter.Eq(f => f.Name, data.Name)),
                        null,
                        asyncQueriesCancelationTokenSource.Token)
                    .ContinueWith(cursor => cursor.Result
                        .ToEnumerable(asyncQueriesCancelationTokenSource.Token)
                        .FirstOrDefault(f => f.Formatik.Example == data.Example));

                user = userQuery.Result;
                if (user == null)
                {
                    asyncQueriesCancelationTokenSource.Cancel();
                    logger.LogInformation($"User not found or not active - {userId}");
                    return Format.GetError(ErrorCode.UserNotFound, "User not found or not active");
                }

                var cachedInput = getCachedInputQuery != null ? getCachedInputQuery.Result : null;
                if (cachedInput != null)
                {
                    // we found our cached input - great
                    data.Input = getCachedInputQuery.Result;
                }
                else if (getCachedInputQuery != null)
                {
                    // we could not find the cached input - return error
                    asyncQueriesCancelationTokenSource.Cancel();
                    logger.LogInformation($"Input \"{data.InputCacheId}\" is no longer cached. Please resubmit input.");
                    return Format.GetError(ErrorCode.InputCacheNotFound, $"Input \"{data.InputCacheId}\" is no longer cached. Please resubmit input.");
                }
                else
                {
                    // new input - cache it asyncronously, don't wait for result
                    addToCacheTask = CacheInputAsync(_userId, inputCacheId, data.Input);
                }

                existingFormat = existingFormatQuery.Result;
            }

            if (existingFormat != null)
            {
                var inputHash = Formatik.GetRepeatableHashCode(data.Input);
                var exampleHash = Formatik.GetRepeatableHashCode(data.Example);

                if ((existingFormat.Formatik.InputHash == inputHash) ||
                    existingFormat.Formatik.ExampleHash == exampleHash)
                {
                    existingFormat.InputCacheId = inputCacheId;
                    return existingFormat;
                }
                else
                {
                    // we found a format with this name, however the input and example don't match 
                    // the current parameters - update the existing doc
                    // Async operation - dont wait for completion
                    db.GetCollection<Format>("Formats")
                        .UpdateOneAsync(
                            Builders<Format>.Filter.And(
                                Builders<Format>.Filter.Eq(f => f.UserId, _userId),
                                Builders<Format>.Filter.Eq(f => f.Name, data.Name)),
                            Builders<Format>.Update
                                .Set(f => f.Formatik.InputHash, inputHash)
                                .Set(f => f.Formatik.Example, data.Example)
                                .Set(f => f.Formatik.ExampleHash, exampleHash)
                                .Set(f => f.LastUpdated, DateTime.Now));
                }
            }

            BsonFormatik format;
            try
            {
                format = new BsonFormatik(data.Input, data.Example, user.MaxRecordCount ?? 1000);
            }
            catch (FormatikException e)
            {
                logger.LogInformation(e.Message);
                return Format.GetError(ErrorCode.EvaluationError, e.Message);
            }

            // search by format hash
            existingFormat = db
                .GetCollection<Format>("Formats")
                .Find(Builders<Format>.Filter.And(
                    Builders<Format>.Filter.Eq(f => f.UserId, user._id),
                    Builders<Format>.Filter.Eq(f => f.Formatik.Hash, format.Hash)))
                .ToEnumerable()
                .FirstOrDefault(f => format == f.Formatik);

            if (existingFormat != null)
            {
                // We found a Format with same hash, however if we got to this point it means it has a different name
                // Update the name of the existing document
                // Async operation - don't wait for completion
                if (existingFormat.Name != data.Name)
                {
                    db.GetCollection<Format>("Formats")
                        .UpdateOneAsync(
                            Builders<Format>.Filter.And(
                                Builders<Format>.Filter.Eq(f => f.UserId, user._id),
                                Builders<Format>.Filter.Eq(f => f.Formatik.Hash, format.Hash)),
                            Builders<Format>.Update
                                .Set(f => f.Name, data.Name)
                                .Set(f => f.LastUpdated, DateTime.Now));
                }

                existingFormat.InputCacheId = inputCacheId;
                existingFormat.InputSize = data.Input.Length;
                existingFormat.InputRecords = format.InputRecords;
                return existingFormat;
            }

            if (addToCacheTask != null)
                addToCacheTask.Wait();

            // If we've reached this point there is no entry in the DB matching this format - insert one
            var now = DateTime.Now;

            var newFormat = new Format()
            {
                _id = ObjectId.GenerateNewId(),
                UserId = user._id,
                Name = data.Name,
                Created = now,
                Formatik = format,
                InputCacheId = inputCacheId,
                InputSize = data.Input.Length,
                InputRecords = format.InputRecords,
                Temporary = data.Temporary ? (DateTime?)now : null
            };

            // no need to wait for save, assume it does
            db.GetCollection<Format>("Formats")
                .InsertOneAsync(newFormat);

            return newFormat;
        }

        [HttpGet("{userId}/{formatId}")]
        [EnableCors("FullCors")]
        public Format GetFormat(string userId, string formatId)
        {
            if (!ObjectId.TryParse(userId, out var _userId))
            {
                logger.LogInformation($"User not found or not active - {userId}");
                return Format.GetError(ErrorCode.UserNotFound, "User not found or not active");
            }

            if (!ObjectId.TryParse(formatId, out var _formatId))
            {
                logger.LogInformation($"Invalid formatId - {formatId}");
                return Format.GetError(ErrorCode.InvalidFormatId, "Invalid formatId");
            }

            using (var asyncQueriesCancelationTokenSource = new CancellationTokenSource())
            {
                var userQuery = GetUserAsync(_userId, asyncQueriesCancelationTokenSource.Token);

                var db = Common.GetDB(configuration.DbConnection);

                var formatQuery = db
                    .GetCollection<Format>("Formats")
                    .FindAsync(
                        Builders<Format>.Filter.And(
                            Builders<Format>.Filter.Eq(f => f.UserId, _userId),
                            Builders<Format>.Filter.Eq(f => f._id, _formatId)),
                        null,
                        asyncQueriesCancelationTokenSource.Token)
                    .ContinueWith(cursor => cursor.Result
                        .FirstOrDefaultAsync(asyncQueriesCancelationTokenSource.Token)
                        .Result);

                var user = userQuery.Result;
                if (user == null)
                {
                    asyncQueriesCancelationTokenSource.Cancel();
                    logger.LogInformation($"User not found or not active - {userId}");
                    return Format.GetError(ErrorCode.UserNotFound, "User not found or not active");
                }

                return formatQuery.Result ?? Format.GetError(ErrorCode.InvalidFormatId, "Unable to find format {formatId}");
            }
        }

        [HttpPost("{userId}/{formatId}")]
        [EnableCors("FullCors")]
        public API.Process Process(string userId, string formatId, [FromBody]ProcessData data)
        {
            if (!ObjectId.TryParse(userId, out var _userId))
            {
                logger.LogInformation($"User not found or not active - {userId}");
                return API.Process.GetError(ErrorCode.UserNotFound, "User not found or not active");
            }

            if (!ObjectId.TryParse(formatId, out var _formatId))
            {
                logger.LogInformation($"Invalid formatId - {formatId}");
                return API.Process.GetError(ErrorCode.InvalidFormatId, "Invalid formatId");
            }

            if (string.IsNullOrEmpty(data.Input) && string.IsNullOrEmpty(data.InputCacheId))
            {
                logger.LogInformation("Expected at either \"input\" or \"inputCacheId\" parameters, or both.");
                return API.Process.GetError(ErrorCode.MissingParameters, "Expected at either \"input\" or \"inputCacheId\" parameters, or both.");
            }

            var inputCacheId = data.InputCacheId;

            User user;
            Format format;
            Task addToCacheTask = null;

            using (var asyncQueriesCancelationTokenSource = new CancellationTokenSource())
            {
                var userQuery = GetUserAsync(_userId, asyncQueriesCancelationTokenSource.Token);

                if (string.IsNullOrEmpty(inputCacheId))
                    inputCacheId = Formatik.GetRepeatableBase64HashCode(data.Input);

                // getting the cached input will be executed asyncroniously. We don't need the result from it 
                // until we try to run the evaluation
                var getCachedInputQuery = string.IsNullOrEmpty(data.Input) ?
                    GetCachedInputAsync(_userId, inputCacheId, asyncQueriesCancelationTokenSource.Token) :
                    null;

                var db = Common.GetDB(configuration.DbConnection);

                var formatQuery = db
                    .GetCollection<Format>("Formats")
                    .FindAsync(
                        Builders<Format>.Filter.And(
                            Builders<Format>.Filter.Eq(f => f.UserId, _userId),
                            Builders<Format>.Filter.Eq(f => f._id, _formatId)),
                        null,
                        asyncQueriesCancelationTokenSource.Token)
                    .ContinueWith((cursorTask) =>
                    {
                        return cursorTask.Result.FirstOrDefaultAsync(asyncQueriesCancelationTokenSource.Token).Result;
                    });

                user = userQuery.Result;
                if (user == null)
                {
                    asyncQueriesCancelationTokenSource.Cancel();
                    logger.LogInformation($"User not found or not active - {userId}");
                    return API.Process.GetError(ErrorCode.UserNotFound, "User not found or not active");
                }

                var cachedInput = getCachedInputQuery != null ? getCachedInputQuery.Result : null;
                if (cachedInput != null)
                {
                    // we found our cached input - great
                    data.Input = getCachedInputQuery.Result;
                }
                else if (getCachedInputQuery != null)
                {
                    // we could not find the cached input - return error
                    asyncQueriesCancelationTokenSource.Cancel();
                    logger.LogInformation($"Input \"{data.InputCacheId}\" is no longer cached. Please resubmit input.");
                    return API.Process.GetError(ErrorCode.InputCacheNotFound, $"Input \"{data.InputCacheId}\" is no longer cached. Please resubmit input.");
                }
                else
                {
                    // new input - cache it asyncronously, don't wait for result
                    addToCacheTask = CacheInputAsync(_userId, inputCacheId, data.Input);
                }

                format = formatQuery.Result;
            }

            if (format == null)
            {
                logger.LogInformation($"Unable to find format {formatId}");
                return API.Process.GetError(ErrorCode.InvalidFormatId, $"Unable to find format {formatId}");
            }

            using (var outputStream = new MemoryStream())
            {
                int processed;
                int inputSize;
                int maxRecordCount = user.MaxRecordCount ?? 1000;

                using (var inputStream = new MemoryStream(Encoding.Unicode.GetBytes(data.Input)))
                {
                    inputSize = (int)inputStream.Length;
                    processed = format.Formatik.Formatik.Process(inputStream, outputStream, Encoding.Unicode, maxRecordCount);
                }

                outputStream.Seek(0, SeekOrigin.Begin);

                if (addToCacheTask != null)
                    addToCacheTask.Wait();

                using (var reader = new StreamReader(outputStream))
                {
                    return new Process()
                    {
                        FormatId = _formatId.ToString(),
                        Name = format.Name,
                        Result = reader.ReadToEnd(),
                        InputSize = inputSize,
                        ProcessedRecords = processed,
                        Trunkated = processed >= maxRecordCount,
                        InputCacheId = inputCacheId
                    };
                }
            }
        }

        // DELETE
        [HttpGet("{id}/delete/{formatId}")]
        [HttpPost("{id}/delete/{formatId}")]
        [HttpDelete("{id}/{formatId}")]
        [EnableCors("FullCors")]
        public Delete Delete(string id, string formatId)
        {
            if (!ObjectId.TryParse(formatId, out var formatIdObj))
            {
                logger.LogInformation($"Invalid formatId - {formatId}");
                return API.Delete.GetError(ErrorCode.InvalidFormatId, "Invalid formatId");
            }

            var db = Common.GetDB(configuration.DbConnection);

            var user = GetUser(id);

            if (user.Status != "OK")
                return API.Delete.GetError(ErrorCode.UserNotFound, user.Error);

            var result = db
                .GetCollection<Format>("Formats")
                .DeleteOne(Builders<Format>.Filter.And(
                    Builders<Format>.Filter.Eq(f => f.UserId, user._id),
                    Builders<Format>.Filter.Eq(f => f._id, formatIdObj)));

            return new Delete()
            {
                FormatId = formatIdObj.ToString(),
                Deleted = result.DeletedCount == 1
            };
        }

        [HttpGet("test-exception")]
        [EnableCors("FullCors")]
        public void TestException(string id)
        {
            var f = new Formatik();
            if (f.Version == "") ;
            throw new Exception("Test Exception");
        }
    }
}
