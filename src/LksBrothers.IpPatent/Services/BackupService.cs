using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Text.Json;

namespace LksBrothers.IpPatent.Services
{
    public interface IBackupService
    {
        Task<BackupResult> CreateFullBackupAsync();
        Task<BackupResult> CreateIncrementalBackupAsync();
        Task<RestoreResult> RestoreFromBackupAsync(string backupId);
        Task<List<BackupInfo>> GetBackupHistoryAsync();
        Task<bool> ValidateBackupIntegrityAsync(string backupId);
        Task<bool> DeleteOldBackupsAsync(int retentionDays = 30);
        Task<BackupResult> CreateEmergencyBackupAsync();
    }

    public class BackupService : BackgroundService, IBackupService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<BackupService> _logger;
        private readonly IAuditTrailService _auditService;
        private readonly IEmailService _emailService;
        private readonly Timer _backupTimer;
        private readonly string _backupDirectory;
        private readonly string _emergencyBackupDirectory;
        private readonly BackupConfiguration _config;

        public BackupService(
            IConfiguration configuration, 
            ILogger<BackupService> logger, 
            IAuditTrailService auditService,
            IEmailService emailService)
        {
            _configuration = configuration;
            _logger = logger;
            _auditService = auditService;
            _emailService = emailService;

            _config = new BackupConfiguration();
            _configuration.GetSection("Backup").Bind(_config);

            _backupDirectory = _config.BackupDirectory ?? Path.Combine(Environment.CurrentDirectory, "Backups");
            _emergencyBackupDirectory = _config.EmergencyBackupDirectory ?? Path.Combine(_backupDirectory, "Emergency");

            // Ensure backup directories exist
            Directory.CreateDirectory(_backupDirectory);
            Directory.CreateDirectory(_emergencyBackupDirectory);

            // Schedule automatic backups
            var backupInterval = TimeSpan.FromHours(_config.AutoBackupIntervalHours);
            _backupTimer = new Timer(async _ => await PerformScheduledBackup(), null, backupInterval, backupInterval);
        }

