using System;
using System.Collections.Generic;
using System.Text;

namespace MediaToText.AI.Business.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Table;

    namespace AzureTableStorage.TableQueryAsync
    {
        public static class TableQueryExtensions
        {
            /// <summary>
            /// Initiates an asynchronous operation to execute a query and return all the results.
            /// </summary>
            /// <param name="tableQuery">A Microsoft.WindowsAzure.Storage.Table.TableQuery representing the query to execute.</param>
            /// <param name="ct">A System.Threading.CancellationToken to observe while waiting for a task to complete.</param>
            public async static Task<List<TElement>> ExecuteQueryAsync<TElement>(this CloudTable table, TableQuery<TElement> tableQuery, bool? orderAsc) where TElement: TableEntity, new()
            {
                var nextQuery = tableQuery;
                var continuationToken = default(TableContinuationToken);
                var results = new List<TElement>();

                do
                {
                    //Execute the next query segment async.
                    var queryResult = await table.ExecuteQuerySegmentedAsync(nextQuery, continuationToken);

                    //Set exact results list capacity with result count.
                    results.Capacity += queryResult.Results.Count;

                    //Add segment results to results list.
                    results.AddRange(queryResult.Results);

                    continuationToken = queryResult.ContinuationToken;

                    //Continuation token is not null, more records to load.
                    if (continuationToken != null && tableQuery.TakeCount.HasValue)
                    {
                        //Query has a take count, calculate the remaining number of items to load.
                        var itemsToLoad = tableQuery.TakeCount.Value - results.Count;

                        //If more items to load, update query take count, or else set next query to null.
                        nextQuery = itemsToLoad > 0
                            ? 
                            tableQuery.Take(itemsToLoad) //.AsTableQuery()
                            : null;
                    }

                } while (continuationToken != null && nextQuery != null);

                IEnumerable<TElement> finalresult = results;
                if (orderAsc.HasValue)
                {
                        finalresult = orderAsc.Value ? finalresult.OrderBy(c => c.Timestamp) 
                                                     : finalresult.OrderByDescending(c => c.Timestamp);
                }
                return finalresult.ToList();
            }
        }
    }
}
