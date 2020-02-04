using CommandLine;
using MediaToText.AI.Business;
using MediaToText.AI.Business.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToText.AI.ServiceTest
{
    class Program
    {
        private static IConfiguration configuration;
        private static BlobService blobService;
        private static TableService tableService;
        private static Options processOpt;
        private static List<Task> LogTasks = new List<Task>();
        static int Main(string[] args)
        {
            Console.WriteLine("Service started");
            var ret = -1;
            configuration = GetConfig();
            blobService = new BlobService(configuration["BlobStorage:ConnectionString"]);
            tableService = new TableService(configuration["BlobStorage:ConnectionString"]);

            Parser.Default.ParseArguments<Options>(args)
                  .WithParsed(options =>
                  {
                      processOpt = options;
                      try
                      {
                          LogTasks.Add(Log("Succesfully parsed cmd line args!"));
                          UpdateMediaInfo(true).GetAwaiter().GetResult();
                          Thread.Sleep(2000);
                          UpdateMediaInfo(false).GetAwaiter().GetResult();
                          LogTasks.Add(Log("END!"));
                          ret = 0;
                      }
                      finally
                      {
                          Task.WaitAll(LogTasks.ToArray());
                      }
                  })
                  .WithNotParsed(errors =>
                  {
                      Console.Error.WriteLine($"Error occurred!");
                      throw new ArgumentException(string.Join(";", errors));
                  });
            return ret;
        }

        static IConfiguration GetConfig()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            IConfiguration config = builder.Build();
            return config;
        }

        static async Task UpdateMediaInfo(bool? changeStartTime)
        {
            var mediaInfo = await tableService.GetEntity<MediaInfo>(processOpt.RecordId, processOpt.Partition);

            if (!changeStartTime.HasValue)
            {
                mediaInfo.TaskErrorTime = DateTimeOffset.Now;
            }
            else if (changeStartTime.Value)
            {
                mediaInfo.TaskStartTime = DateTimeOffset.Now;
            }
            else
            {
                mediaInfo.TaskEndTime = DateTimeOffset.Now;
            }
            await tableService.UpdateEntity(mediaInfo);
        }

        static Task Log(string message)
        {
            return tableService.AddEntity(new Log(message, processOpt.RecordId));
        }
    }
}
