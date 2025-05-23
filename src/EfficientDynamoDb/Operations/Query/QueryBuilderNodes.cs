using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using EfficientDynamoDb.Configs;
using EfficientDynamoDb.Converters;
using EfficientDynamoDb.FluentCondition;
using EfficientDynamoDb.FluentCondition.Core;
using EfficientDynamoDb.Internal.Extensions;
using EfficientDynamoDb.Internal.Metadata;
using EfficientDynamoDb.Operations.Shared;

namespace EfficientDynamoDb.Operations.Query
{
    // TODO: Refactor and split classes in files
    
    internal enum BuilderNodeType : byte
    {
        Primitive,
        KeyExpression,
        FilterExpression,
        Item,
        Condition,
        AddUpdate,
        SetUpdate,
        RemoveUpdate,
        DeleteUpdate,
        PrimaryKey,
        ProjectedAttributes,
        BatchGetTableNode,
        GetItemNode,
        TransactDeleteItemNode,
        TransactConditionCheckNode,
        TransactUpdateItemNode,
        TransactPutItemNode,
        BatchItems,
        TableName
    }

    internal static class NodeBits
    {
        public const int IndexName = 1 << 0;
        public const int ConsistentRead = 1 << 1;
        public const int Limit = 1 << 2;
        public const int ProjectedAttributes = 1 << 3;
        public const int ReturnConsumedCapacity = 1 << 4;
        public const int Select = 1 << 5;
        public const int BackwardSearch = 1 << 6;
        public const int ReturnValues = 1 << 7;
        public const int ReturnItemCollectionMetrics = 1 << 8;
        public const int PaginationToken = 1 << 9;
        public const int PrimaryKey = 1 << 10;
        public const int Item = 1 << 11;
        public const int Condition = 1 << 12;
        public const int Segment = 1 << 13;
        public const int TotalSegments = 1 << 14;
        public const int ClientRequestToken = 1 << 15;
        public const int ReturnValuesOnConditionCheckFailure = 1 << 16;
        public const int TableName = 1 << 17;
    }
    
    internal abstract class BuilderNode : IEnumerable<BuilderNode>
    {
        public BuilderNode? Next { get; }

        public virtual BuilderNodeType Type => BuilderNodeType.Primitive;

        protected BuilderNode(BuilderNode? next) => Next = next;

        public abstract void WriteValue(in DdbWriter writer, ref int state);

        public IEnumerator<BuilderNode> GetEnumerator() => new NodeEnumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new NodeEnumerator(this);
            
        private struct NodeEnumerator : IEnumerator<BuilderNode?>
        {
            private readonly BuilderNode? _start;
            private  BuilderNode? _next;
            
            public BuilderNode? Current { get; private set; }

            public NodeEnumerator(BuilderNode? start) : this()
            {
                _next = _start = start;
            }

            public bool MoveNext()
            {
                if (_next == null)
                    return false;

                Current = _next;
                _next = _next.Next;
                return true;
            }

            public void Reset()
            {
                _next = _start;
                Current = null;
            }

            object? IEnumerator.Current => Current;

            public void Dispose()
            {
            }
        }
    }

    internal abstract class BuilderNode<TValue> : BuilderNode
    {
        public TValue Value { get; }

        protected BuilderNode(TValue value, BuilderNode? next) : base(next) => Value = value;
    }

    internal sealed class IndexNameNode : BuilderNode<string>
    {
        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            if (state.IsBitSet(NodeBits.IndexName))
                return;
            
            writer.JsonWriter.WriteString("IndexName", Value);

            state = state.SetBit(NodeBits.IndexName);
        }

