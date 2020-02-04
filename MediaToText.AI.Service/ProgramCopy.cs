using MediaToText.AI.Business;
using MediaToText.AI.Business.Services;
using Microsoft.Azure.ServiceBus;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToText.AI.Service
{
    class ProgramCopy
    {
        private static IConfiguration configuration;
        private static QueueClient queueClient;
        private static BlobService blobService;
        private static SpeechConfig speechConfig;
        private static TableService tableService;
        static IConfiguration GetConfig()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            IConfiguration config = builder.Build();
            return config;
        }
        static void MainCopy(string[] args)
        {
            Console.WriteLine("Hello World!");
            configuration = GetConfig();
            blobService = new BlobService(configuration["BlobStorage:ConnectionString"]);
            speechConfig = SpeechConfig.FromSubscription(configuration["SpeechConfig:Key"], configuration["SpeechConfig:Region"]);
            speechConfig.SpeechRecognitionLanguage = configuration["SpeechConfig:Language"];
            tableService = new TableService(configuration["BlobStorage:ConnectionString"]);

            MainAsync().GetAwaiter().GetResult();
            Console.Read();
        }

        static async Task MainAsync()
        {
            queueClient = new QueueClient(configuration["ServiceBus:ConnectionString"], configuration["ServiceBus:QueueName"], ReceiveMode.ReceiveAndDelete);
            var messageHandlerOptions = new MessageHandlerOptions(ExceptionReceivedHandler);
            messageHandlerOptions.AutoComplete = true;

            // Register the function that will process messages
            queueClient.RegisterMessageHandler(ProcessMessagesAsync, messageHandlerOptions);

            Console.Read();

            await queueClient.CloseAsync();
        }
        static async Task ProcessAudio(NotificationDetail detail, string fileName)
        {
            var source = new TaskCompletionSource<int>();

            using (var audioInput = AudioConfig.FromWavFileInput(fileName))
            {
                using (var basicRecognizer = new SpeechRecognizer(speechConfig, audioInput))
                {
                    basicRecognizer.Recognized += async (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Result.Text))
                        {
                            await tableService.AddEntity(new TranscriptionDetail
                            {
                                RowKey = Guid.NewGuid().ToString(),
                                PartitionKey = detail.RecordId,
                                Duration = e.Result.Duration.TotalMilliseconds.ToString(),
                                Sentence = e.Result.Text,
                                StartTime = e.Result.OffsetInTicks.ToString()

                            });
                        }
                    };
                    basicRecognizer.Canceled += (sender, e) => source.TrySetResult(0);
                    basicRecognizer.SessionStopped += (sender, e) => source.TrySetResult(0);

                    await basicRecognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
                    await source.Task.ConfigureAwait(false);
                    await basicRecognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
                   
                }
            }
            File.Delete(fileName);

        }

        static async Task ProcessMessagesAsync(Message message, CancellationToken token)
        {
            var contentJson = Encoding.Default.GetString(message.Body);

            var detail = JsonConvert.DeserializeObject<NotificationDetail>(contentJson);

            var fileBytes = await blobService.DownloadBytes(configuration["BlobStorage:InputContainerName"], detail.FileName);
            string fileName = $"{Guid.NewGuid()}_{detail.FileName}";
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            await File.WriteAllBytesAsync(filePath, fileBytes);
            await ProcessAudio(detail, filePath);
        }



        static Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            var message = $"Message handler encountered an exception {exceptionReceivedEventArgs.Exception}.";
            Console.WriteLine(message);
            //  telemetryClient.TrackException(exceptionReceivedEventArgs.Exception);

            return Task.CompletedTask;
        }
    }
}
