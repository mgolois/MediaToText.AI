using CommandLine;

namespace MediaToText.AI.Service
{
    public class Options
    {
        [Option('f', "filename", Required = true)]
        public string FileName { get; set; }

        [Option('r', "recordid", Required = true)]
        public string RecordId { get; set; }
        
        [Option('p', "partition", Required = true)]
        public string Partition { get; set; }
    }
}