        public IndexNameNode(string value, BuilderNode? next) : base(value, next)
        {
        }
    }

    internal sealed class KeyExpressionNode : BuilderNode<FilterBase>
    {
        public override BuilderNodeType Type => BuilderNodeType.KeyExpression;

        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            throw new NotImplementedException();
        }

        public KeyExpressionNode(FilterBase value, BuilderNode? next) : base(value, next)
        {
        }
    }

    internal sealed class ConsistentReadNode : BuilderNode<bool>
    {
        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            if (state.IsBitSet(NodeBits.ConsistentRead))
                return;
            
            writer.JsonWriter.WriteBoolean("ConsistentRead", Value);

            state = state.SetBit(NodeBits.ConsistentRead);
        }

        public ConsistentReadNode(bool value, BuilderNode? next) : base(value, next)
        {
        }
    }

    internal sealed class LimitNode : BuilderNode<int> 
    {
        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            if (state.IsBitSet(NodeBits.Limit))
                return;
            
            writer.JsonWriter.WriteNumber("Limit", Value);
            
            state = state.SetBit(NodeBits.Limit);
        }

        public LimitNode(int value, BuilderNode? next) : base(value, next)
        {
        }
    }

    internal sealed class ProjectedAttributesNode : BuilderNode
    {
        public Type ProjectionType { get; }
        
        public IReadOnlyList<Expression>? Expressions { get; }
        
        public override BuilderNodeType Type => BuilderNodeType.ProjectedAttributes;

        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            throw new NotImplementedException();
        }

        public ProjectedAttributesNode(Type projectionType, IReadOnlyList<Expression>? expressions, BuilderNode? next) : base(next)
        {
            ProjectionType = projectionType;
            Expressions = expressions;
        }
    }

    internal sealed class ReturnConsumedCapacityNode : BuilderNode<ReturnConsumedCapacity>
    {
        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            if (state.IsBitSet(NodeBits.ReturnConsumedCapacity))
                return;
            
            if (Value != ReturnConsumedCapacity.None)
                writer.JsonWriter.WriteReturnConsumedCapacity(Value);
            
            state = state.SetBit(NodeBits.ReturnConsumedCapacity);
        }

        public ReturnConsumedCapacityNode(ReturnConsumedCapacity value, BuilderNode? next) : base(value, next)
        {
        }
    }

    internal sealed class SelectNode : BuilderNode<Select> 
    {
        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            if (state.IsBitSet(NodeBits.Select))
                return;
            
            var selectValue = Value switch
            {
                Select.AllAttributes => "ALL_ATTRIBUTES",
                Select.AllProjectedAttributes => "ALL_PROJECTED_ATTRIBUTES",
                Select.Count => "COUNT",
                Select.SpecificAttributes => "SPECIFIC_ATTRIBUTES",
                _ => "ALL_ATTRIBUTES"
            };
            
            writer.JsonWriter.WriteString("Select", selectValue);
            
            state = state.SetBit(NodeBits.Select);
        }

        public SelectNode(Select value, BuilderNode? next) : base(value, next)
        {
        }
    }

    internal sealed class BackwardSearchNode : BuilderNode<bool> 
    {
        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            if (state.IsBitSet(NodeBits.BackwardSearch))
                return;
            
            if (Value)
                writer.JsonWriter.WriteBoolean("ScanIndexForward", false);
            
            state = state.SetBit(NodeBits.BackwardSearch);
        }

        public BackwardSearchNode(bool value, BuilderNode? next) : base(value, next)
        {
        }
    }

    internal sealed class FilterExpressionNode : BuilderNode<FilterBase>
    {
        public override BuilderNodeType Type => BuilderNodeType.FilterExpression;
        
        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            throw new NotImplementedException();
        }

        public FilterExpressionNode(FilterBase value, BuilderNode? next) : base(value, next)
        {
        }
    }

    internal abstract class EntityNodeBase : BuilderNode
    {
        public DdbClassInfo EntityClassInfo { get; }

        protected EntityNodeBase(DdbClassInfo entityClassInfo, BuilderNode? next) : base(next)
        {
            EntityClassInfo = entityClassInfo;
        }
    }
    
    internal sealed class ItemNode : EntityNodeBase
    {
        public override BuilderNodeType Type => BuilderNodeType.Item;

        public object Value { get; }
        
        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            throw new NotImplementedException();
        }

        public ItemNode(object value, DdbClassInfo entityClassInfo, BuilderNode? next) : base(entityClassInfo, next)
        {
            Value = value;
        }
    }
    
    internal sealed class ItemTypeNode : BuilderNode<object>
    {
        public override BuilderNodeType Type => BuilderNodeType.Item;

        public Type ItemType { get; }

        public ItemTypeNode(object value, Type itemType, BuilderNode? next) : base(value, next)
        {
            ItemType = itemType;
        }

        public override void WriteValue(in DdbWriter writer, ref int state) => throw new NotImplementedException();
    }
    
    internal sealed class ReturnValuesNode : BuilderNode<ReturnValues>
    {
        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            if (state.IsBitSet(NodeBits.ReturnValues))
                return;
            
            if (Value != ReturnValues.None)
                writer.JsonWriter.WriteReturnValues(Value);

            state = state.SetBit(NodeBits.ReturnValues);
        }

        public ReturnValuesNode(ReturnValues value, BuilderNode? next) : base(value, next)
        {
        }
    }

    internal sealed class ReturnItemCollectionMetricsNode : BuilderNode<ReturnItemCollectionMetrics> 
    {
        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            if (state.IsBitSet(NodeBits.ReturnItemCollectionMetrics))
                return;
            
            if (Value != ReturnItemCollectionMetrics.None)
                writer.JsonWriter.WriteReturnItemCollectionMetrics(Value);
            
            state = state.SetBit(NodeBits.ReturnItemCollectionMetrics);
        }

        public ReturnItemCollectionMetricsNode(ReturnItemCollectionMetrics value, BuilderNode? next) : base(value, next)
        {
        }
    }
    
    internal sealed class ConditionNode : BuilderNode<FilterBase>
    {
        public override BuilderNodeType Type => BuilderNodeType.Condition;
        
        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            throw new NotImplementedException();
        }

        public ConditionNode(FilterBase value, BuilderNode? next) : base(value, next)
        {
        }
    }

    internal sealed class PaginationTokenNode : BuilderNode<string?>
    {
        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            if (state.IsBitSet(NodeBits.PaginationToken))
                return;
            
            if(Value != null)
                writer.WritePaginationToken(Value);
            
            state = state.SetBit(NodeBits.PaginationToken);
        }

        public PaginationTokenNode(string? value, BuilderNode? next) : base(value, next)
        {
        }
    }

    internal sealed class UpdateAttributeNode : BuilderNode<UpdateBase>
    {
        public override BuilderNodeType Type { get; }

        public UpdateAttributeNode(UpdateBase value, BuilderNodeType type, BuilderNode? next) : base(value, next)
        {
            Type = type;
        }

        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            throw new NotImplementedException();
        }
    }
    
    internal abstract class PrimaryKeyNodeBase : BuilderNode
    {
        public abstract void Write(in DdbWriter writer, DdbClassInfo classInfo, ref int state);
        
        public abstract void WriteValueWithoutKey(in DdbWriter writer, DdbClassInfo classInfo);
        
        public override BuilderNodeType Type => BuilderNodeType.PrimaryKey;

        protected PrimaryKeyNodeBase(BuilderNode? next) : base(next)
        {
        }
    }

    internal sealed class PartitionAndSortKeyNode<TPk, TSk> : PrimaryKeyNodeBase
    {
        private TPk _pk;
        private TSk _sk;

        public PartitionAndSortKeyNode(TPk pk, TSk sk, BuilderNode? next) : base(next)
        {
            _pk = pk;
            _sk = sk;
        }

        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            throw new NotImplementedException();
        }

        public override void Write(in DdbWriter writer, DdbClassInfo classInfo, ref int state)
        {
            if (state.IsBitSet(NodeBits.PrimaryKey))
                return;
            
            writer.JsonWriter.WritePropertyName("Key");
            WriteValueWithoutKey(in writer, classInfo);

            state = state.SetBit(NodeBits.PrimaryKey);
        }
        
        public override void WriteValueWithoutKey(in DdbWriter writer, DdbClassInfo classInfo)
        {
            writer.JsonWriter.WriteStartObject();

            var pkAttribute = (DdbPropertyInfo<TPk>) classInfo.PartitionKey!;
            writer.JsonWriter.WritePropertyName(pkAttribute.AttributeName);
            pkAttribute.Converter.Write(in writer, ref _pk);
            
            var skAttribute = (DdbPropertyInfo<TSk>)classInfo.SortKey!;
            writer.JsonWriter.WritePropertyName(skAttribute.AttributeName);
            skAttribute.Converter.Write(in writer, ref _sk);
            
            writer.JsonWriter.WriteEndObject();
        }
    }

    internal sealed class PartitionKeyNode<TPk> : PrimaryKeyNodeBase
    {
        private TPk _pk;

        public PartitionKeyNode(TPk pk, BuilderNode? next) : base(next)
        {
            _pk = pk;
        }

        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            throw new NotImplementedException();
        }

        public override void Write(in DdbWriter writer, DdbClassInfo classInfo, ref int state)
        {
            if (state.IsBitSet(NodeBits.PrimaryKey))
                return;
            
            writer.JsonWriter.WritePropertyName("Key");
            WriteValueWithoutKey(in writer, classInfo);
            
            state = state.SetBit(NodeBits.PrimaryKey);
        }
        
        public override void WriteValueWithoutKey(in DdbWriter writer, DdbClassInfo classInfo)
        {
            writer.JsonWriter.WriteStartObject();

            var pkAttribute = (DdbPropertyInfo<TPk>) classInfo.PartitionKey!;
            writer.JsonWriter.WritePropertyName(pkAttribute.AttributeName);
            pkAttribute.Converter.Write(in writer, ref _pk);

            writer.JsonWriter.WriteEndObject();
        }
    }
    
    internal abstract class EntityPrimaryKeyNodeBase : EntityNodeBase
    {
        public override BuilderNodeType Type => BuilderNodeType.PrimaryKey;

        public abstract void WriteValueWithoutKey(in DdbWriter writer);

        protected EntityPrimaryKeyNodeBase(DdbClassInfo entityClassInfo, BuilderNode? next) : base(entityClassInfo, next)
        {
        }
    }
    
    internal sealed class EntityPartitionAndSortKeyNode<TPk, TSk> : EntityPrimaryKeyNodeBase
    {
        private TPk _pk;
        private TSk _sk;

        public EntityPartitionAndSortKeyNode(DdbClassInfo entityClassInfo, TPk pk, TSk sk, BuilderNode? next) : base(entityClassInfo, next)
        {
            _pk = pk;
            _sk = sk;
        }

        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            writer.JsonWriter.WritePropertyName("Key");
            WriteValueWithoutKey(in writer);
        }

        public override void WriteValueWithoutKey(in DdbWriter writer)
        {
            writer.JsonWriter.WriteStartObject();

            var pkAttribute = (DdbPropertyInfo<TPk>) EntityClassInfo.PartitionKey!;
            writer.JsonWriter.WritePropertyName(pkAttribute.AttributeName);
            pkAttribute.Converter.Write(in writer, ref _pk);
            
            var skAttribute = (DdbPropertyInfo<TSk>)EntityClassInfo.SortKey!;
            writer.JsonWriter.WritePropertyName(skAttribute.AttributeName);
            skAttribute.Converter.Write(in writer, ref _sk);
            
            writer.JsonWriter.WriteEndObject();
        }
    }

    internal sealed class EntityPartitionKeyNode<TPk> : EntityPrimaryKeyNodeBase
    {
        private TPk _pk;
        
        public EntityPartitionKeyNode(DdbClassInfo entityClassInfo, TPk pk, BuilderNode? next) : base(entityClassInfo, next)
        {
            _pk = pk;
        }

        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            writer.JsonWriter.WritePropertyName("Key");
            WriteValueWithoutKey(in writer);
        }

        public override void WriteValueWithoutKey(in DdbWriter writer)
        {
            writer.JsonWriter.WriteStartObject();

            var pkAttribute = (DdbPropertyInfo<TPk>) EntityClassInfo.PartitionKey!;
            writer.JsonWriter.WritePropertyName(pkAttribute.AttributeName);
            pkAttribute.Converter.Write(in writer, ref _pk);
            
            writer.JsonWriter.WriteEndObject();
        }
    }
    
    internal sealed class BatchGetTableNode : BuilderNode<BuilderNode>
    {
        public override BuilderNodeType Type => BuilderNodeType.BatchGetTableNode;
        
        public DdbClassInfo ClassInfo { get; }

        public BatchGetTableNode(DdbClassInfo classInfo, BuilderNode value, BuilderNode? next) : base(value, next)
        {
            ClassInfo = classInfo;
        }

        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            throw new NotImplementedException();
        }
    }

    internal sealed class GetItemNode : BuilderNode<BuilderNode>
    {
        public override BuilderNodeType Type => BuilderNodeType.GetItemNode;
        
        public DdbClassInfo ClassInfo { get; }

        public GetItemNode(DdbClassInfo classInfo, BuilderNode value, BuilderNode? next) : base(value, next)
        {
            ClassInfo = classInfo;
        }

        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            throw new NotImplementedException();
        }
    }

    internal sealed class SegmentNode : BuilderNode<int>
    {
        public SegmentNode(int value, BuilderNode? next) : base(value, next)
        {
        }

        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            if (state.IsBitSet(NodeBits.Segment))
                return;

            writer.JsonWriter.WriteNumber("Segment", Value);
            
            state = state.SetBit(NodeBits.Segment);
        }
    }
    
    internal sealed class TotalSegmentsNode : BuilderNode<int>
    {
        public TotalSegmentsNode(int value, BuilderNode? next) : base(value, next)
        {
        }

        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            if (state.IsBitSet(NodeBits.TotalSegments))
                return;

            writer.JsonWriter.WriteNumber("TotalSegments", Value);
            
            state = state.SetBit(NodeBits.TotalSegments);
        }
    }

    internal sealed class ClientRequestTokenNode : BuilderNode<string>
    {
        public ClientRequestTokenNode(string value, BuilderNode? next) : base(value, next)
        {
        }

        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            if (state.IsBitSet(NodeBits.ClientRequestToken))
                return;

            if(Value != null)
                writer.JsonWriter.WriteString("ClientRequestToken", Value);

            state = state.SetBit(NodeBits.ClientRequestToken);
        }
    }

    internal sealed class ReturnValuesOnConditionCheckFailureNode : BuilderNode<ReturnValuesOnConditionCheckFailure>
    {
        public ReturnValuesOnConditionCheckFailureNode(ReturnValuesOnConditionCheckFailure value, BuilderNode? next) : base(value, next)
        {
        }

        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            if (state.IsBitSet(NodeBits.ReturnValuesOnConditionCheckFailure))
                return;

            if(Value != ReturnValuesOnConditionCheckFailure.None)
                writer.JsonWriter.WriteString("ReturnValuesOnConditionCheckFailure", "ALL_OLD");

            state = state.SetBit(NodeBits.ReturnValuesOnConditionCheckFailure);
        }
    }

    internal sealed class BatchItemsNode<TBuilder> : BuilderNode<IEnumerable<TBuilder>>
    {
        public override BuilderNodeType Type => BuilderNodeType.BatchItems;

        public BatchItemsNode(IEnumerable<TBuilder> value, BuilderNode? next) : base(value, next)
        {
        }

        public override void WriteValue(in DdbWriter writer, ref int state)
        {
            throw new NotImplementedException();
        }
    }

    internal sealed class TableNameNode : BuilderNode<string>
    {
        public override BuilderNodeType Type => BuilderNodeType.TableName;

        public TableNameNode(string value, BuilderNode? next) : base(value, next)
        {
        }

        public override void WriteValue(in DdbWriter writer, ref int state) => throw new NotImplementedException();

        public void WriteTableName(in DdbWriter writer, ref int state, ITableNameFormatter? tableNameFormatter)
        {
            if (state.IsBitSet(NodeBits.TableName))
                return;

            writer.JsonWriter.WriteTableName(tableNameFormatter, Value);

            state = state.SetBit(NodeBits.TableName);
        }
    }
}