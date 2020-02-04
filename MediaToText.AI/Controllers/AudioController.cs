using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaToText.AI.Business.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace MediaToText.AI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AudioController : ControllerBase
    {

        private IBlobService blobService;
        private IConfiguration configuration;
        private IMemoryCache memoryCache;
        public AudioController(IBlobService blobService, IConfiguration configuration, IMemoryCache memoryCache)
        {
            this.blobService = blobService;
            this.configuration=configuration;
            this.memoryCache = memoryCache;
    }
        [HttpGet]
        [Route("{fileName}")]
        public async Task<IActionResult> Get(string fileName)
        {
           var bytes =  memoryCache.Get<byte[]>(fileName);
            if (bytes == null)
            {
                bytes = await blobService.DownloadBytes("input", fileName);
                memoryCache.Set(fileName, bytes);
            }
            var contentType = "audio/wav";
            var result = new FileContentResult(bytes, contentType);
            result.FileDownloadName = fileName;
            
            Response.Headers.Add("Accept-Ranges", "bytes");
            return result;
        }
    }
}