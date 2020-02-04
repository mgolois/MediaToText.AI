using MediaToText.AI.Business.Services.AzureTableStorage.TableQueryAsync;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Threading.Tasks;

namespace MediaToText.AI.Business.Services
{
    public interface ITableService
    {
        Task AddEntity<T>(T entity) where T : TableEntity;
        Task<List<T>> GetEntities<T>(string partitionKey = "", int? take = null, bool? orderByAsc = true) where T : TableEntity, new();
        Task AddEntities<T>(IEnumerable<T> entities) where T : TableEntity;
        Task<T> GetEntity<T>(string rowKey, string partitionKey) where T : TableEntity, new();
        Task UpdateEntity<T>(T entity) where T : TableEntity, new();
    }
    public class TableService : ITableService
    {
        private CloudTableClient tableClient;
        public TableService(string connectionString)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            tableClient = storageAccount.CreateCloudTableClient();
        }

        public async Task AddEntity<T>(T entity) where T : TableEntity
        {
            var tableName = typeof(T).Name;
            CloudTable table = tableClient.GetTableReference(tableName);
            TableOperation insertOperation = TableOperation.Insert(entity);
            await table.ExecuteAsync(insertOperation);
        }

        public async Task AddEntities<T>(IEnumerable<T> entities) where T : TableEntity
        {
            var tableName = typeof(T).Name;
            CloudTable table = tableClient.GetTableReference(tableName);
            TableBatchOperation batchOperation = new TableBatchOperation();
            foreach (var item in entities)
            {
                batchOperation.Add(TableOperation.Insert(item));
            }
            await table.ExecuteBatchAsync(batchOperation);
        }

        public async Task<List<T>> GetEntities<T>(string partitionKey = "", int? take = null, bool? orderByAsc = true) where T : TableEntity, new()
        {
            var tableName = typeof(T).Name;
            CloudTable table = tableClient.GetTableReference(tableName);
            var query = new TableQuery<T>();
            if (!string.IsNullOrWhiteSpace(partitionKey))
            {
                query = query.Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey));
            }
            if (take.HasValue)
            {
                query = query.Take(take);
            }
            return await table.ExecuteQueryAsync(query, orderByAsc);
        }

        public async Task<T> GetEntity<T>(string rowKey, string partitionKey) where T : TableEntity, new()
        {
            var tableName = typeof(T).Name;
            CloudTable table = tableClient.GetTableReference(tableName);
            TableOperation operation = TableOperation.Retrieve<T>(partitionKey, rowKey);
            var result = await table.ExecuteAsync(operation);
            return (T)result.Result;
        }

        public async Task UpdateEntity<T>(T entity) where T : TableEntity, new()
        {
            entity.ETag = "*";
            var tableName = typeof(T).Name;
            CloudTable table = tableClient.GetTableReference(tableName);
            TableOperation operation = TableOperation.Replace(entity);
            await table.ExecuteAsync(operation);

        }

    }

}

