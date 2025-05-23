using System.Runtime.CompilerServices;
using EfficientDynamoDb.Configs;
using EfficientDynamoDb.Converters;
using EfficientDynamoDb.Operations.GetItem;

namespace EfficientDynamoDb.Internal.Operations.GetItem
{
    internal sealed class GetItemHttpContent : GetItemHttpContentBase<GetItemRequest>
    {
        private readonly string _pkName;
        private readonly string? _skName;
        
        public GetItemHttpContent(GetItemRequest request, ITableNameFormatter? tableNameFormatter, string pkName, string? skName) : base(request, tableNameFormatter)
        {
            _pkName = pkName;
            _skName = skName;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void WritePrimaryKey(in DdbWriter writer)
        {
            writer.JsonWriter.WritePropertyName("Key");
            writer.JsonWriter.WriteStartObject();
            
            writer.JsonWriter.WritePropertyName(_pkName);
            Request.Key!.PartitionKeyValue.Write(writer.JsonWriter);

            if (Request.Key.SortKeyValue != null)
            {
                writer.JsonWriter.WritePropertyName(_skName!);
                Request.Key.SortKeyValue.Value.Write(writer.JsonWriter);
            }
            
            writer.JsonWriter.WriteEndObject();
        }
    }
}