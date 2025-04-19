using Microsoft.AspNetCore.Mvc;
using LogViewerApi.Models;
using LogViewerApi.Services;

namespace LogViewerApi.Controllers
{
    private readonly SessionManager _sessionManager;
    [ApiController]
    [Route("api/[controller]")]
    public class FileController : ControllerBase
    {
        private readonly FileService _fileService;
        private readonly ILogger<FileController> _logger;

        public FileController(FileService fileService, ILogger<FileController> logger)
        {
            _fileService = fileService;
            _sessionManager = sessionManager;
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
        [HttpGet("session/{sessionId}")]
        public ActionResult<ExtractionSession> GetSession(string sessionId)
        {
            try
            {
                var session = _sessionManager.GetSession(sessionId);
                if (session == null)
                {
                    return NotFound("Session not found");
                }

                // Update last access time
                session.UpdateAccessTime();
                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving session");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("sessions")]
        public ActionResult<List<string>> GetAllSessions()
        {
            try
            {
                var sessions = _sessionManager.GetAllSessionIds();
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sessions");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("browse")]
        public ActionResult<DirectoryContentsResponse> BrowseDirectory([FromBody] DirectoryBrowseRequest request)
        {
            try
            {
                var response = _fileService.BrowseDirectory(request.DirectoryPath);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing directory");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("extract-in-directory")]
        public ActionResult<InitialExtractionResponse> ExtractZipInDirectory([FromBody] ZipExtractionRequest request)
        {
            try
            {
                var response = _fileService.ExtractZipInDirectory(request.ZipFilePath);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting zip in directory");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("livelogs/directory")]
        public ActionResult<LiveLogResponse> GetLiveLogDirectoryContents([FromQuery] string path = null)
        {
            try
            {
                var response = _fileService.GetLiveLogDirectoryContents(path);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving live log directory contents");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}