using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace MediaToText.AI.Business
{
    public class Log : TableEntity
    {
        public string LogMessage { get; set; }
        public Log()
        {
        }
        public Log(string message, string partitionKey)
        {
            LogMessage = message;
            Timestamp = DateTimeOffset.Now;
            RowKey = Guid.NewGuid().ToString();
            PartitionKey = partitionKey;
        }
    }
}
