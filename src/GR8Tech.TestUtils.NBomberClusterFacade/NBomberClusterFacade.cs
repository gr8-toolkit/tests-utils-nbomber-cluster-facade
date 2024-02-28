using GR8Tech.TestUtils.NBomberClusterFacade.CommonSettings;
using GR8Tech.TestUtils.NBomberClusterFacade.Enums;
using GR8Tech.TestUtils.NBomberClusterFacade.Extensions;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace GR8Tech.TestUtils.NBomberClusterFacade;

public static class NBomberClusterFacade
{
    private static IConfigurationRoot _сonfiguration;
    private static CustomClusterContext _context;
    public static ClusterSettings ClusterSettings { get; }
    public static InfluxDBCustomSink InfluxDbSink { get; }
    public static CustomClusterContext Context => _context;

    public static string TestName { get; }

    public static LoggerConfiguration LoggerConfiguration =>
        new LoggerConfiguration().ReadFrom.Configuration(_сonfiguration);

    static NBomberClusterFacade()
    {
        IConfigurationBuilder configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory());

        if (File.Exists("test-settings.json"))
        {
            configurationBuilder
                .AddJsonFile("test-settings.json", false, true)
                .AddJsonFile($"test-settings.{Environment.GetEnvironmentVariable("ENVIRONMENT")}.json", true, true);
        }
        else
        {
            configurationBuilder
                .AddJsonFile("appsettings.json", false, true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ENVIRONMENT")}.json", true, true);
        }

        var configuration = configurationBuilder
            .AddEnvironmentVariables()
            .Build();

        _сonfiguration = configuration;
        ClusterSettings = configuration.GetSection("ClusterSettings").Get<ClusterSettings>()!;
        ValidateSettings();

        var loggerConfiguration = new LoggerConfiguration().ReadFrom.Configuration(_сonfiguration);
        Log.Logger = loggerConfiguration.CreateLogger();
        Log.Information($"ClusterSettings: \n{ClusterSettings.ToReadableString()}");

        InfluxDbSink = new InfluxDBCustomSink(
            ClusterSettings.InfluxDB.Url,
            ClusterSettings.InfluxDB.User,
            ClusterSettings.InfluxDB.Password,
            ClusterSettings.InfluxDB.DataBaseName);

        InfluxDbSink.GlobalMetricTags = ClusterSettings.ReportingTags.CommonTags;

        _context = new CustomClusterContext(
            ClusterSettings.RunnerSettings.Type,
            ClusterSettings.MinAgentsCount,
            new HttpClient
            {
                BaseAddress = new Uri(ClusterSettings.CoordinatorHostUrl)
            });