        public async Task<BackupResult> CreateFullBackupAsync()
        {
            var backupId = $"full_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var backupPath = Path.Combine(_backupDirectory, $"{backupId}.zip");

            try
            {
                _logger.LogInformation($"Starting full backup: {backupId}");

                var backupData = new BackupData
                {
                    BackupId = backupId,
                    BackupType = BackupType.Full,
                    CreatedAt = DateTime.UtcNow,
                    Version = "1.0"
                };

                using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
                {
                    // Backup database data
                    await BackupDatabaseAsync(archive, backupData);

                    // Backup file uploads
                    await BackupFileUploadsAsync(archive, backupData);

                    // Backup configuration
                    await BackupConfigurationAsync(archive, backupData);

                    // Backup certificates and keys
                    await BackupCertificatesAsync(archive, backupData);

                    // Add backup metadata
                    var metadataEntry = archive.CreateEntry("backup_metadata.json");
                    using (var stream = metadataEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        await writer.WriteAsync(JsonSerializer.Serialize(backupData, new JsonSerializerOptions { WriteIndented = true }));
                    }
                }

                var fileInfo = new FileInfo(backupPath);
                var result = new BackupResult
                {
                    Success = true,
                    BackupId = backupId,
                    BackupPath = backupPath,
                    SizeBytes = fileInfo.Length,
                    Duration = DateTime.UtcNow - backupData.CreatedAt,
                    ItemsBackedUp = backupData.ItemsBackedUp
                };

                await LogBackupEvent("FULL_BACKUP_COMPLETED", backupId, result);
                _logger.LogInformation($"Full backup completed: {backupId}, Size: {fileInfo.Length / 1024 / 1024}MB");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Full backup failed: {backupId}");
                await LogBackupEvent("FULL_BACKUP_FAILED", backupId, new { Error = ex.Message });
                
                return new BackupResult
                {
                    Success = false,
                    BackupId = backupId,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<BackupResult> CreateIncrementalBackupAsync()
        {
            var backupId = $"incr_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var backupPath = Path.Combine(_backupDirectory, $"{backupId}.zip");

            try
            {
                _logger.LogInformation($"Starting incremental backup: {backupId}");

                var lastBackup = await GetLastBackupAsync();
                var cutoffDate = lastBackup?.CreatedAt ?? DateTime.UtcNow.AddDays(-1);

                var backupData = new BackupData
                {
                    BackupId = backupId,
                    BackupType = BackupType.Incremental,
                    CreatedAt = DateTime.UtcNow,
                    Version = "1.0",
                    BasedOnBackup = lastBackup?.BackupId,
                    IncrementalSince = cutoffDate
                };

                using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
                {
                    // Backup only changed data since last backup
                    await BackupIncrementalDatabaseAsync(archive, backupData, cutoffDate);
                    await BackupIncrementalFilesAsync(archive, backupData, cutoffDate);

                    // Add backup metadata
                    var metadataEntry = archive.CreateEntry("backup_metadata.json");
                    using (var stream = metadataEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        await writer.WriteAsync(JsonSerializer.Serialize(backupData, new JsonSerializerOptions { WriteIndented = true }));
                    }
                }

                var fileInfo = new FileInfo(backupPath);
                var result = new BackupResult
                {
                    Success = true,
                    BackupId = backupId,
                    BackupPath = backupPath,
                    SizeBytes = fileInfo.Length,
                    Duration = DateTime.UtcNow - backupData.CreatedAt,
                    ItemsBackedUp = backupData.ItemsBackedUp
                };

                await LogBackupEvent("INCREMENTAL_BACKUP_COMPLETED", backupId, result);
                _logger.LogInformation($"Incremental backup completed: {backupId}, Size: {fileInfo.Length / 1024 / 1024}MB");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Incremental backup failed: {backupId}");
                await LogBackupEvent("INCREMENTAL_BACKUP_FAILED", backupId, new { Error = ex.Message });
                
                return new BackupResult
                {
                    Success = false,
                    BackupId = backupId,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<RestoreResult> RestoreFromBackupAsync(string backupId)
        {
            try
            {
                _logger.LogInformation($"Starting restore from backup: {backupId}");

                var backupPath = Path.Combine(_backupDirectory, $"{backupId}.zip");
                if (!File.Exists(backupPath))
                {
                    return new RestoreResult
                    {
                        Success = false,
                        ErrorMessage = $"Backup file not found: {backupId}"
                    };
                }

                // Validate backup integrity first
                if (!await ValidateBackupIntegrityAsync(backupId))
                {
                    return new RestoreResult
                    {
                        Success = false,
                        ErrorMessage = $"Backup integrity validation failed: {backupId}"
                    };
                }

                var restoreStartTime = DateTime.UtcNow;
                var tempRestoreDir = Path.Combine(Path.GetTempPath(), $"restore_{backupId}_{DateTime.UtcNow:yyyyMMddHHmmss}");

                try
                {
                    // Extract backup to temporary directory
                    ZipFile.ExtractToDirectory(backupPath, tempRestoreDir);

                    // Read backup metadata
                    var metadataPath = Path.Combine(tempRestoreDir, "backup_metadata.json");
                    var metadata = JsonSerializer.Deserialize<BackupData>(await File.ReadAllTextAsync(metadataPath));

                    // Create emergency backup before restore
                    await CreateEmergencyBackupAsync();

                    // Restore database
                    await RestoreDatabaseAsync(tempRestoreDir, metadata);

                    // Restore files
                    await RestoreFilesAsync(tempRestoreDir, metadata);

                    // Restore configuration
                    await RestoreConfigurationAsync(tempRestoreDir, metadata);

                    var result = new RestoreResult
                    {
                        Success = true,
                        BackupId = backupId,
                        Duration = DateTime.UtcNow - restoreStartTime,
                        ItemsRestored = metadata.ItemsBackedUp
                    };

                    await LogBackupEvent("RESTORE_COMPLETED", backupId, result);
                    _logger.LogInformation($"Restore completed successfully: {backupId}");

                    return result;
                }
                finally
                {
                    // Clean up temporary directory
                    if (Directory.Exists(tempRestoreDir))
                    {
                        Directory.Delete(tempRestoreDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Restore failed: {backupId}");
                await LogBackupEvent("RESTORE_FAILED", backupId, new { Error = ex.Message });
                
                return new RestoreResult
                {
                    Success = false,
                    BackupId = backupId,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<List<BackupInfo>> GetBackupHistoryAsync()
        {
            var backups = new List<BackupInfo>();

            try
            {
                var backupFiles = Directory.GetFiles(_backupDirectory, "*.zip");
                
                foreach (var file in backupFiles)
                {
                    try
                    {
                        using (var archive = ZipFile.OpenRead(file))
                        {
                            var metadataEntry = archive.GetEntry("backup_metadata.json");
                            if (metadataEntry != null)
                            {
                                using (var stream = metadataEntry.Open())
                                using (var reader = new StreamReader(stream))
                                {
                                    var content = await reader.ReadToEndAsync();
                                    var metadata = JsonSerializer.Deserialize<BackupData>(content);
                                    
                                    var fileInfo = new FileInfo(file);
                                    backups.Add(new BackupInfo
                                    {
                                        BackupId = metadata.BackupId,
                                        BackupType = metadata.BackupType,
                                        CreatedAt = metadata.CreatedAt,
                                        SizeBytes = fileInfo.Length,
                                        ItemsBackedUp = metadata.ItemsBackedUp,
                                        FilePath = file
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to read backup metadata from {file}");
                    }
                }

                backups.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get backup history");
            }

            return backups;
        }

        public async Task<bool> ValidateBackupIntegrityAsync(string backupId)
        {
            try
            {
                var backupPath = Path.Combine(_backupDirectory, $"{backupId}.zip");
                if (!File.Exists(backupPath))
                {
                    return false;
                }

                using (var archive = ZipFile.OpenRead(backupPath))
                {
                    // Check if metadata exists
                    var metadataEntry = archive.GetEntry("backup_metadata.json");
                    if (metadataEntry == null)
                    {
                        return false;
                    }

                    // Validate all entries can be read
                    foreach (var entry in archive.Entries)
                    {
                        using (var stream = entry.Open())
                        {
                            // Try to read first few bytes to ensure entry is not corrupted
                            var buffer = new byte[1024];
                            await stream.ReadAsync(buffer, 0, buffer.Length);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Backup integrity validation failed: {backupId}");
                return false;
            }
        }

        public async Task<bool> DeleteOldBackupsAsync(int retentionDays = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
                var backups = await GetBackupHistoryAsync();
                var deletedCount = 0;

                foreach (var backup in backups.Where(b => b.CreatedAt < cutoffDate))
                {
                    try
                    {
                        File.Delete(backup.FilePath);
                        deletedCount++;
                        _logger.LogInformation($"Deleted old backup: {backup.BackupId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to delete backup: {backup.BackupId}");
                    }
                }

                await LogBackupEvent("OLD_BACKUPS_DELETED", "CLEANUP", new { DeletedCount = deletedCount, RetentionDays = retentionDays });
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete old backups");
                return false;
            }
        }

        public async Task<BackupResult> CreateEmergencyBackupAsync()
        {
            var backupId = $"emergency_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var backupPath = Path.Combine(_emergencyBackupDirectory, $"{backupId}.zip");

            try
            {
                _logger.LogWarning($"Creating emergency backup: {backupId}");

                // Create a quick backup of critical data only
                var backupData = new BackupData
                {
                    BackupId = backupId,
                    BackupType = BackupType.Emergency,
                    CreatedAt = DateTime.UtcNow,
                    Version = "1.0"
                };

                using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
                {
                    // Backup only critical database tables
                    await BackupCriticalDatabaseAsync(archive, backupData);

                    // Backup configuration files
                    await BackupConfigurationAsync(archive, backupData);

                    // Add backup metadata
                    var metadataEntry = archive.CreateEntry("backup_metadata.json");
                    using (var stream = metadataEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        await writer.WriteAsync(JsonSerializer.Serialize(backupData, new JsonSerializerOptions { WriteIndented = true }));
                    }
                }

                var fileInfo = new FileInfo(backupPath);
                var result = new BackupResult
                {
                    Success = true,
                    BackupId = backupId,
                    BackupPath = backupPath,
                    SizeBytes = fileInfo.Length,
                    Duration = DateTime.UtcNow - backupData.CreatedAt,
                    ItemsBackedUp = backupData.ItemsBackedUp
                };

                await LogBackupEvent("EMERGENCY_BACKUP_COMPLETED", backupId, result);
                _logger.LogWarning($"Emergency backup completed: {backupId}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Emergency backup failed: {backupId}");
                return new BackupResult { Success = false, BackupId = backupId, ErrorMessage = ex.Message };
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Monitor system health and create emergency backups if needed
                    await MonitorSystemHealthAsync();
                    
                    // Clean up old backups
                    if (DateTime.UtcNow.Hour == _config.CleanupHour)
                    {
                        await DeleteOldBackupsAsync(_config.RetentionDays);
                    }

                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in backup service background task");
                }
            }
        }

        private async Task PerformScheduledBackup()
        {
            try
            {
                BackupResult result;
                
                // Perform full backup on Sundays, incremental on other days
                if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Sunday)
                {
                    result = await CreateFullBackupAsync();
                }
                else
                {
                    result = await CreateIncrementalBackupAsync();
                }

                if (!result.Success)
                {
                    await NotifyBackupFailureAsync(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled backup failed");
            }
        }

        private async Task MonitorSystemHealthAsync()
        {
            // Monitor disk space, memory usage, etc.
            // Create emergency backup if system health is degraded
            var diskSpaceGB = GetAvailableDiskSpaceGB();
            
            if (diskSpaceGB < _config.MinimumDiskSpaceGB)
            {
                _logger.LogWarning($"Low disk space detected: {diskSpaceGB}GB remaining");
                await CreateEmergencyBackupAsync();
            }
        }

        private double GetAvailableDiskSpaceGB()
        {
            var drive = new DriveInfo(Path.GetPathRoot(_backupDirectory));
            return drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
        }

        private async Task<BackupInfo> GetLastBackupAsync()
        {
            var backups = await GetBackupHistoryAsync();
            return backups.FirstOrDefault();
        }

        private async Task LogBackupEvent(string eventType, string backupId, object details)
        {
            await _auditService.LogEventAsync(new AuditEvent
            {
                EventType = eventType,
                EntityType = "Backup",
                EntityId = backupId,
                UserId = "SYSTEM",
                Description = $"Backup operation: {eventType}",
                Details = details
            });
        }

        private async Task NotifyBackupFailureAsync(BackupResult result)
        {
            // Send email notification to administrators
            var adminEmails = _config.AdminNotificationEmails ?? new List<string>();
            
            foreach (var email in adminEmails)
            {
                await _emailService.SendSystemMaintenanceNotificationAsync(
                    email,
                    "Administrator",
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddHours(1),
                    $"Backup failure: {result.ErrorMessage}"
                );
            }
        }

        // Placeholder methods for actual backup operations
        private async Task BackupDatabaseAsync(ZipArchive archive, BackupData backupData)
        {
            // TODO: Implement database backup logic
            await Task.CompletedTask;
        }

        private async Task BackupFileUploadsAsync(ZipArchive archive, BackupData backupData)
        {
            // TODO: Implement file uploads backup logic
            await Task.CompletedTask;
        }

        private async Task BackupConfigurationAsync(ZipArchive archive, BackupData backupData)
        {
            // TODO: Implement configuration backup logic
            await Task.CompletedTask;
        }

        private async Task BackupCertificatesAsync(ZipArchive archive, BackupData backupData)
        {
            // TODO: Implement certificates backup logic
            await Task.CompletedTask;
        }

        private async Task BackupIncrementalDatabaseAsync(ZipArchive archive, BackupData backupData, DateTime cutoffDate)
        {
            // TODO: Implement incremental database backup logic
            await Task.CompletedTask;
        }

        private async Task BackupIncrementalFilesAsync(ZipArchive archive, BackupData backupData, DateTime cutoffDate)
        {
            // TODO: Implement incremental files backup logic
            await Task.CompletedTask;
        }

        private async Task BackupCriticalDatabaseAsync(ZipArchive archive, BackupData backupData)
        {
            // TODO: Implement critical database backup logic
            await Task.CompletedTask;
        }

        private async Task RestoreDatabaseAsync(string restoreDir, BackupData metadata)
        {
            // TODO: Implement database restore logic
            await Task.CompletedTask;
        }

        private async Task RestoreFilesAsync(string restoreDir, BackupData metadata)
        {
            // TODO: Implement files restore logic
            await Task.CompletedTask;
        }

        private async Task RestoreConfigurationAsync(string restoreDir, BackupData metadata)
        {
            // TODO: Implement configuration restore logic
            await Task.CompletedTask;
        }

        public override void Dispose()
        {
            _backupTimer?.Dispose();
            base.Dispose();
        }
    }

    // Supporting classes
    public class BackupConfiguration
    {
        public string BackupDirectory { get; set; }
        public string EmergencyBackupDirectory { get; set; }
        public int AutoBackupIntervalHours { get; set; } = 6;
        public int RetentionDays { get; set; } = 30;
        public int CleanupHour { get; set; } = 2; // 2 AM
        public double MinimumDiskSpaceGB { get; set; } = 10.0;
        public List<string> AdminNotificationEmails { get; set; } = new List<string>();
    }

    public class BackupData
    {
        public string BackupId { get; set; }
        public BackupType BackupType { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Version { get; set; }
        public string BasedOnBackup { get; set; }
        public DateTime? IncrementalSince { get; set; }
        public int ItemsBackedUp { get; set; }
    }

    public class BackupResult
    {
        public bool Success { get; set; }
        public string BackupId { get; set; }
        public string BackupPath { get; set; }
        public long SizeBytes { get; set; }
        public TimeSpan Duration { get; set; }
        public int ItemsBackedUp { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class RestoreResult
    {
        public bool Success { get; set; }
        public string BackupId { get; set; }
        public TimeSpan Duration { get; set; }
        public int ItemsRestored { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class BackupInfo
    {
        public string BackupId { get; set; }
        public BackupType BackupType { get; set; }
        public DateTime CreatedAt { get; set; }
        public long SizeBytes { get; set; }
        public int ItemsBackedUp { get; set; }
        public string FilePath { get; set; }
    }

    public enum BackupType
    {
        Full,
        Incremental,
        Emergency
    }
}
