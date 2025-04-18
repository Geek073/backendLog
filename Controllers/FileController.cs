using Microsoft.AspNetCore.Mvc;
using LogViewerApi.Models;
using LogViewerApi.Services;

namespace LogViewerApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileController : ControllerBase
    {
        private readonly FileService _fileService;
        private readonly ILogger<FileController> _logger;

        public FileController(FileService fileService, ILogger<FileController> logger)
        {
            _fileService = fileService;
            _logger = logger;
        }

        [HttpGet("zips")]
        public ActionResult<IEnumerable<ZipFileInfo>> GetZipFiles()
        {
            try
            {
                var zipFiles = _fileService.GetZipFiles();
                return Ok(zipFiles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving zip files");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("extract")]
        public ActionResult<InitialExtractionResponse> ExtractZipFile([FromBody] ZipExtractionRequest request)
        {
            try
            {
                var response = _fileService.ExtractInitialZip(request.ZipFilePath);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting zip file");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("progress/{sessionId}")]
        public ActionResult<ExtractionProgressResponse> GetExtractionProgress(string sessionId)
        {
            try
            {
                var progress = _fileService.GetExtractionProgress(sessionId);
                return Ok(progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving extraction progress");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("logcontent")]
        public ActionResult<string> GetLogContent([FromBody] LogContentRequest request)
        {
            try
            {
                var content = _fileService.GetLogContent(request.LogFilePath);
                return Ok(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving log content");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("livelogs")]
        public ActionResult<LiveLogResponse> GetLiveLogs()
        {
            try
            {
                var logs = _fileService.GetLiveLogs();
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving live logs");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("livelogs/extract")]
        public ActionResult<InitialExtractionResponse> ExtractLiveLogZip([FromBody] LiveLogExtractionRequest request)
        {
            try
            {
                var response = _fileService.ExtractLiveLogZip(request.ZipFilePath);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting live log zip file");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("cleanup")]
        public ActionResult CleanupTempFiles()
        {
            try
            {
                _fileService.CleanupTempFiles();
                return Ok("Cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}