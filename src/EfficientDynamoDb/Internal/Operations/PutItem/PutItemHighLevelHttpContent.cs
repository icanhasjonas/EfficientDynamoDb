using System.Threading.Tasks;
using EfficientDynamoDb.Converters;
using EfficientDynamoDb.Internal.Extensions;
using EfficientDynamoDb.Internal.Operations.Shared;
using EfficientDynamoDb.Operations.Query;

namespace EfficientDynamoDb.Internal.Operations.PutItem
{
    internal sealed class PutItemHighLevelHttpContent : DynamoDbHttpContent
    {
        private readonly DynamoDbContext _context;
        private readonly BuilderNode? _node;

        public PutItemHighLevelHttpContent(DynamoDbContext context, BuilderNode? node)
            : base("DynamoDB_20120810.PutItem")
        {
            _context = context;
            _node = node;
        }

        protected override async ValueTask WriteDataAsync(DdbWriter ddbWriter)
        {
            var writer = ddbWriter.JsonWriter;
            writer.WriteStartObject();

            var currentNode = _node;
            var writeState = 0;

            string? tableName = null;
            
            while (currentNode != null)
            {
                switch (currentNode.Type)
                {
                    case BuilderNodeType.Item:
                    {
                        if (writeState.IsBitSet(NodeBits.Item))
                            break;
                        
                        var itemNode = ((ItemNode) currentNode);

                        tableName = itemNode.EntityClassInfo.TableName;
                        
                        writer.WritePropertyName("Item");
                        await ddbWriter.WriteEntityAsync(itemNode.EntityClassInfo, itemNode.Value).ConfigureAwait(false);

                        writeState = writeState.SetBit(NodeBits.Item);
                        break;
                    }
                    case BuilderNodeType.Condition:
                    {
                        if (writeState.IsBitSet(NodeBits.Condition))
                            break;
                        
                        ddbWriter.WriteConditionExpression(((ConditionNode) currentNode).Value, _context.Config.Metadata);

                        writeState = writeState.SetBit(NodeBits.Condition);
                        break;
                    }
                    case BuilderNodeType.TableName:
                        ((TableNameNode) currentNode).WriteTableName(in ddbWriter, ref writeState, _context.Config.TableNameFormatter);
                        break;
                    default:
                    {
                        currentNode.WriteValue(in ddbWriter, ref writeState);
                        break;
                    }
                }
                
                currentNode = currentNode.Next;
            }
            
            if(!writeState.IsBitSet(NodeBits.TableName) && tableName != null)
                writer.WriteTableName(_context.Config.TableNameFormatter, tableName);

            writer.WriteEndObject();
        }
    }
}