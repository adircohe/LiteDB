﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LiteDB.Engine
{
    /// <summary>
    /// This class are result from optimization from QueryBuild in QueryAnalyzer. Indicate how engine must run query - there is no more decisions to engine made, must only execute as query was defined
    /// Contains used index and estimate cost to run
    /// </summary>
    internal class QueryPlan
    {
        public QueryPlan(string collection)
        {
            this.Collection = collection;
        }

        /// <summary>
        /// Get collection name (required)
        /// </summary>
        public string Collection { get; set; } = null;

        /// <summary>
        /// Index used on query (required)
        /// </summary>
        public Index Index { get; set; } = null;

        /// <summary>
        /// Index expression that will be used in index (source only)
        /// </summary>
        public string IndexExpression { get; set; } = null;

        /// <summary>
        /// Get index cost (lower is best)
        /// </summary>
        public long IndexCost { get; internal set; } = 0; // not calculated

        /// <summary>
        /// If true, gereate document result only with IndexNode.Key (avoid load all document)
        /// </summary>
        public bool IsIndexKeyOnly { get; set; } = false;

        /// <summary>
        /// List of filters of documents
        /// </summary>
        public List<BsonExpression> Filters { get; set; } = new List<BsonExpression>();

        /// <summary>
        /// List of includes must be done BEFORE filter (it's not optimized but some filter will use this include)
        /// </summary>
        public List<BsonExpression> IncludeBefore { get; set; } = new List<BsonExpression>();

        /// <summary>
        /// List of includes must be done AFTER filter (it's optimized because will include result only)
        /// </summary>
        public List<BsonExpression> IncludeAfter { get; set; } = new List<BsonExpression>();

        /// <summary>
        /// Expression to order by resultset
        /// </summary>
        public OrderBy OrderBy { get; set; } = null;

        /// <summary>
        /// Expression to group by document results
        /// </summary>
        public GroupBy GroupBy { get; set; } = null;

        /// <summary>
        /// Transaformation data before return - if null there is no transform (return document)
        /// </summary>
        public Select Select { get; set; }

        /// <summary>
        /// Get fields name that will be deserialize from disk
        /// </summary>
        public HashSet<string> Fields { get; set; }

        /// <summary>
        /// Limit resultset
        /// </summary>
        public int Limit { get; set; } = int.MaxValue;

        /// <summary>
        /// Skip documents before returns
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// Indicate this query is for update (lock mode = Write)
        /// </summary>
        public bool ForUpdate { get; set; } = false;

        #region Execution Plan

        /// <summary>
        /// Get detail about execution plan for this query definition
        /// </summary>
        public BsonDocument GetExecutionPlan()
        {
            var doc = new BsonDocument
            {
                ["collection"] = this.Collection,
                ["snaphost"] = this.ForUpdate ? "write" : "read",
                ["pipe"] = this.GroupBy == null ? "queryPipe" : "groupByPipe"
            };

            doc["index"] = new BsonDocument
            {
                ["name"] = this.Index.Name,
                ["expr"] = this.IndexExpression,
                ["order"] = this.Index.Order,
                ["mode"] = this.Index.ToString(),
                ["cost"] = (int)this.IndexCost
            };

            doc["lookup"] = new BsonDocument
            {
                ["loader"] = this.Index is IndexVirtual ? "virtual" : (this.IsIndexKeyOnly ? "index" : "document"),
                ["fields"] =
                    this.Fields.Count == 0 ? new BsonValue("$") :
                    (BsonValue)new BsonArray(this.Fields.Select(x => new BsonValue(x))),
            };

            doc["includeBefore"] = this.IncludeBefore.Count == 0 ?
                BsonValue.Null :
                new BsonArray(this.IncludeBefore.Select(x => new BsonValue(x.Source)));

            doc["filters"] = this.Filters.Count == 0 ?
                BsonValue.Null :
                new BsonArray(this.Filters.Select(x => new BsonValue(x.Source)));

            doc["includeAfter"] = this.IncludeAfter.Count == 0 ?
                BsonValue.Null :
                new BsonArray(this.IncludeAfter.Select(x => new BsonValue(x.Source)));

            if (this.GroupBy != null)
            {
                doc["groupBy"] = new BsonDocument
                {
                    ["expr"] = this.GroupBy.Expression.Source,
                    ["order"] = this.GroupBy.Order,
                    ["having"] = this.GroupBy.Having?.Source,
                    ["select"] = this.GroupBy.Select?.Source
                };
            }

            doc["orderBy"] = this.OrderBy == null ?
                BsonValue.Null :
                new BsonDocument
                {
                    ["expr"] = this.OrderBy.Expression.Source,
                    ["order"] = this.OrderBy.Order,
                };

            doc["limit"] = this.Limit;
            doc["offset"] = this.Offset;

            if (this.Select != null)
            {
                doc["select"] = new BsonDocument
                {
                    ["expr"] = this.Select.Expression.Source,
                    ["all"] = this.Select.All
                };
            }

            return doc;
        }

        #endregion
    }
}