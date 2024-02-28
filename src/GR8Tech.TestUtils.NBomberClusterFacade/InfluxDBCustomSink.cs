using System.Runtime.CompilerServices;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Configuration;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using Serilog;

namespace GR8Tech.TestUtils.NBomberClusterFacade;

[Serializable]
public class InfluxDBCustomSink : IReportingSink
{
    private ILogger _logger;
    private InfluxDBClient _influxdbClient;
    private IBaseContext _context;
    private Dictionary<string, string> _globalTags = new();

    public InfluxDBCustomSink(string url, string userName, string password, string databaseName)
    {
        _logger = null;
        _context = null;
        _influxdbClient =
            InfluxDBClientFactory.CreateV1(url, userName, password.ToCharArray(), databaseName, "autogen");
    }

    public Dictionary<string, string> GlobalMetricTags
    {
        get => _globalTags;
        set => _globalTags = value;
    }

    [SpecialName] public string SinkName => "NBomber.ParimatchTech.Sinks.InfluxDB";

    public Task Init(IBaseContext context, IConfiguration infraConfig)
    {
        _logger = context.Logger.ForContext<InfluxDBCustomSink>();
        _context = context;

        return Task.CompletedTask;
    }

    public Task Start() => Task.CompletedTask;

    public async Task SaveAnnotationToInfluxDBAsync(
        string annotationTitle,
        Dictionary<string, string> annotationTags,
        string text = "")
    {
        var tags = "";

        foreach (var tag in annotationTags)
            tags += $"{tag.Key}: {tag.Value},";

        tags = tags.Substring(0, tags.Length - 1);

        var point = PointData.Measurement("nbomber__annotations")
            .Tag("title", annotationTitle)
            .Tag("tags", tags)
            .Field("text", text);

        point = AddTags(point, new Dictionary<string, string>());

        await _influxdbClient.GetWriteApiAsync().WritePointAsync(point);
    }

    public async Task SaveValueToInfluxDBAsync(
        string measurement,
        Dictionary<string, string> customTags,
        string fieldKey,
        double fieldValue)
    {
        var point = PointData.Measurement(measurement)
            .Field(fieldKey, fieldValue);
        point = AddTags(point, customTags);

        await _influxdbClient.GetWriteApiAsync().WritePointAsync(point);
    }

    public async Task SaveValueToInfluxDBAsync(
        string measurement,
        Dictionary<string, string> customTags,
        string fieldKey,
        double fieldValue,
        DateTime timestamp)
    {
        var point = PointData.Measurement(measurement)
            .Field(fieldKey, fieldValue);
        point = AddTags(point, customTags);
        point = point.Timestamp(timestamp, WritePrecision.Ms);

        await _influxdbClient.GetWriteApiAsync().WritePointAsync(point);
    }

    internal string GetOperationName(OperationType operation)
    {
        switch (operation)
        {
            case OperationType.Bombing:
                return "bombing";
            case OperationType.Complete:
                return "complete";
            default:
                return "bombing";
        }
    }

    public Task SaveRealtimeStats(ScenarioStats[] stats)
    {
        if (stats == null)
            _logger.Information("Scenario stats is null");

        var scenarioTasks = new List<Task>();

        foreach (var s in stats)
            scenarioTasks.Add(SaveScenarioStats(s));

        return Task.WhenAll(scenarioTasks);
    }

    private Task SaveScenarioStats(ScenarioStats stats)
    {
        var operationName = GetOperationName(stats.CurrentOperation);
        var nodeType = _context.GetNodeInfo().NodeType.ToString();
        var scenarioName = stats.ScenarioName;
        var testInfo = _context.TestInfo;
        var loadSimulationStats = stats.LoadSimulationStats;
        var stepStats = stats.StepStats;

        if (stepStats == null)
            _logger.Information("Step stats is null");

        var stepTasks = new List<Task>();
        foreach (var s in stepStats)
            stepTasks.Add(SaveStepStats(operationName, nodeType, testInfo, scenarioName, loadSimulationStats, s));

        return Task.WhenAll(stepTasks);
    }

