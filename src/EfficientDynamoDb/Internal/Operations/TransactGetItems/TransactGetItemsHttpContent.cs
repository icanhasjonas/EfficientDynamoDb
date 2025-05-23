using System.Threading.Tasks;
using EfficientDynamoDb.Configs;
using EfficientDynamoDb.Converters;
using EfficientDynamoDb.Internal.Extensions;
using EfficientDynamoDb.Internal.Operations.Shared;
using EfficientDynamoDb.Operations.Shared;
using EfficientDynamoDb.Operations.TransactGetItems;

namespace EfficientDynamoDb.Internal.Operations.TransactGetItems
{
    internal class TransactGetItemsHttpContent : DynamoDbHttpContent
    {
        private readonly TransactGetItemsRequest _request;
        private readonly ITableNameFormatter? _tableNameFormatter;

        public TransactGetItemsHttpContent(TransactGetItemsRequest request, ITableNameFormatter? tableNameFormatter) : base("DynamoDB_20120810.TransactGetItems")
        {
            _request = request;
            _tableNameFormatter = tableNameFormatter;
        }

        protected override async ValueTask WriteDataAsync(DdbWriter ddbWriter)
        {
            var writer = ddbWriter.JsonWriter;
            writer.WriteStartObject();

            if (_request.ReturnConsumedCapacity != ReturnConsumedCapacity.None)
                writer.WriteReturnConsumedCapacity(_request.ReturnConsumedCapacity);
            
            writer.WritePropertyName("TransactItems");
            
            writer.WriteStartArray();

            foreach (var transactItem in _request.TransactItems)
            {
                writer.WriteStartObject();
                
                writer.WritePropertyName("Get");
                writer.WriteStartObject();
            
                if(transactItem.ExpressionAttributeNames != null)
                    writer.WriteExpressionAttributeNames(transactItem.ExpressionAttributeNames);

                if (transactItem.ProjectionExpression != null)
                    writer.WriteString("ProjectionExpression", transactItem.ProjectionExpression);

                writer.WriteTableName(_tableNameFormatter, transactItem.TableName);
                writer.WritePrimaryKey(transactItem.Key!);
            
                writer.WriteEndObject();
                
                writer.WriteEndObject();

                if (ddbWriter.ShouldFlush)
                    await ddbWriter.FlushAsync().ConfigureAwait(false);
            }
            
            writer.WriteEndArray();
            
            writer.WriteEndObject();
        }
    }
}