        if (ClusterSettings.RunnerSettings.Type == RunnerType.coordinator)
        {
            foreach (var scenario in ClusterSettings.CoordinatorScenarios)
                Context.RegisterScenarioInternalAsync(scenario);

            if (ClusterSettings.MinAgentsCount == 0)
                Context.DoesClusterReady = true;
            
            Log.Information("Coordinator has registered own scenarios");
        }
    }

    public static async Task SynchronizeCluster(int retries = 600, int delayMs = 1000)
    {
        if (ClusterSettings.RunnerSettings.Type == RunnerType.agent)
        {
            await Context.RegisterAgentAsync();

            var scenarios = ClusterSettings.Agents.ToList()
                .Find(x => x.AgentGroup == ClusterSettings.RunnerSettings.AgentGroup)?.TargetScenarios;
            
            Log.Information($"Agent will register next scenarios: {scenarios?.ToReadableString()}");

            if (scenarios != null && scenarios.Length > 0)
            {
                foreach (var scenario in scenarios)
                    await Context.RegisterScenarioAsync(scenario);
            }
        }

        Log.Information("Starting the cluster synchronization...");
        await Context.SynchronizeClusterAsync(retries, delayMs);
        Log.Information("Cluster synchronization has been completed successfully");

        if (ClusterSettings.RunnerSettings.Type == RunnerType.coordinator)
            Log.Information($"Cluster context after synchronization:\n " + Context.Cache.ToReadableString());
    }

    private static void ValidateSettings()
    {
        if (string.IsNullOrEmpty(ClusterSettings.NATSServerUrl))
            throw new Exception("NATSServerUrl is Null or Empty");

        if (string.IsNullOrEmpty(ClusterSettings.CoordinatorHostUrl))
            throw new Exception("CoordinatorHostUrl is Null or Empty");

        if (ClusterSettings.InfluxDB == null)
            throw new Exception("InfluxDB is Null");

        if (string.IsNullOrEmpty(ClusterSettings.InfluxDB.Url))
            throw new Exception("InfluxDB.Url is Null or Empty");

        if (string.IsNullOrEmpty(ClusterSettings.InfluxDB.Password))
            throw new Exception("InfluxDB.Password is Null or Empty");

        if (string.IsNullOrEmpty(ClusterSettings.InfluxDB.User))
            throw new Exception("InfluxDB.User is Null or Empty");

        if (string.IsNullOrEmpty(ClusterSettings.InfluxDB.DataBaseName))
            throw new Exception("InfluxDB.DataBaseName is Null or Empty");

        if (ClusterSettings.ReportingTags == null)
            throw new Exception("ReportingTags is Null");

        if (ClusterSettings.ReportingTags.CommonTags == null || ClusterSettings.ReportingTags.CommonTags.Count == 0)
            throw new Exception("ReportingTags.CommonTags is Null or Count == 0");

        if (!ClusterSettings.ReportingTags.CommonTags.ContainsKey("test_name"))
            throw new Exception("ReportingTags.CommonTags[test_name] is NOT found, but required");

        if (!ClusterSettings.ReportingTags.CommonTags.ContainsKey("pipeline"))
            throw new Exception("ReportingTags.CommonTags[pipeline] is NOT found, but required");

        if (string.IsNullOrEmpty(ClusterSettings.ClusterId))
            throw new Exception("ClusterId is Null or Empty");

        if (ClusterSettings.RunnerSettings == null)
            throw new Exception("RunnerSettings is Null");

        if (ClusterSettings.RunnerSettings == null)
            throw new Exception("RunnerSettings is Null");

        if (ClusterSettings.RunnerSettings.Type != RunnerType.agent &&
            ClusterSettings.RunnerSettings.Type != RunnerType.coordinator)
            throw new Exception("Runner Type is undefined");

        if (ClusterSettings.RunnerSettings.Type == RunnerType.agent &&
            string.IsNullOrEmpty(ClusterSettings.RunnerSettings.AgentGroup))
            throw new Exception("AgentGroup is Null or Empty");
    }

    public static List<string> GetAllScenariosNames()
    {
        var scenariosNames = new List<string>();
        scenariosNames.AddRange(ClusterSettings.CoordinatorScenarios);

        foreach (var agent in ClusterSettings.Agents)
        {
            foreach (var scenarioName in agent.TargetScenarios)
            {
                if (!scenariosNames.Contains(scenarioName))
                    scenariosNames.Add(scenarioName);
            }
        }

        return scenariosNames;
    }

    public static async Task AnnounceTheStartOfTheTest()
    {
        if (ClusterSettings.RunnerSettings.Type == RunnerType.coordinator)
            await InfluxDbSink.SaveAnnotationToInfluxDBAsync("---> Test started",
                ClusterSettings.ReportingTags.AnnotationTags);
    }

    public static async Task AnnounceTheEndOfTheTest()
    {
        if (ClusterSettings.RunnerSettings.Type == RunnerType.coordinator)
            await InfluxDbSink.SaveAnnotationToInfluxDBAsync("<--- Test ended",
                ClusterSettings.ReportingTags.AnnotationTags);
    }

    public static async Task<string> GetConfigFilePath()
    {
        string configFilePath;

        if (ClusterSettings.RunnerSettings.Type == RunnerType.coordinator)
        {
            configFilePath = await ClusterConfigGenerator.GenerateCoordinatorConfig(
                ClusterSettings.ClusterId,
                ClusterSettings.MinAgentsCount,
                ClusterSettings.NATSServerUrl,
                ClusterSettings.CoordinatorScenarios,
                ClusterSettings.Agents);

            Log.Information("============>  ConfigFile for Coordinator was generated");
        }
        else
        {
            configFilePath = await ClusterConfigGenerator.GenerateAgentConfig(
                ClusterSettings.ClusterId,
                ClusterSettings.NATSServerUrl,
                ClusterSettings.RunnerSettings.AgentGroup);

            Log.Information($"============>  ConfigFile for Agent was generated");
        }

        return configFilePath;
    }
}