    private Task SaveStepStats(
        string operationName,
        string nodeType,
        TestInfo testInfo,
        string scenarioName,
        LoadSimulationStats loadSimulationStats,
        StepStats stats)
    {
        var context = "nbomber";

        var measurementSimulationValue = $"{context}__simulation.value";
        
        var measurementOkRequestCount = $"{context}__ok.request.count";
        var measurementOkRequestRps = $"{context}__ok.request.rps";
        var measurementOkLatencyMax = $"{context}__ok.latency.max";
        var measurementOkLatencyMin = $"{context}__ok.latency.min";
        var measurementOkLatencyMean = $"{context}__ok.latency.mean";
        var measurementOkLatencyPercent50 = $"{context}__ok.latency.percent50";
        var measurementOkLatencyPercent75 = $"{context}__ok.latency.percent75";
        var measurementOkLatencyPercent95 = $"{context}__ok.latency.percent95";
        var measurementOkLatencyPercent99 = $"{context}__ok.latency.percent99";
        var measurementOkLatencyStddev = $"{context}__ok.latency.stddev";
        var measurementOkDataTransferAllbytes = $"{context}__ok.datatransfer.allbytes";
        var measurementOkDataTransferMax = $"{context}__ok.datatransfer.max";
        var measurementOkDataTransferMin = $"{context}__ok.datatransfer.min";
        var measurementOkDataTransferMean = $"{context}__ok.datatransfer.mean";

        var measurementFailRequestCount = $"{context}__fail.request.count";
        var measurementFailRequestRps = $"{context}__fail.request.rps";
        var measurementFailLatencyMax = $"{context}__fail.latency.max";
        var measurementFailLatencyMin = $"{context}__fail.latency.min";
        var measurementFailLatencyMean = $"{context}__fail.latency.mean";
        var measurementFailLatencyPercent50 = $"{context}__fail.latency.percent50";
        var measurementFailLatencyPercent75 = $"{context}__fail.latency.percent75";
        var measurementFailLatencyPercent95 = $"{context}__fail.latency.percent95";
        var measurementFailLatencyPercent99 = $"{context}__fail.latency.percent99";
        var measurementFailLatencyStddev = $"{context}__fail.latency.stddev";
        var measurementFailDataTransferAllbytes = $"{context}__fail.datatransfer.allbytes";
        var measurementFailDataTransferMax = $"{context}__fail.datatransfer.max";
        var measurementFailDataTransferMin = $"{context}__fail.datatransfer.min";
        var measurementFailDataTransferMean = $"{context}__fail.datatransfer.mean";

        var measurementAllRequestCount = $"{context}__all.request.count";
        var measurementAllDataTransferAllbytes = $"{context}__all.datatransfer.allbytes";

        var commonTags = new Dictionary<string, string>()
        {
            { "node_type", nodeType },
            { "operation", operationName },
            { "scenario", scenarioName },
            { "simulation.name", loadSimulationStats.SimulationName },
            { "step", stats.StepName }
        };

        var writeApiAsync = _influxdbClient.GetWriteApiAsync();
        var points = new List<PointData>();
        points.Add(GeneratePoint(measurementSimulationValue, commonTags, loadSimulationStats.Value));

        if (stats.Ok.Request.Count > 0)
        {
            points.Add(GeneratePoint(measurementOkRequestCount, commonTags, stats.Ok.Request.Count));
            points.Add(GeneratePoint(measurementOkRequestRps, commonTags, stats.Ok.Request.RPS));
            points.Add(GeneratePoint(measurementOkLatencyMax, commonTags, stats.Ok.Latency.MaxMs));
            points.Add(GeneratePoint(measurementOkLatencyMin, commonTags, stats.Ok.Latency.MinMs));
            points.Add(GeneratePoint(measurementOkLatencyMean, commonTags, stats.Ok.Latency.MeanMs));
            points.Add(GeneratePoint(measurementOkLatencyPercent50, commonTags, stats.Ok.Latency.Percent50));
            points.Add(GeneratePoint(measurementOkLatencyPercent75, commonTags, stats.Ok.Latency.Percent75));
            points.Add(GeneratePoint(measurementOkLatencyPercent95, commonTags, stats.Ok.Latency.Percent95));
            points.Add(GeneratePoint(measurementOkLatencyPercent99, commonTags, stats.Ok.Latency.Percent99));
            points.Add(GeneratePoint(measurementOkLatencyStddev, commonTags, stats.Ok.Latency.StdDev));
            points.Add(GeneratePoint(measurementOkDataTransferAllbytes, commonTags,
                stats.Ok.DataTransfer.AllBytes));
            points.Add(GeneratePoint(measurementOkDataTransferMax, commonTags, stats.Ok.DataTransfer.MaxBytes));
            points.Add(GeneratePoint(measurementOkDataTransferMin, commonTags, stats.Ok.DataTransfer.MinBytes));
            points.Add(GeneratePoint(measurementOkDataTransferMean, commonTags, stats.Ok.DataTransfer.MeanBytes));
        }

        if (stats.Fail.Request.Count > 0)
        {
            points.Add(GeneratePoint(measurementFailRequestCount, commonTags, stats.Fail.Request.Count));
            points.Add(GeneratePoint(measurementFailRequestRps, commonTags, stats.Fail.Request.RPS));
            points.Add(GeneratePoint(measurementFailLatencyMax, commonTags, stats.Fail.Latency.MaxMs));
            points.Add(GeneratePoint(measurementFailLatencyMin, commonTags, stats.Fail.Latency.MinMs));
            points.Add(GeneratePoint(measurementFailLatencyMean, commonTags, stats.Fail.Latency.MeanMs));
            points.Add(GeneratePoint(measurementFailLatencyPercent50, commonTags, stats.Fail.Latency.Percent50));
            points.Add(GeneratePoint(measurementFailLatencyPercent75, commonTags, stats.Fail.Latency.Percent75));
            points.Add(GeneratePoint(measurementFailLatencyPercent95, commonTags, stats.Fail.Latency.Percent95));
            points.Add(GeneratePoint(measurementFailLatencyPercent99, commonTags, stats.Fail.Latency.Percent99));
            points.Add(GeneratePoint(measurementFailLatencyStddev, commonTags, stats.Fail.Latency.StdDev));
            points.Add(GeneratePoint(measurementFailDataTransferAllbytes, commonTags,
                stats.Fail.DataTransfer.AllBytes));
            points.Add(GeneratePoint(measurementFailDataTransferMax, commonTags, stats.Fail.DataTransfer.MaxBytes));
            points.Add(GeneratePoint(measurementFailDataTransferMin, commonTags, stats.Fail.DataTransfer.MinBytes));
            points.Add(
                GeneratePoint(measurementFailDataTransferMean, commonTags, stats.Fail.DataTransfer.MeanBytes));
        }

        points.Add(GeneratePoint(measurementAllRequestCount, commonTags,
            stats.Ok.Request.Count + stats.Fail.Request.Count));
        points.Add(GeneratePoint(measurementAllDataTransferAllbytes, commonTags,
            stats.Ok.DataTransfer.AllBytes + stats.Fail.DataTransfer.AllBytes));

        return Task.WhenAll(SavePoints(writeApiAsync, points.ToArray()));
    }

