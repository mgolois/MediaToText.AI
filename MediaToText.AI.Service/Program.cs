using CommandLine;
using MediaToText.AI.Business;
using MediaToText.AI.Business.Services;
using Microsoft.Azure.ServiceBus;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToText.AI.Service
{
    class Program
    {
        private static IConfiguration configuration;
        private static BlobService blobService;
        private static SpeechConfig speechConfig;
        private static TableService tableService;
        private static Options processOpt;
        private static List<Task> LogTasks = new List<Task>();

        private static string Now => DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");

        static int Main(string[] args)
        {
            Console.WriteLine("Service started");
            var ret = -1;

            Parser.Default.ParseArguments<Options>(args)
                  .WithParsed(options =>
                  {
                      processOpt = options;
                      configuration = GetConfig();
                      blobService = new BlobService(configuration["BlobStorage:ConnectionString"]);
                      speechConfig = SpeechConfig.FromSubscription(configuration["SpeechConfig:Key"], configuration["SpeechConfig:Region"]);
                      speechConfig.SpeechRecognitionLanguage = configuration["SpeechConfig:Language"];
                      tableService = new TableService(configuration["BlobStorage:ConnectionString"]);
                      LogTasks.Add(Log("Succesfully parsed cmd line args!"));

                      try
                      {
                          LogTasks.Add(Log("Succesfully collected configurations!"));

                          UpdateMediaInfo(true).GetAwaiter().GetResult();
                          ProcessMessagesAsync().GetAwaiter().GetResult();
                          UpdateMediaInfo(false).GetAwaiter().GetResult();
                          LogTasks.Add(Log("END!"));
                          ret = 0;
                      }
                      catch (Exception ex)
                      {
                          LogTasks.Add(Log($"{ex}"));
                          UpdateMediaInfo(null).GetAwaiter().GetResult();
                      }
                      finally
                      {
                          Task.WaitAll(LogTasks.ToArray());
                      }
                  })
                  .WithNotParsed(errors =>
                  {
                      Console.Error.WriteLine($"{Now} -Error occurred!");
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

        static async Task ProcessAudio(string recordId, string fileName)
        {
            var source = new TaskCompletionSource<int>();
            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output.txt");
            File.WriteAllText(outputPath, "");

            using (var audioInput = AudioConfig.FromWavFileInput(fileName))
            {
                using (var basicRecognizer = new SpeechRecognizer(speechConfig, audioInput))
                {
                    LogTasks.Add(Log("Now processing audio with speech recignizer"));
                    basicRecognizer.Recognized += async (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Result.Text))
                        {
                            var task1 = File.AppendAllTextAsync(outputPath, e.Result.Text);
                            var task2 = tableService.AddEntity(new TranscriptionDetail
                            {
                                RowKey = Guid.NewGuid().ToString(),
                                PartitionKey = recordId,
                                Duration = e.Result.Duration.TotalMilliseconds.ToString(),
                                Sentence = e.Result.Text,
                                StartTime = e.Result.OffsetInTicks.ToString()
                            });
                            await Task.WhenAll(task1, task2);
                        }
                    };
                    basicRecognizer.Canceled += (sender, e) =>
                    {
                        LogTasks.Add(Log($"CancellationReason: {e.Reason}. ErrorDetails: {e.ErrorDetails}."));
                        source.TrySetResult(0);
                    };
                    basicRecognizer.SessionStopped += (sender, e) =>
                    {
                        LogTasks.Add(Log($"Session Stop event: {e}"));
                        source.TrySetResult(0);
                    };

                    await basicRecognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
                    await source.Task.ConfigureAwait(false);
                    LogTasks.Add(Log("Audio processing DONE"));
                    await basicRecognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);

                }
            }
            File.Delete(fileName);
        }

        static async Task ProcessMessagesAsync()
        {
            var fileBytes = await blobService.DownloadBytes(configuration["BlobStorage:InputContainerName"], processOpt.FileName);
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), processOpt.FileName);
            Console.WriteLine($"{Now} -File downloaded from Blob, fileName='{filePath}', length={fileBytes.Length}");
            await File.WriteAllBytesAsync(filePath, fileBytes);
            await ProcessAudio(processOpt.RecordId, filePath);

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
