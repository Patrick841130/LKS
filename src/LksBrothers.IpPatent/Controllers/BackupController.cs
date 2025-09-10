using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LksBrothers.IpPatent.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System;

namespace LksBrothers.IpPatent.Controllers
{
    [ApiController]
    [Route("api/ippatent/backup")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class BackupController : ControllerBase
    {
        private readonly IBackupService _backupService;
        private readonly ILogger<BackupController> _logger;
        private readonly IAuditTrailService _auditService;

        public BackupController(
            IBackupService backupService, 
            ILogger<BackupController> logger,
            IAuditTrailService auditService)
        {
            _backupService = backupService;
            _logger = logger;
            _auditService = auditService;
        }

        /// <summary>
        /// Create a full backup of the IP PATENT system
        /// </summary>
        [HttpPost("full")]
        public async Task<IActionResult> CreateFullBackup()
        {
            try
            {
                _logger.LogInformation($"Full backup requested by user: {User.Identity.Name}");
                
                var result = await _backupService.CreateFullBackupAsync();
                
                await _auditService.LogEventAsync(new AuditEvent
                {
                    EventType = "BACKUP_REQUESTED",
                    EntityType = "System",
                    EntityId = "FULL_BACKUP",
                    UserId = User.Identity.Name,
                    Description = "Full backup manually requested",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers["User-Agent"].ToString()
                });

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        backupId = result.BackupId,
                        sizeBytes = result.SizeBytes,
                        sizeMB = Math.Round(result.SizeBytes / 1024.0 / 1024.0, 2),
                        duration = result.Duration.TotalSeconds,
                        itemsBackedUp = result.ItemsBackedUp,
                        message = "Full backup completed successfully"
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = result.ErrorMessage,
                        message = "Full backup failed"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating full backup");
                return StatusCode(500, new { success = false, error = "Internal server error" });
            }
        }

        /// <summary>
        /// Create an incremental backup of the IP PATENT system
        /// </summary>
        [HttpPost("incremental")]
        public async Task<IActionResult> CreateIncrementalBackup()
        {
            try
            {
                _logger.LogInformation($"Incremental backup requested by user: {User.Identity.Name}");
                
                var result = await _backupService.CreateIncrementalBackupAsync();
                
                await _auditService.LogEventAsync(new AuditEvent
                {
                    EventType = "BACKUP_REQUESTED",
                    EntityType = "System",
                    EntityId = "INCREMENTAL_BACKUP",
                    UserId = User.Identity.Name,
                    Description = "Incremental backup manually requested",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers["User-Agent"].ToString()
                });

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        backupId = result.BackupId,
                        sizeBytes = result.SizeBytes,
                        sizeMB = Math.Round(result.SizeBytes / 1024.0 / 1024.0, 2),
                        duration = result.Duration.TotalSeconds,
                        itemsBackedUp = result.ItemsBackedUp,
                        message = "Incremental backup completed successfully"
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = result.ErrorMessage,
                        message = "Incremental backup failed"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating incremental backup");
                return StatusCode(500, new { success = false, error = "Internal server error" });
            }
        }

        /// <summary>
        /// Create an emergency backup of critical data
        /// </summary>
        [HttpPost("emergency")]
        public async Task<IActionResult> CreateEmergencyBackup()
        {
            try
            {
                _logger.LogWarning($"Emergency backup requested by user: {User.Identity.Name}");
                
                var result = await _backupService.CreateEmergencyBackupAsync();
                
                await _auditService.LogEventAsync(new AuditEvent
                {
                    EventType = "EMERGENCY_BACKUP_REQUESTED",
                    EntityType = "System",
                    EntityId = "EMERGENCY_BACKUP",
                    UserId = User.Identity.Name,
                    Description = "Emergency backup manually requested",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers["User-Agent"].ToString()
                });

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        backupId = result.BackupId,
                        sizeBytes = result.SizeBytes,
                        sizeMB = Math.Round(result.SizeBytes / 1024.0 / 1024.0, 2),
                        duration = result.Duration.TotalSeconds,
                        itemsBackedUp = result.ItemsBackedUp,
                        message = "Emergency backup completed successfully"
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = result.ErrorMessage,
                        message = "Emergency backup failed"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating emergency backup");
                return StatusCode(500, new { success = false, error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get backup history and status
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetBackupHistory()
        {
            try
            {
                var backups = await _backupService.GetBackupHistoryAsync();
                
                var response = backups.Select(b => new
                {
                    backupId = b.BackupId,
                    backupType = b.BackupType.ToString(),
                    createdAt = b.CreatedAt,
                    sizeBytes = b.SizeBytes,
                    sizeMB = Math.Round(b.SizeBytes / 1024.0 / 1024.0, 2),
                    itemsBackedUp = b.ItemsBackedUp
                }).ToList();

                return Ok(new
                {
                    success = true,
                    backups = response,
                    totalBackups = response.Count,
                    totalSizeBytes = response.Sum(b => b.sizeBytes),
                    totalSizeMB = Math.Round(response.Sum(b => b.sizeBytes) / 1024.0 / 1024.0, 2)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting backup history");
                return StatusCode(500, new { success = false, error = "Internal server error" });
            }
        }

        /// <summary>
        /// Validate backup integrity
        /// </summary>
        [HttpPost("validate/{backupId}")]
        public async Task<IActionResult> ValidateBackup(string backupId)
        {
            try
            {
                _logger.LogInformation($"Backup validation requested for {backupId} by user: {User.Identity.Name}");
                
                var isValid = await _backupService.ValidateBackupIntegrityAsync(backupId);
                
                await _auditService.LogEventAsync(new AuditEvent
                {
                    EventType = "BACKUP_VALIDATION",
                    EntityType = "Backup",
                    EntityId = backupId,
                    UserId = User.Identity.Name,
                    Description = $"Backup validation performed: {(isValid ? "VALID" : "INVALID")}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers["User-Agent"].ToString()
                });

                return Ok(new
                {
                    success = true,
                    backupId = backupId,
                    isValid = isValid,
                    message = isValid ? "Backup is valid and intact" : "Backup validation failed - file may be corrupted"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating backup {backupId}");
                return StatusCode(500, new { success = false, error = "Internal server error" });
            }
        }

        /// <summary>
        /// Restore from backup (DANGEROUS - requires SuperAdmin)
        /// </summary>
        [HttpPost("restore/{backupId}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> RestoreFromBackup(string backupId, [FromBody] RestoreRequest request)
        {
            try
            {
                _logger.LogWarning($"System restore requested for {backupId} by user: {User.Identity.Name}");
                
                // Additional confirmation required
                if (request?.ConfirmationCode != "RESTORE_CONFIRMED")
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Invalid confirmation code",
                        message = "Restore operation requires confirmation code: RESTORE_CONFIRMED"
                    });
                }

                var result = await _backupService.RestoreFromBackupAsync(backupId);
                
                await _auditService.LogEventAsync(new AuditEvent
                {
                    EventType = result.Success ? "SYSTEM_RESTORE_COMPLETED" : "SYSTEM_RESTORE_FAILED",
                    EntityType = "System",
                    EntityId = backupId,
                    UserId = User.Identity.Name,
                    Description = $"System restore from backup {backupId}: {(result.Success ? "SUCCESS" : "FAILED")}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers["User-Agent"].ToString(),
                    Details = new { BackupId = backupId, ErrorMessage = result.ErrorMessage }
                });

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        backupId = result.BackupId,
                        duration = result.Duration.TotalSeconds,
                        itemsRestored = result.ItemsRestored,
                        message = "System restore completed successfully"
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = result.ErrorMessage,
                        message = "System restore failed"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error restoring from backup {backupId}");
                return StatusCode(500, new { success = false, error = "Internal server error" });
            }
        }

        /// <summary>
        /// Delete old backups based on retention policy
        /// </summary>
        [HttpDelete("cleanup")]
        public async Task<IActionResult> CleanupOldBackups([FromQuery] int retentionDays = 30)
        {
            try
            {
                _logger.LogInformation($"Backup cleanup requested by user: {User.Identity.Name}, retention: {retentionDays} days");
                
                var success = await _backupService.DeleteOldBackupsAsync(retentionDays);
                
                await _auditService.LogEventAsync(new AuditEvent
                {
                    EventType = "BACKUP_CLEANUP",
                    EntityType = "System",
                    EntityId = "CLEANUP",
                    UserId = User.Identity.Name,
                    Description = $"Backup cleanup performed with {retentionDays} days retention",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers["User-Agent"].ToString(),
                    Details = new { RetentionDays = retentionDays, Success = success }
                });

                return Ok(new
                {
                    success = success,
                    retentionDays = retentionDays,
                    message = success ? "Old backups cleaned up successfully" : "Backup cleanup encountered some errors"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during backup cleanup");
                return StatusCode(500, new { success = false, error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get backup system status and health
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetBackupStatus()
        {
            try
            {
                var backups = await _backupService.GetBackupHistoryAsync();
                var lastBackup = backups.FirstOrDefault();
                var lastFullBackup = backups.FirstOrDefault(b => b.BackupType == BackupType.Full);

                return Ok(new
                {
                    success = true,
                    status = new
                    {
                        lastBackup = lastBackup != null ? new
                        {
                            backupId = lastBackup.BackupId,
                            backupType = lastBackup.BackupType.ToString(),
                            createdAt = lastBackup.CreatedAt,
                            sizeBytes = lastBackup.SizeBytes,
                            sizeMB = Math.Round(lastBackup.SizeBytes / 1024.0 / 1024.0, 2),
                            hoursAgo = Math.Round((DateTime.UtcNow - lastBackup.CreatedAt).TotalHours, 1)
                        } : null,
                        lastFullBackup = lastFullBackup != null ? new
                        {
                            backupId = lastFullBackup.BackupId,
                            createdAt = lastFullBackup.CreatedAt,
                            daysAgo = Math.Round((DateTime.UtcNow - lastFullBackup.CreatedAt).TotalDays, 1)
                        } : null,
                        totalBackups = backups.Count,
                        totalSizeBytes = backups.Sum(b => b.SizeBytes),
                        totalSizeMB = Math.Round(backups.Sum(b => b.SizeBytes) / 1024.0 / 1024.0, 2),
                        healthStatus = GetBackupHealthStatus(backups)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting backup status");
                return StatusCode(500, new { success = false, error = "Internal server error" });
            }
        }

        private string GetBackupHealthStatus(List<BackupInfo> backups)
        {
            if (!backups.Any())
                return "NO_BACKUPS";

            var lastBackup = backups.First();
            var hoursSinceLastBackup = (DateTime.UtcNow - lastBackup.CreatedAt).TotalHours;

            if (hoursSinceLastBackup > 48)
                return "CRITICAL";
            else if (hoursSinceLastBackup > 24)
                return "WARNING";
            else
                return "HEALTHY";
        }
    }

    public class RestoreRequest
    {
        public string ConfirmationCode { get; set; }
        public string Reason { get; set; }
    }
}
