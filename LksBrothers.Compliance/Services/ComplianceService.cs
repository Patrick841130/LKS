using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LksBrothers.Compliance.Engine;
using LksBrothers.Compliance.Models;
using LksBrothers.Core.Models;
using LksBrothers.Core.Primitives;
using LksBrothers.StateManagement.Services;
using System.Collections.Concurrent;

namespace LksBrothers.Compliance.Services;

public class ComplianceService : BackgroundService
{
    private readonly ILogger<ComplianceService> _logger;
    private readonly ComplianceEngine _complianceEngine;
    private readonly StateService _stateService;
    private readonly ComplianceOptions _options;
    private readonly ConcurrentQueue<ComplianceTask> _taskQueue;
    private readonly SemaphoreSlim _processingLock;
    private readonly Timer _reportingTimer;
    private readonly ConcurrentDictionary<Hash, ComplianceResult> _recentResults;

    public ComplianceService(
        ILogger<ComplianceService> logger,
        ComplianceEngine complianceEngine,
        StateService stateService,
        IOptions<ComplianceOptions> options)
    {
        _logger = logger;
        _complianceEngine = complianceEngine;
        _stateService = stateService;
        _options = options.Value;
        _taskQueue = new ConcurrentQueue<ComplianceTask>();
        _processingLock = new SemaphoreSlim(1, 1);
        _recentResults = new ConcurrentDictionary<Hash, ComplianceResult>();
        
        // Initialize reporting timer for regulatory reports
        _reportingTimer = new Timer(GeneratePeriodicReports, null, 
            TimeSpan.FromHours(24), TimeSpan.FromHours(24));
        
        _logger.LogInformation("Compliance service initialized");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Compliance service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessComplianceTasksAsync(stoppingToken);
                await Task.Delay(1000, stoppingToken); // Process every second
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in compliance service execution");
                await Task.Delay(5000, stoppingToken); // Wait before retrying
            }
        }

        _logger.LogInformation("Compliance service stopped");
    }

    public async Task<ComplianceResult> ValidateTransactionComplianceAsync(Transaction transaction)
    {
        try
        {
            // Check cache first
            if (_recentResults.TryGetValue(transaction.Hash, out var cachedResult))
            {
                _logger.LogDebug("Returning cached compliance result for transaction {TxHash}", transaction.Hash);
                return cachedResult;
            }

            // Perform compliance validation
            var result = await _complianceEngine.ValidateTransactionAsync(transaction);
            
            // Cache result
            _recentResults.TryAdd(transaction.Hash, result);
            
            // Log compliance event
            await LogComplianceEventAsync(new ComplianceEvent
            {
                Id = Hash.ComputeHash($"compliance_{transaction.Hash}_{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                Type = ComplianceEventType.TransactionValidation,
                TransactionHash = transaction.Hash,
                Address = transaction.From,
                Result = result.IsCompliant ? "PASS" : "FAIL",
                Details = result.Message,
                Timestamp = DateTimeOffset.UtcNow
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating transaction compliance for {TxHash}", transaction.Hash);
            return ComplianceResult.Failed($"Compliance validation error: {ex.Message}");
        }
    }

    public async Task<KYCResult> SubmitKYCRequestAsync(KYCRequest request)
    {
        try
        {
            // Queue KYC processing task
            var task = new ComplianceTask
            {
                Id = Hash.ComputeHash($"kyc_{request.Address}_{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                Type = ComplianceTaskType.KYCProcessing,
                Address = request.Address,
                Data = request,
                Priority = ComplianceTaskPriority.High,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _taskQueue.Enqueue(task);
            
            _logger.LogInformation("Queued KYC processing task {TaskId} for address {Address}", 
                task.Id, request.Address);

            // For real-time processing, process immediately
            if (_options.EnableRealTimeScreening)
            {
                return await _complianceEngine.ProcessKYCAsync(request);
            }

            return KYCResult.Success(new KYCRecord
            {
                Id = task.Id,
                Address = request.Address,
                FirstName = request.FirstName,
                LastName = request.LastName,
                DateOfBirth = request.DateOfBirth,
                Nationality = request.Nationality,
                DocumentType = "pending",
                DocumentNumber = "pending",
                VerificationLevel = KYCVerificationLevel.Basic,
                Status = KYCStatus.Pending,
                VerifiedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddYears(1),
                RiskScore = 0.0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting KYC request for address {Address}", request.Address);
            return KYCResult.Failed($"KYC submission error: {ex.Message}");
        }
    }

    public async Task<AMLResult> PerformAMLScreeningAsync(Address address, UInt256 amount, string transactionType)
    {
        try
        {
            var result = await _complianceEngine.ProcessAMLScreeningAsync(address, amount, transactionType);
            
            // Log AML screening event
            await LogComplianceEventAsync(new ComplianceEvent
            {
                Id = Hash.ComputeHash($"aml_{address}_{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                Type = ComplianceEventType.AMLScreening,
                Address = address,
                Amount = amount,
                Result = result.IsClear ? "CLEAR" : "FLAGGED",
                Details = result.ErrorMessage ?? "AML screening completed",
                Timestamp = DateTimeOffset.UtcNow
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing AML screening for address {Address}", address);
            return AMLResult.Failed($"AML screening error: {ex.Message}");
        }
    }

    public async Task<SanctionsResult> PerformSanctionsScreeningAsync(Address address)
    {
        try
        {
            var result = await _complianceEngine.CheckSanctionsAsync(address);
            
            // Log sanctions screening event
            await LogComplianceEventAsync(new ComplianceEvent
            {
                Id = Hash.ComputeHash($"sanctions_{address}_{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                Type = ComplianceEventType.SanctionsScreening,
                Address = address,
                Result = result.IsClear ? "CLEAR" : "MATCH",
                Details = result.ErrorMessage ?? "Sanctions screening completed",
                Timestamp = DateTimeOffset.UtcNow
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing sanctions screening for address {Address}", address);
            return SanctionsResult.Failed($"Sanctions screening error: {ex.Message}");
        }
    }

    public async Task<ComplianceReport> GenerateComplianceReportAsync(DateTimeOffset startDate, DateTimeOffset endDate)
    {
        try
        {
            var report = await _complianceEngine.GenerateComplianceReportAsync(startDate, endDate);
            
            // Store report
            await _stateService.StoreComplianceReportAsync(report);
            
            _logger.LogInformation("Generated compliance report {ReportId} for period {Start} to {End}", 
                report.Id, startDate, endDate);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance report");
            throw;
        }
    }

    public async Task<List<ComplianceEvent>> GetComplianceEventsAsync(Address? address = null, 
        ComplianceEventType? eventType = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
    {
        try
        {
            return await _stateService.GetComplianceEventsAsync(address, eventType, startDate, endDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compliance events");
            return new List<ComplianceEvent>();
        }
    }

    public async Task<ComplianceMetrics> GetComplianceMetricsAsync()
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var last24Hours = now.AddHours(-24);
            var last7Days = now.AddDays(-7);
            var last30Days = now.AddDays(-30);

            var metrics = new ComplianceMetrics
            {
                GeneratedAt = now,
                Last24Hours = await CalculateMetricsForPeriodAsync(last24Hours, now),
                Last7Days = await CalculateMetricsForPeriodAsync(last7Days, now),
                Last30Days = await CalculateMetricsForPeriodAsync(last30Days, now)
            };

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating compliance metrics");
            throw;
        }
    }

    private async Task ProcessComplianceTasksAsync(CancellationToken cancellationToken)
    {
        if (_taskQueue.IsEmpty)
            return;

        await _processingLock.WaitAsync(cancellationToken);
        try
        {
            var processedCount = 0;
            while (_taskQueue.TryDequeue(out var task) && processedCount < 10) // Process up to 10 tasks per cycle
            {
                await ProcessComplianceTaskAsync(task);
                processedCount++;
            }

            if (processedCount > 0)
            {
                _logger.LogDebug("Processed {Count} compliance tasks", processedCount);
            }
        }
        finally
        {
            _processingLock.Release();
        }
    }

    private async Task ProcessComplianceTaskAsync(ComplianceTask task)
    {
        try
        {
            switch (task.Type)
            {
                case ComplianceTaskType.KYCProcessing:
                    if (task.Data is KYCRequest kycRequest)
                    {
                        var result = await _complianceEngine.ProcessKYCAsync(kycRequest);
                        await LogComplianceEventAsync(new ComplianceEvent
                        {
                            Id = Hash.ComputeHash($"kyc_processed_{task.Id}_{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                            Type = ComplianceEventType.KYCProcessing,
                            Address = task.Address,
                            Result = result.IsSuccess ? "SUCCESS" : "FAILED",
                            Details = result.ErrorMessage ?? "KYC processing completed",
                            Timestamp = DateTimeOffset.UtcNow
                        });
                    }
                    break;

                case ComplianceTaskType.AMLReview:
                    // Process AML review task
                    await ProcessAMLReviewTaskAsync(task);
                    break;

                case ComplianceTaskType.SanctionsUpdate:
                    // Process sanctions list update
                    await ProcessSanctionsUpdateTaskAsync(task);
                    break;

                case ComplianceTaskType.ReportGeneration:
                    // Process report generation
                    await ProcessReportGenerationTaskAsync(task);
                    break;
            }

            _logger.LogDebug("Processed compliance task {TaskId} of type {Type}", task.Id, task.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing compliance task {TaskId}", task.Id);
        }
    }

    private async Task ProcessAMLReviewTaskAsync(ComplianceTask task)
    {
        // Implementation for AML review processing
        await Task.CompletedTask;
    }

    private async Task ProcessSanctionsUpdateTaskAsync(ComplianceTask task)
    {
        // Implementation for sanctions list updates
        await Task.CompletedTask;
    }

    private async Task ProcessReportGenerationTaskAsync(ComplianceTask task)
    {
        // Implementation for automated report generation
        await Task.CompletedTask;
    }

    private async Task LogComplianceEventAsync(ComplianceEvent complianceEvent)
    {
        try
        {
            await _stateService.StoreComplianceEventAsync(complianceEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging compliance event {EventId}", complianceEvent.Id);
        }
    }

    private async Task<CompliancePeriodMetrics> CalculateMetricsForPeriodAsync(DateTimeOffset start, DateTimeOffset end)
    {
        var events = await GetComplianceEventsAsync(null, null, start, end);
        
        return new CompliancePeriodMetrics
        {
            TotalTransactions = events.Count(e => e.Type == ComplianceEventType.TransactionValidation),
            PassedTransactions = events.Count(e => e.Type == ComplianceEventType.TransactionValidation && e.Result == "PASS"),
            FailedTransactions = events.Count(e => e.Type == ComplianceEventType.TransactionValidation && e.Result == "FAIL"),
            KYCApplications = events.Count(e => e.Type == ComplianceEventType.KYCProcessing),
            AMLScreenings = events.Count(e => e.Type == ComplianceEventType.AMLScreening),
            SanctionsScreenings = events.Count(e => e.Type == ComplianceEventType.SanctionsScreening),
            ComplianceViolations = events.Count(e => e.Result == "FAIL" || e.Result == "FLAGGED" || e.Result == "MATCH")
        };
    }

    private async void GeneratePeriodicReports(object? state)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var yesterday = now.AddDays(-1);
            
            // Generate daily compliance report
            var dailyReport = await GenerateComplianceReportAsync(yesterday.Date, now.Date);
            
            _logger.LogInformation("Generated daily compliance report {ReportId}", dailyReport.Id);

            // Generate weekly report on Sundays
            if (now.DayOfWeek == DayOfWeek.Sunday)
            {
                var weekStart = now.AddDays(-7);
                var weeklyReport = await GenerateComplianceReportAsync(weekStart, now);
                _logger.LogInformation("Generated weekly compliance report {ReportId}", weeklyReport.Id);
            }

            // Generate monthly report on the 1st of each month
            if (now.Day == 1)
            {
                var monthStart = now.AddMonths(-1);
                var monthlyReport = await GenerateComplianceReportAsync(monthStart, now);
                _logger.LogInformation("Generated monthly compliance report {ReportId}", monthlyReport.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating periodic compliance reports");
        }
    }

    public override void Dispose()
    {
        _reportingTimer?.Dispose();
        _processingLock?.Dispose();
        _complianceEngine?.Dispose();
        base.Dispose();
        _logger.LogInformation("Compliance service disposed");
    }
}

// Supporting data models
public class ComplianceTask
{
    public required Hash Id { get; set; }
    public required ComplianceTaskType Type { get; set; }
    public required Address Address { get; set; }
    public object? Data { get; set; }
    public required ComplianceTaskPriority Priority { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}

public enum ComplianceTaskType
{
    KYCProcessing,
    AMLReview,
    SanctionsUpdate,
    ReportGeneration
}

public enum ComplianceTaskPriority
{
    Low,
    Medium,
    High,
    Critical
}

[MessagePack.MessagePackObject]
public class ComplianceEvent
{
    [MessagePack.Key(0)]
    public required Hash Id { get; set; }

    [MessagePack.Key(1)]
    public required ComplianceEventType Type { get; set; }

    [MessagePack.Key(2)]
    public Hash? TransactionHash { get; set; }

    [MessagePack.Key(3)]
    public required Address Address { get; set; }

    [MessagePack.Key(4)]
    public UInt256? Amount { get; set; }

    [MessagePack.Key(5)]
    public required string Result { get; set; }

    [MessagePack.Key(6)]
    public required string Details { get; set; }

    [MessagePack.Key(7)]
    public required DateTimeOffset Timestamp { get; set; }
}

public enum ComplianceEventType
{
    TransactionValidation,
    KYCProcessing,
    AMLScreening,
    SanctionsScreening,
    ComplianceViolation,
    ReportGeneration
}

public class ComplianceMetrics
{
    public required DateTimeOffset GeneratedAt { get; set; }
    public required CompliancePeriodMetrics Last24Hours { get; set; }
    public required CompliancePeriodMetrics Last7Days { get; set; }
    public required CompliancePeriodMetrics Last30Days { get; set; }
}

public class CompliancePeriodMetrics
{
    public int TotalTransactions { get; set; }
    public int PassedTransactions { get; set; }
    public int FailedTransactions { get; set; }
    public int KYCApplications { get; set; }
    public int AMLScreenings { get; set; }
    public int SanctionsScreenings { get; set; }
    public int ComplianceViolations { get; set; }
    public double ComplianceRate => TotalTransactions > 0 ? (double)PassedTransactions / TotalTransactions * 100 : 100;
}
