using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaToText.AI.Business.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MediaToText.AI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentController : ControllerBase
    {
        private ITableService tableService;
        private IWordProcessorService wordService;
        public DocumentController(ITableService tableService, IWordProcessorService wordProcessorService)
        {
            this.tableService = tableService;
            this.wordService = wordProcessorService;
        }
        [Route("{id}")]
        public async Task<ActionResult> Get(string id)
        {
            var transcriptionDetails = await tableService.GetEntities<TranscriptionDetail>(id);
            var bytes = wordService.CreateDocument(transcriptionDetails.Select(c => c.Sentence).ToList());
            return File(bytes, "application/octet-stream", $"File_{DateTime.Now.Ticks}.docx");
        }
    }
}