using GR8Tech.TestUtils.NBomberClusterFacade.Enums;

namespace GR8Tech.TestUtils.NBomberClusterFacade.CommonSettings;

public sealed class ClusterSettings
{
    public string NATSServerUrl { get; set; }
    public InfluxDB InfluxDB { get; set; }
    public ReportingTags ReportingTags { get; set; }
    public string ClusterId { get; set; }
    public string License { get; set; }
    public string CoordinatorHostUrl { get; set; }
    public int MinAgentsCount { get; set; }
    public RunnerSettings RunnerSettings { get; set; }
    public string[] CoordinatorScenarios { get; set; }
    public Agent[] Agents { get; set; }
}

public sealed class ReportingTags
{
    public Dictionary<string, string> CommonTags { get; set; }
    public Dictionary<string, string> AnnotationTags { get; set; }
}

public sealed class InfluxDB
{
    public string Url { get; set; }
    public string DataBaseName { get; set; }
    public string User { get; set; }
    public string Password { get; set; }
}

public sealed class RunnerSettings
{
    public RunnerType Type { get; set; }
    public string AgentGroup { get; set; }
}

public sealed class Agent
{
    public string AgentGroup { get; set; }
    public string[] TargetScenarios { get; set; }
}