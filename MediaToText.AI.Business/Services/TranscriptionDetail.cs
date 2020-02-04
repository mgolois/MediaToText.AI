using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace MediaToText.AI.Business.Services
{
    public class TranscriptionDetail:TableEntity
    {
        public string Sentence { get; set; }
        public string StartTime { get; set; }
        public string Duration { get; set; }
        [IgnoreProperty]
        public TimeSpan DurationTimeSpan => TimeSpan.FromMilliseconds(double.Parse(Duration));
        [IgnoreProperty]
        public TimeSpan StartTimeSpan => new TimeSpan(long.Parse(StartTime));
        [IgnoreProperty]
        public string DurationTimeSpanString => DurationTimeSpan.ToString("hh\\:mm\\:ss");
        [IgnoreProperty]
        public string StartTimeSpanString => StartTimeSpan.ToString("hh\\:mm\\:ss");

    }
}