    private PointData GeneratePoint(
        string measurement,
        Dictionary<string, string> commonTags,
        double fieldValue,
        string fieldKey = "value")
    {
        var point = PointData.Measurement(measurement);
        point = AddTags(point, commonTags);
        point = point.Field(fieldKey, fieldValue);

        return point;
    }

    private async Task SavePoints(WriteApiAsync writeApiAsync, PointData[] points)
    {
        await writeApiAsync.WritePointsAsync(points);
    }

    private PointData AddTags(PointData point, Dictionary<string, string> commonTags)
    {
        foreach (var tag in _globalTags)
            point = point.Tag(tag.Key, tag.Value);

        foreach (var tag in commonTags)
            point = point.Tag(tag.Key, tag.Value);

        return point;
    }

    public Task SaveFinalStats(NodeStats stats)
    {
        var nodeTasks = new List<Task>();

        nodeTasks.Add(SaveFinalNodeStats(stats));

        return Task.WhenAll(nodeTasks);
    }

    private Task SaveFinalNodeStats(NodeStats stats)
    {
        var scenarioTasks = new List<Task>();

        foreach (var s in stats.ScenarioStats)
        {
            scenarioTasks.Add(SaveFinalScenarioStats(s, stats.NodeInfo.CurrentOperation));
        }

        return Task.WhenAll(scenarioTasks);
    }

    private Task SaveFinalScenarioStats(ScenarioStats stats, OperationType operationType)
    {
        var stepTasks = new List<Task>();

        foreach (var s in stats.StepStats)
        {
            stepTasks.Add(SaveFinalStepStats(s, operationType, stats.ScenarioName));
        }

        return Task.WhenAll(stepTasks);
    }

