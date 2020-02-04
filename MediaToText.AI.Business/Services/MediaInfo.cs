using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace MediaToText.AI.Business.Services
{
    public class MediaInfo : TableEntity
    {
        public string FileName { get; set; }
        public string InputUri { get; set; }
        public string OutputUri { get; set; }
        public DateTimeOffset? SubmissionTime { get; set; }
        public DateTimeOffset? TaskStartTime { get; set; }
        public DateTimeOffset? TaskEndTime { get; set; }
        public DateTimeOffset? TaskErrorTime { get; set; }
        [IgnoreProperty]
        public string Status
        {
            get
            {
                if (!SubmissionTime.HasValue)
                {
                    return "OLD";
                }
                else if (TaskErrorTime.HasValue)
                {
                    return "Failed";
                }
                else if (!TaskStartTime.HasValue)
                {
                    return "Submitted";
                }
                else if (!TaskEndTime.HasValue)
                {
                    return "Started";
                }
                else
                {
                    return "Completed";
                }
            }
        }
        [IgnoreProperty]
        public string FileNameDisplay
        {
            get
            {
                var index = FileName.IndexOf('_');
                var result = FileName.Substring(index + 1);
                if (result.Length > 25)
                {
                    result = "..." + result.Substring(result.Length - 25);
                }

                return result;
            }
        }
        [IgnoreProperty]
        public string SubmissionToStart
        {
            get
            {
                if (SubmissionTime.HasValue && TaskStartTime.HasValue)
                {
                    return (TaskStartTime.Value - SubmissionTime.Value).ToString("hh\\:mm\\:ss");
                }
                return string.Empty;
            }
        }

        [IgnoreProperty]
        public string StartToEnd
        {
            get
            {
                if (TaskEndTime.HasValue && TaskStartTime.HasValue)
                {
                    return (TaskEndTime.Value - TaskStartTime.Value).ToString("hh\\:mm\\:ss");
                }
                return string.Empty;
            }
        }
    }
}
