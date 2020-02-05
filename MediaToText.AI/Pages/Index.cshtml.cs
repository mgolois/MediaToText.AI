using MediaToText.AI.Business;
using MediaToText.AI.Business.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToText.AI.Pages
{
    [DisableRequestSizeLimit]
    public class IndexModel : PageModel
    {
        private IBlobService blobService;
        private IConfiguration configuration;
        private ITableService tableService;
        private BatchClient batchClient;
        public List<TranscriptionDetail> TranscriptionDetails { get; set; }
        public List<MediaInfo> UpdatedRequests { get; set; }
        public List<Log> LogMessages { get; set; }

        public IndexModel(IBlobService blobService, ITableService tableService, BatchClient batchClient, IConfiguration configuration)
        {
            this.blobService = blobService;
            this.configuration = configuration;
            this.tableService = tableService;
            this.batchClient = batchClient;
        }

        [BindProperty]
        public IFormFile FileToUpload { get; set; }
        [BindProperty]
        public bool CreatePool { get; set; }
        public async Task OnGetAsync(string id = null, string fileName = "", bool showLog = false)
        {
            if (showLog)
            {
                ViewData["ShowLog"] = showLog;
                ViewData["RecordId"] = id;
                LogMessages = await tableService.GetEntities<Log>(id);
            }
            else if (!string.IsNullOrWhiteSpace(id))
            {
                ViewData["RecordId"] = id;
                ViewData["FileName"] = fileName;
                TranscriptionDetails = await tableService.GetEntities<TranscriptionDetail>(id);
            }

            else
            {
                UpdatedRequests = await tableService.GetEntities<MediaInfo>(string.Empty, orderByAsc: false);
            }
        }


       
        public async Task<IActionResult> OnPostAsync()
        {
            var logs = new List<Log>();
            var fileName = $"{ DateTime.Now.Ticks}_{FileToUpload.FileName}";
            var entity = new MediaInfo
            {
                Timestamp = DateTimeOffset.Now,
                FileName = fileName,
                RowKey = Guid.NewGuid().ToString(),
                PartitionKey = DateTime.Now.ToString("MMddyyy"),
                SubmissionTime = DateTimeOffset.Now
            };
            logs.Add(new Log("File ready to  upload to blob", entity.RowKey));

            var stream = FileToUpload.OpenReadStream();
            var url = await blobService.UploadFile(configuration["BlobStorage:InputContainerName"], fileName, stream);
            logs.Add(new Log($"File '{fileName}' uploaded to blob", entity.RowKey));

            entity.InputUri = url;
            await tableService.AddEntity(entity);
            logs.Add(new Log("Entity record created ", entity.RowKey));

            if (CreatePool)
            {
                logs.Add(new Log("User chose to create a NEW POOL and submit task", entity.RowKey));
                await CreatePoolSubmitTask(entity);
                logs.Add(new Log("Pool created and tasks submitted", entity.RowKey));
            }
            else
            {
                logs.Add(new Log("User chose to use EXISTING POOL and submit task", entity.RowKey));
                await SubmitTaskToExistingJob(entity);
                logs.Add(new Log("Tasks submitted", entity.RowKey));
            }

            await tableService.AddEntities(logs);

            return Redirect("/?id=" + entity.RowKey + "&fileName=" + entity.FileName);
        }

        private CloudTask CreateTask(MediaInfo entity)
        {
           
            var cmdLine = $"cmd /c %AZ_BATCH_APP_PACKAGE_{configuration["BatchService:AppName"]}#{configuration["BatchService:AppVersion"]}%\\{configuration["BatchService:AppExe"]} " +
                      $"--filename \"{entity.FileName}\" --recordid \"{entity.RowKey}\" --partition \"{entity.PartitionKey}\"";


            var containerUrl = blobService.GetContainerUrl(configuration["BlobStorage:OutputContainerName"]);

            var task = new CloudTask(DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff"), cmdLine);
            task.UserIdentity = new UserIdentity(new AutoUserSpecification(elevationLevel: ElevationLevel.Admin, scope: AutoUserScope.Pool));
            task.OutputFiles = new List<OutputFile>
            {
                new OutputFile(@"output.txt",
                              new OutputFileDestination(new OutputFileBlobContainerDestination(containerUrl, task.Id)),
                              new OutputFileUploadOptions(OutputFileUploadCondition.TaskCompletion)),
                new OutputFile(@"..\std*.txt",
                              new OutputFileDestination(new OutputFileBlobContainerDestination(containerUrl, task.Id)),
                              new OutputFileUploadOptions(OutputFileUploadCondition.TaskCompletion)),
            };
                
            return task;
        }

        private async Task SubmitTaskToExistingJob(MediaInfo entity)
        {
            var task = CreateTask(entity);

            await batchClient.JobOperations.AddTaskAsync(configuration["BatchService:JobId"], task);
        }

        private async Task CreatePoolSubmitTask(MediaInfo entity)
        {
            var appReference = new ApplicationPackageReference()
            {
                ApplicationId = configuration["BatchService:AppName"],
                Version = configuration["BatchService:AppVersion"]
            };

            var nameSuffix = Guid.NewGuid().ToString();
            var poolSpec = new PoolSpecification
            {
                ApplicationPackageReferences = new List<ApplicationPackageReference>() { appReference },
                TargetDedicatedComputeNodes = 1,
                TargetLowPriorityComputeNodes = 0,
                CloudServiceConfiguration = new CloudServiceConfiguration("5", "*"),
                VirtualMachineSize = "large",
                DisplayName = $"PoolName_{nameSuffix}"
            };

            var poolInfo = new PoolInformation
            {
                PoolId = $"PoolID_{nameSuffix}",
                AutoPoolSpecification = new AutoPoolSpecification()
                {
                    AutoPoolIdPrefix = "Pool",
                    PoolSpecification = poolSpec,
                    KeepAlive = false,
                    PoolLifetimeOption = PoolLifetimeOption.Job,
                }
            };

            var job = batchClient.JobOperations.CreateJob($"Job_{nameSuffix}", poolInfo);
            job.OnAllTasksComplete = OnAllTasksComplete.TerminateJob;

            await job.CommitAsync();

            var task = CreateTask(entity);
            task.ApplicationPackageReferences = new List<ApplicationPackageReference> { appReference };

            await batchClient.JobOperations.AddTaskAsync($"Job_{nameSuffix}", task);

        }
    }
}