    private Task SaveFinalStepStats(StepStats stats, OperationType operationType, string scenarioName)
    {
        var context = "nbomber";

        var measurementOkSummary = $"{context}__ok.summary";
        var measurementFailSummary = $"{context}__fail.summary";

        var commonTags = new Dictionary<string, string>()
        {
            { "node_type", _context.GetNodeInfo().NodeType.ToString() },
            { "operation", GetOperationName(operationType) },
            { "scenario", scenarioName },
            { "step", stats.StepName },
            { "stat_type", "default" }
        };

        var writeApiAsync = _influxdbClient.GetWriteApiAsync();
        var points = new List<PointData>();

        if (stats.Ok.Request.Count > 0)
        {
            commonTags["stat_type"] = "request";
            points.Add(GeneratePoint(measurementOkSummary, commonTags, stats.Ok.Request.Count, "count"));
            points.Add(GeneratePoint(measurementOkSummary, commonTags,
                stats.Ok.Request.Count + stats.Fail.Request.Count, "all"));
            points.Add(GeneratePoint(measurementOkSummary, commonTags, stats.Ok.Request.RPS, "rps"));

            commonTags["stat_type"] = "latency";
            points.Add(GeneratePoint(measurementOkSummary, commonTags, stats.Ok.Latency.MaxMs, "max"));
            points.Add(GeneratePoint(measurementOkSummary, commonTags, stats.Ok.Latency.MinMs, "min"));
            points.Add(GeneratePoint(measurementOkSummary, commonTags, stats.Ok.Latency.MeanMs, "mean"));
            points.Add(GeneratePoint(measurementOkSummary, commonTags, stats.Ok.Latency.StdDev, "stddev"));
            points.Add(GeneratePoint(measurementOkSummary, commonTags, stats.Ok.Latency.Percent50, "percent50"));
            points.Add(GeneratePoint(measurementOkSummary, commonTags, stats.Ok.Latency.Percent75, "percent75"));
            points.Add(GeneratePoint(measurementOkSummary, commonTags, stats.Ok.Latency.Percent95, "percent95"));
            points.Add(GeneratePoint(measurementOkSummary, commonTags, stats.Ok.Latency.Percent99, "percent99"));

            commonTags["stat_type"] = "datatransfer";
            points.Add(GeneratePoint(measurementOkSummary, commonTags, stats.Ok.DataTransfer.MinBytes, "min"));
            points.Add(GeneratePoint(measurementOkSummary, commonTags, stats.Ok.DataTransfer.MaxBytes, "max"));
            points.Add(GeneratePoint(measurementOkSummary, commonTags, stats.Ok.DataTransfer.MeanBytes, "mean"));
            points.Add(GeneratePoint(measurementOkSummary, commonTags, stats.Ok.DataTransfer.AllBytes, "allbytes"));
        }

        if (stats.Fail.Request.Count > 0)
        {
            commonTags["stat_type"] = "request";
            points.Add(GeneratePoint(measurementFailSummary, commonTags, stats.Fail.Request.Count, "count"));
            points.Add(GeneratePoint(measurementFailSummary, commonTags,
                stats.Ok.Request.Count + stats.Fail.Request.Count, "all"));
            points.Add(GeneratePoint(measurementFailSummary, commonTags, stats.Fail.Request.RPS, "rps"));

            commonTags["stat_type"] = "latency";
            points.Add(GeneratePoint(measurementFailSummary, commonTags, stats.Fail.Latency.MaxMs, "max"));
            points.Add(GeneratePoint(measurementFailSummary, commonTags, stats.Fail.Latency.MinMs, "min"));
            points.Add(GeneratePoint(measurementFailSummary, commonTags, stats.Fail.Latency.MeanMs, "mean"));
            points.Add(GeneratePoint(measurementFailSummary, commonTags, stats.Fail.Latency.StdDev, "stddev"));
            points.Add(GeneratePoint(measurementFailSummary, commonTags, stats.Fail.Latency.Percent50,
                "percent50"));
            points.Add(GeneratePoint(measurementFailSummary, commonTags, stats.Fail.Latency.Percent75,
                "percent75"));
            points.Add(GeneratePoint(measurementFailSummary, commonTags, stats.Fail.Latency.Percent95,
                "percent95"));
            points.Add(GeneratePoint(measurementFailSummary, commonTags, stats.Fail.Latency.Percent99,
                "percent99"));

            commonTags["stat_type"] = "datatransfer";
            points.Add(GeneratePoint(measurementFailSummary, commonTags, stats.Fail.DataTransfer.MinBytes, "min"));
            points.Add(GeneratePoint(measurementFailSummary, commonTags, stats.Fail.DataTransfer.MaxBytes, "max"));
            points.Add(GeneratePoint(measurementFailSummary, commonTags, stats.Fail.DataTransfer.MeanBytes,
                "mean"));
            points.Add(GeneratePoint(measurementFailSummary, commonTags, stats.Fail.DataTransfer.AllBytes,
                "allbytes"));
        }

        return Task.WhenAll(SavePoints(writeApiAsync, points.ToArray()));
    }

    public Task Stop() => Task.CompletedTask;

    public void Dispose()
    {
    }
}