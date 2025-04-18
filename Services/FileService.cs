using System.IO.Compression;
using System.Text.RegularExpressions;
using LogViewerApi.Models;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace LogViewerApi.Services
{
    public class FileService
    {
        private readonly string _databasePath = @"C:\Savelog\Database\";
        private readonly string _tempPath = @"C:\Savelog\Database\Temp\";
        private readonly ILogger<FileService> _logger;
        private readonly ConcurrentDictionary<string, ExtractionProgress> _progressTrackers = new ConcurrentDictionary<string, ExtractionProgress>();
        // HashSet to track processed files to avoid duplicates
        private readonly ConcurrentDictionary<string, HashSet<string>> _processedFiles = new ConcurrentDictionary<string, HashSet<string>>();

        public FileService(ILogger<FileService> logger)
        {
            _logger = logger;

            // Ensure temp directory exists
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
        }

        public List<ZipFileInfo> GetZipFiles()
        {
            try
            {
                var zipFiles = Directory.GetFiles(_databasePath, "*.zip")
                    .Select(path => new ZipFileInfo
                    {
                        Name = Path.GetFileName(path),
                        Path = path
                    })
                    .ToList();

                return zipFiles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting zip files from directory");
                return new List<ZipFileInfo>();
            }
        }

        public InitialExtractionResponse ExtractInitialZip(string zipFilePath)
        {
            var response = new InitialExtractionResponse();
            var sessionId = Guid.NewGuid().ToString();
            var tempFolderPath = Path.Combine(_tempPath, sessionId);

            try
            {
                // Create a unique temp directory for this extraction
                Directory.CreateDirectory(tempFolderPath);

                // Setup progress tracking
                var progress = new ExtractionProgress { SessionId = sessionId, TempFolderPath = tempFolderPath };
                _progressTrackers[sessionId] = progress;

                // Initialize processed files tracker for this session
                _processedFiles[sessionId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Extract only the main zip (non-recursive)
                using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                {
                    // Get total entries for progress tracking
                    progress.TotalInitialEntries = archive.Entries.Count;

                    int entriesProcessed = 0;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destinationPath = Path.Combine(tempFolderPath, entry.FullName);

                        // Create directory if needed
                        string? dirPath = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
                        {
                            Directory.CreateDirectory(dirPath);
                        }

                        // Extract the file if it's not a directory
                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            entry.ExtractToFile(destinationPath, true);
                        }

                        entriesProcessed++;
                        progress.InitialProgress = (int)((entriesProcessed / (double)progress.TotalInitialEntries) * 100);
                    }
                }

                progress.InitialProgress = 100;

                // Find initial log files (central and process)
                response.CentralLogs = FindCentralLogs(tempFolderPath, sessionId);
                response.ProcessLogs = FindProcessLogs(tempFolderPath, sessionId);
                response.SessionId = sessionId;
                response.InitialProgress = 100;

                // Start background extraction of nested zip files
                StartNestedExtractionAsync(zipFilePath, tempFolderPath, sessionId);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting initial zip file: {zipFilePath}");

                // Clean up on error
                if (Directory.Exists(tempFolderPath))
                {
                    try { Directory.Delete(tempFolderPath, true); } catch { }
                }

                // Remove the trackers
                _progressTrackers.TryRemove(sessionId, out _);
                _processedFiles.TryRemove(sessionId, out _);

                return response;
            }
        }

        private async Task StartNestedExtractionAsync(string originalZipPath, string tempFolderPath, string sessionId)
        {
            await Task.Run(() => ProcessNestedZipsAsync(tempFolderPath, sessionId));
        }

        private async Task ProcessNestedZipsAsync(string basePath, string sessionId)
        {
            try
            {
                if (!_progressTrackers.TryGetValue(sessionId, out var progress))
                {
                    _logger.LogWarning($"Session {sessionId} not found for nested extraction");
                    return;
                }

                // Find all nested zip files
                var nestedZipFiles = Directory.GetFiles(basePath, "*.zip", SearchOption.AllDirectories).ToList();
                progress.TotalNestedZips = nestedZipFiles.Count;

                if (nestedZipFiles.Count == 0)
                {
                    progress.NestedProgress = 100;
                    progress.IsComplete = true;
                    return;
                }

                int processedZips = 0;
                var newLogs = new ConcurrentBag<LogFile>();

                // Process zip files in parallel for better performance
                await Parallel.ForEachAsync(nestedZipFiles, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (zipFile, cancellationToken) =>
                {
                    try
                    {
                        var nestedExtractPath = Path.Combine(
                            Path.GetDirectoryName(zipFile)!,
                            Path.GetFileNameWithoutExtension(zipFile)
                        );

                        // Create directory if it doesn't exist
                        if (!Directory.Exists(nestedExtractPath))
                        {
                            Directory.CreateDirectory(nestedExtractPath);
                        }

                        // Extract the nested zip
                        ZipFile.ExtractToDirectory(zipFile, nestedExtractPath, true);

                        // Find logs in the newly extracted content
                        var centralLogs = FindCentralLogs(nestedExtractPath, sessionId);
                        foreach (var log in centralLogs)
                        {
                            newLogs.Add(log);
                        }

                        var processLogs = FindProcessLogs(nestedExtractPath, sessionId);
                        foreach (var log in processLogs)
                        {
                            newLogs.Add(log);
                        }

                        // Delete the zip file after extraction to save space
                        try
                        {
                            File.Delete(zipFile);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Could not delete zip file after extraction: {zipFile}");
                        }

                        // Update progress
                        int processed = Interlocked.Increment(ref processedZips);
                        progress.NestedProgress = (int)((processed / (double)progress.TotalNestedZips) * 100);

                        // Add any new logs found to the progress tracker
                        lock (progress.NewlyExtractedLogs)
                        {
                            progress.NewlyExtractedLogs.AddRange(newLogs);
                            newLogs = new ConcurrentBag<LogFile>();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error extracting nested zip file: {zipFile}");
                    }
                });

                progress.NestedProgress = 100;
                progress.IsComplete = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background nested zip extraction");

                if (_progressTrackers.TryGetValue(sessionId, out var progress))
                {
                    progress.IsComplete = true;
                    progress.NestedProgress = 100; // Mark as complete even on error
                }
            }
        }

        public ExtractionProgressResponse GetExtractionProgress(string sessionId)
        {
            if (_progressTrackers.TryGetValue(sessionId, out var progress))
            {
                List<LogFile> newLogs;
                lock (progress.NewlyExtractedLogs)
                {
                    newLogs = progress.NewlyExtractedLogs.ToList();
                    progress.NewlyExtractedLogs.Clear();
                }

                var response = new ExtractionProgressResponse
                {
                    InitialProgress = progress.InitialProgress,
                    NestedProgress = progress.NestedProgress,
                    IsComplete = progress.IsComplete,
                    NewLogs = newLogs
                };

                return response;
            }

            return new ExtractionProgressResponse
            {
                InitialProgress = 0,
                NestedProgress = 0,
                IsComplete = false
            };
        }

        private List<LogFile> FindCentralLogs(string basePath, string sessionId)
        {
            var centralLogs = new List<LogFile>();

            try
            {
                if (!_processedFiles.TryGetValue(sessionId, out var processedPaths))
                {
                    _processedFiles[sessionId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    processedPaths = _processedFiles[sessionId];
                }

                // Find existing syngo.txt files
                var syngoTxtFiles = Directory.GetFiles(basePath, "syngo.txt", SearchOption.AllDirectories);

                foreach (var logFile in syngoTxtFiles)
                {
                    // Only add if not already processed
                    lock (processedPaths)
                    {
                        if (!processedPaths.Contains(logFile))
                        {
                            centralLogs.Add(new LogFile
                            {
                                Name = Path.GetFileName(logFile),
                                Path = logFile,
                                Type = "central"
                            });
                            processedPaths.Add(logFile);
                        }
                    }
                }

                // Find files with pattern syngo_yyyy-mm-dd format
                var allTextFiles = Directory.GetFiles(basePath, "syngo_*.txt", SearchOption.AllDirectories);
                var datePattern = new Regex(@"^syngo_\d{4}-\d{2}-\d{2}", RegexOptions.Compiled);

                foreach (var logFile in allTextFiles)
                {
                    var fileName = Path.GetFileName(logFile);
                    if (datePattern.IsMatch(fileName))
                    {
                        // Only add if not already processed
                        lock (processedPaths)
                        {
                            if (!processedPaths.Contains(logFile))
                            {
                                centralLogs.Add(new LogFile
                                {
                                    Name = fileName,
                                    Path = logFile,
                                    Type = "central"
                                });
                                processedPaths.Add(logFile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding central logs");
            }

            return centralLogs;
        }

        private List<LogFile> FindProcessLogs(string basePath, string sessionId)
        {
            var processLogs = new List<LogFile>();
            var regex = new Regex(@".*\d{5,6}\.txt$", RegexOptions.Compiled);

            try
            {
                if (!_processedFiles.TryGetValue(sessionId, out var processedPaths))
                {
                    _processedFiles[sessionId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    processedPaths = _processedFiles[sessionId];
                }

                var allTextFiles = Directory.GetFiles(basePath, "*.txt", SearchOption.AllDirectories);

                foreach (var textFile in allTextFiles)
                {
                    if (regex.IsMatch(textFile))
                    {
                        // Only add if not already processed
                        lock (processedPaths)
                        {
                            if (!processedPaths.Contains(textFile))
                            {
                                processLogs.Add(new LogFile
                                {
                                    Name = Path.GetFileName(textFile),
                                    Path = textFile,
                                    Type = "process"
                                });
                                processedPaths.Add(textFile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding process logs");
            }

            return processLogs;
        }

        public string GetLogContent(string logFilePath)
        {
            try
            {
                if (File.Exists(logFilePath))
                {
                    return File.ReadAllText(logFilePath);
                }
                return "Log file not found";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading log content: {logFilePath}");
                return $"Error reading log content: {ex.Message}";
            }
        }

        public LiveLogResponse GetLiveLogs()
        {
            var response = new LiveLogResponse();
            string livePath = @"C:\store\logs";

            try
            {
                if (!Directory.Exists(livePath))
                {
                    _logger.LogWarning($"Live log directory does not exist: {livePath}");
                    return response;
                }

                // Get directories
                var directories = new DirectoryInfo(livePath).GetDirectories();
                foreach (var dir in directories)
                {
                    response.Items.Add(new LiveLogItem
                    {
                        Name = dir.Name,
                        Path = dir.FullName,
                        IsDirectory = true,
                        LastModified = dir.LastWriteTime,
                        Size = 0
                    });
                }

                // Get files
                var files = new DirectoryInfo(livePath).GetFiles();
                foreach (var file in files)
                {
                    response.Items.Add(new LiveLogItem
                    {
                        Name = file.Name,
                        Path = file.FullName,
                        IsDirectory = false,
                        LastModified = file.LastWriteTime,
                        Size = file.Length
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving live logs from {livePath}");
            }

            return response;
        }

        public InitialExtractionResponse ExtractLiveLogZip(string zipFilePath)
        {
            // Validate that the zip path is within the permitted live logs directory
            string livePath = @"C:\store\logs";
            if (!zipFilePath.StartsWith(livePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning($"Attempted to extract zip outside of live logs directory: {zipFilePath}");
                return new InitialExtractionResponse();
            }

            // Check if the file exists and is a zip
            if (!File.Exists(zipFilePath) || !zipFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning($"File does not exist or is not a zip: {zipFilePath}");
                return new InitialExtractionResponse();
            }

            // Reuse the existing extraction logic
            return ExtractInitialZip(zipFilePath);
        }

        public void CleanupTempFiles()
        {
            try
            {
                // Delete files older than 1 hour
                var directory = new DirectoryInfo(_tempPath);
                foreach (var dir in directory.GetDirectories())
                {
                    if (dir.CreationTime < DateTime.Now.AddHours(-1))
                    {
                        dir.Delete(true);
                    }
                }

                // Remove old progress trackers and processed files
                var keysToRemove = new List<string>();
                foreach (var tracker in _progressTrackers)
                {
                    if (tracker.Value.IsComplete || tracker.Value.StartTime < DateTime.Now.AddHours(-1))
                    {
                        keysToRemove.Add(tracker.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _progressTrackers.TryRemove(key, out _);
                    _processedFiles.TryRemove(key, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up temp files");
            }
        }
    }
}