namespace LogViewerApi.Models
{
    public class ZipFileInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }

    public class LogFile
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; } // "central" or "process"
    }

    public class ZipExtractionRequest
    {
        public string ZipFilePath { get; set; }
    }

    public class LogContentRequest
    {
        public string LogFilePath { get; set; }
    }

    public class LogsResponse
    {
        public List<LogFile> CentralLogs { get; set; } = new List<LogFile>();
        public List<LogFile> ProcessLogs { get; set; } = new List<LogFile>();
    }

    public class InitialExtractionResponse : LogsResponse
    {
        public string SessionId { get; set; }
        public int InitialProgress { get; set; } = 0;
    }

    public class ExtractionProgressResponse
    {
        public int InitialProgress { get; set; }
        public int NestedProgress { get; set; }
        public bool IsComplete { get; set; }
        public List<LogFile> NewLogs { get; set; } = new List<LogFile>();
    }

    public class ExtractionProgress
    {
        public string SessionId { get; set; }
        public string TempFolderPath { get; set; }
        public DateTime StartTime { get; set; } = DateTime.Now;
        public int InitialProgress { get; set; } = 0;
        public int NestedProgress { get; set; } = 0;
        public bool IsComplete { get; set; } = false;
        public int TotalInitialEntries { get; set; } = 0;
        public int TotalNestedZips { get; set; } = 0;
        public List<LogFile> NewlyExtractedLogs { get; set; } = new List<LogFile>();
    }

    public class LiveLogItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime LastModified { get; set; }
        public long Size { get; set; } // For files only
    }

    public class LiveLogExtractionRequest
    {
        public string ZipFilePath { get; set; }
    }

    public class LiveLogResponse
    {
        public List<LiveLogItem> Items { get; set; } = new List<LiveLogItem>();
    }
    public class ExtractionSession
    {
        public string SessionId { get; set; }
        public string TempFolderPath { get; set; }
        public string OriginalZipPath { get; set; }
        public DateTime LastAccessTime { get; set; } = DateTime.Now;
        public bool IsLiveLog { get; set; }
        public LogsResponse LogsData { get; set; } = new LogsResponse();

        // Update the last access time
        public void UpdateAccessTime()
        {
            LastAccessTime = DateTime.Now;
        }
    }
    public class DirectoryBrowseRequest
    {
        public string DirectoryPath { get; set; }
    }

    public class DirectoryContentsResponse
    {
        public string CurrentPath { get; set; }
        public List<FileSystemItem> Items { get; set; } = new List<FileSystemItem>();
    }

    public class FileSystemItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; } // "Directory" or "File"
        public DateTime LastModified { get; set; }
        public long Size { get; set; } // For files only
        public bool IsZipFile { get; set; }
    }
}