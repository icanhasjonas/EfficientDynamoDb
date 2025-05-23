using EfficientDynamoDb.DocumentModel;
using EfficientDynamoDb.Exceptions;
using EfficientDynamoDb.Internal;
using EfficientDynamoDb.Internal.Extensions;
using EfficientDynamoDb.Internal.Operations.BatchGetItem;
using EfficientDynamoDb.Internal.Operations.BatchWriteItem;
using EfficientDynamoDb.Internal.Operations.DeleteItem;
using EfficientDynamoDb.Internal.Operations.DescribeTable;
using EfficientDynamoDb.Internal.Operations.GetItem;
using EfficientDynamoDb.Internal.Operations.PutItem;
using EfficientDynamoDb.Internal.Operations.Query;
using EfficientDynamoDb.Internal.Operations.Scan;
using EfficientDynamoDb.Internal.Operations.TransactGetItems;
using EfficientDynamoDb.Internal.Operations.TransactWriteItems;
using EfficientDynamoDb.Internal.Operations.UpdateItem;
using EfficientDynamoDb.Internal.Reader;
using EfficientDynamoDb.Operations.BatchGetItem;
using EfficientDynamoDb.Operations.BatchWriteItem;
using EfficientDynamoDb.Operations.DeleteItem;
using EfficientDynamoDb.Operations.DescribeTable;
using EfficientDynamoDb.Operations.DescribeTable.Models.Enums;
using EfficientDynamoDb.Operations.GetItem;
using EfficientDynamoDb.Operations.PutItem;
using EfficientDynamoDb.Operations.Query;
using EfficientDynamoDb.Operations.Scan;
using EfficientDynamoDb.Operations.TransactGetItems;
using EfficientDynamoDb.Operations.TransactWriteItems;
using EfficientDynamoDb.Operations.UpdateItem;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EfficientDynamoDb
{
    public class DynamoDbLowLevelContext : IDynamoDbLowLevelContext
    {
        internal DynamoDbContextConfig Config { get; }
        internal HttpApi Api { get; }
        private static readonly ConcurrentDictionary<string, Task<(string Pk, string? Sk)>> KeysCache = new ConcurrentDictionary<string, Task<(string Pk, string? Sk)>>();
        private readonly DynamoDbLowLevelPartiQLContext _partiQLContext;
        public IDynamoDbLowLevelPartiQLContext PartiQL => _partiQLContext;
        
        internal DynamoDbLowLevelContext(DynamoDbContextConfig config, HttpApi api)
        {
            Api = api;
            Config = config;
            _partiQLContext = new DynamoDbLowLevelPartiQLContext(config, api);
        }

        public async Task<GetItemResponse> GetItemAsync(GetItemRequest request, CancellationToken cancellationToken = default)
        {
            using var httpContent = await BuildHttpContentAsync(request).ConfigureAwait(false);
            return await GetItemInternalAsync(httpContent, cancellationToken).ConfigureAwait(false);
        }
        
        public async Task<BatchGetItemResponse> BatchGetItemAsync(BatchGetItemRequest request, CancellationToken cancellationToken = default)
        {
            using var httpContent = new BatchGetItemHttpContent(request, Config.TableNameFormatter);
            
            using var response = await Api.SendAsync(Config, httpContent, cancellationToken).ConfigureAwait(false);
            var result = await ReadDocumentAsync(response, BatchGetItemParsingOptions.Instance, cancellationToken).ConfigureAwait(false);

            return BatchGetItemResponseParser.Parse(result!);
        }
        
        public async Task<BatchWriteItemResponse> BatchWriteItemAsync(BatchWriteItemRequest request, CancellationToken cancellationToken = default)
        {
            using var httpContent = new BatchWriteItemHttpContent(request, Config.TableNameFormatter);
            
            using var response = await Api.SendAsync(Config, httpContent, cancellationToken).ConfigureAwait(false);
            var result = await ReadDocumentAsync(response, BatchWriteItemParsingOptions.Instance, cancellationToken).ConfigureAwait(false);

            return BatchWriteItemResponseParser.Parse(result!);
        }

        public async Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken = default)
        {
            using var httpContent = new QueryHttpContent(request, Config.TableNameFormatter);
            
            using var response = await Api.SendAsync(Config, httpContent, cancellationToken).ConfigureAwait(false);
            var result = await ReadDocumentAsync(response, QueryParsingOptions.Instance, cancellationToken).ConfigureAwait(false);

            return QueryResponseParser.Parse(result!);
        }

        public async Task<ScanResponse> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
        {
            using var httpContent = new ScanHttpContent(request, Config.TableNameFormatter);

            using var response = await Api.SendAsync(Config, httpContent, cancellationToken).ConfigureAwait(false);
            var result = await ReadDocumentAsync(response, QueryParsingOptions.Instance, cancellationToken).ConfigureAwait(false);

            return ScanResponseParser.Parse(result!);
        }
        
        public async Task<TransactGetItemsResponse> TransactGetItemsAsync(TransactGetItemsRequest request, CancellationToken cancellationToken = default)
        {
            using var httpContent = new TransactGetItemsHttpContent(request, Config.TableNameFormatter);

            using var response = await Api.SendAsync(Config, httpContent, cancellationToken).ConfigureAwait(false);
            var result = await ReadDocumentAsync(response, TransactGetItemsParsingOptions.Instance, cancellationToken).ConfigureAwait(false);

            return TransactGetItemsResponseParser.Parse(result!);
        }

        public async Task<PutItemResponse> PutItemAsync(PutItemRequest request, CancellationToken cancellationToken = default)
        {
            using var httpContent = new PutItemHttpContent(request, Config.TableNameFormatter);
            
            using var response = await Api.SendAsync(Config, httpContent, cancellationToken).ConfigureAwait(false);
            var result = await ReadDocumentAsync(response, PutItemParsingOptions.Instance, cancellationToken).ConfigureAwait(false);

            return PutItemResponseParser.Parse(result);
        }
        
        public async Task<UpdateItemResponse> UpdateItemAsync(UpdateItemRequest request, CancellationToken cancellationToken = default)
        {
            using var httpContent = await BuildHttpContentAsync(request).ConfigureAwait(false);
            
            using var response = await Api.SendAsync(Config, httpContent, cancellationToken).ConfigureAwait(false);
            var result = await ReadDocumentAsync(response, UpdateItemParsingOptions.Instance, cancellationToken).ConfigureAwait(false);

            return UpdateItemResponseParser.Parse(result);
        }

        public async Task<DeleteItemResponse> DeleteItemAsync(DeleteItemRequest request, CancellationToken cancellationToken = default)
        {
            var (pkName, skName) = request.Key!.HasKeyNames
                ? (request.Key.PartitionKeyName!, request.Key.SortKeyName)
                : await GetKeyNamesAsync(request.TableName).ConfigureAwait(false);

            using var httpContent = new DeleteItemHttpContent(request, pkName, skName, Config.TableNameFormatter);
            
            using var response = await Api.SendAsync(Config, httpContent, cancellationToken).ConfigureAwait(false);
            var result = await ReadDocumentAsync(response, PutItemParsingOptions.Instance, cancellationToken).ConfigureAwait(false);

            return DeleteItemResponseParser.Parse(result);
        }
        
        public async Task<TransactWriteItemsResponse> TransactWriteItemsAsync(TransactWriteItemsRequest request, CancellationToken cancellationToken = default)
        {
            using var httpContent = new TransactWriteItemsHttpContent(request, Config.TableNameFormatter);
            
            using var response = await Api.SendAsync(Config, httpContent, cancellationToken).ConfigureAwait(false);
            var result = await ReadDocumentAsync(response, TransactWriteItemsParsingOptions.Instance, cancellationToken).ConfigureAwait(false);

            return TransactWriteItemsResponseParser.Parse(result);
        }

        public T ToObject<T>(Document document) where T : class => document.ToObject<T>(Config.Metadata);

        public Document ToDocument<T>(T entity) where T : class => entity.ToDocument(Config.Metadata);

        private async ValueTask<GetItemResponse> GetItemInternalAsync(HttpContent httpContent, CancellationToken cancellationToken = default)
        {
            using var response = await Api.SendAsync(Config, httpContent, cancellationToken).ConfigureAwait(false);
            var result = await ReadDocumentAsync(response, GetItemParsingOptions.Instance, cancellationToken).ConfigureAwait(false);

            // TODO: Consider removing root dictionary
            return GetItemResponseParser.Parse(result!);
        }

        private async ValueTask<HttpContent> BuildHttpContentAsync(GetItemRequest request)
        {
            if (request.Key!.HasKeyNames)
                return new GetItemHttpContent(request, Config.TableNameFormatter, request.Key.PartitionKeyName!, request.Key.SortKeyName!);

            var (remotePkName, remoteSkName) = await GetKeyNamesAsync(request.TableName);
            return new GetItemHttpContent(request, Config.TableNameFormatter, remotePkName, remoteSkName!);
        }
        
        private async ValueTask<HttpContent> BuildHttpContentAsync(UpdateItemRequest request)
        {
            if (request.Key!.HasKeyNames)
                return new UpdateItemHttpContent(request, Config.TableNameFormatter, request.Key.PartitionKeyName!, request.Key.SortKeyName!);

            var (remotePkName, remoteSkName) = await GetKeyNamesAsync(request.TableName);
            return new UpdateItemHttpContent(request, Config.TableNameFormatter, remotePkName, remoteSkName!);
        }
        
        private async ValueTask<(string Pk, string? Sk)> GetKeyNamesAsync(string tableName)
        {
            if (KeysCache.TryGetValue(tableName, out var task) && (task.IsCompletedSuccessfully || !task.IsCompleted))
                return await task.ConfigureAwait(false);

            return await KeysCache.AddOrUpdate(tableName, CreateKeyNamesTaskAsync,
                (table, t) => t.IsCompletedSuccessfully || !t.IsCompleted
                    ? task!
                    : CreateKeyNamesTaskAsync(table)).ConfigureAwait(false);
            
            async Task<(string Pk, string? Sk)> CreateKeyNamesTaskAsync(string table)
            {
                var response = await Api.SendAsync<DescribeTableResponse>(Config, new DescribeTableRequestHttpContent(Config.TableNameFormatter, tableName))
                    .ConfigureAwait(false);

                var keySchema = response.Table.KeySchema;
                return (keySchema.First(x => x.KeyType == KeyType.HASH).AttributeName,
                    keySchema.FirstOrDefault(x => x.KeyType == KeyType.RANGE)?.AttributeName);
            }
        }

        internal static async ValueTask<Document?> ReadDocumentAsync(HttpResponseMessage response, IParsingOptions options, CancellationToken cancellationToken = default)
        {
            await using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            var expectedCrc = GetExpectedCrc(response);
            var result = await DdbJsonReader.ReadAsync(responseStream, options, expectedCrc.HasValue, cancellationToken).ConfigureAwait(false);
            
            if (expectedCrc.HasValue && expectedCrc.Value != result.Crc)
                throw new ChecksumMismatchException();

            return result.Value;
        }
        
        internal static uint? GetExpectedCrc(HttpResponseMessage response)
        {
            if (!response.Content.Headers.ContentLength.HasValue)
                return null;
            
            if (response.Headers.TryGetValues("x-amz-crc32", out var crcHeaderValues) && uint.TryParse(crcHeaderValues.FirstOrDefault(), out var expectedCrc))
                return expectedCrc;

            return null;
        }
    }
}