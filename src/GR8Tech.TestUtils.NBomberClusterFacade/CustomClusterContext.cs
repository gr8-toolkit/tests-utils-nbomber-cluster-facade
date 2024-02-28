using System.Collections.Concurrent;
using System.Net;
using System.Text;
using GR8Tech.TestUtils.NBomberClusterFacade.Enums;
using Newtonsoft.Json;
using Polly;
using Serilog;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;
using WireMock.Types;
using WireMock.Util;

namespace GR8Tech.TestUtils.NBomberClusterFacade;

public sealed class CustomClusterContext
{
    private HttpClient _client;
    private static string SetCacheByName => "/api/Data/SetCacheByName";
    private static string GetCacheByName => "/api/Data/GetCacheByName";
    private static string RegisterScenarioByName => "/api/Data/RegisterScenarioByName";
    private static string GetScenarioExecutorsCountByName => "/api/Data/GetScenarioExecutorsCountByName";
    private static string ReadinessProbe => "/api/Data/ReadinessProbe";
    private static string RegisterAgent => "/api/Data/RegisterAgent";
    private static string DoesClusterSynchronized => "/api/Data/DoesClusterSynchronized";

    private WireMockServer _server;
    private int _clusterSize;
    private ConcurrentDictionary<string, string> _cache;
    private object _lockObject = new();

    internal ConcurrentDictionary<string, string> Cache => _cache;

    public CustomClusterContext(RunnerType runnerType, int clusterSize, HttpClient client)
    {
        _client = client;
        _clusterSize = clusterSize;

        if (runnerType == RunnerType.coordinator)
        {
            _cache = new ConcurrentDictionary<string, string>();
            _cache.TryAdd("registeredAgents", "0");

            _server = WireMockServer.Start(new WireMockServerSettings()
            {
                StartAdminInterface = true,
                Urls = new[] { "http://localhost:5777" }
            });

            SetCacheHandler();
            GetCacheHandler();
            SetScenarioHandler();
            GetScenarioHandler();
            ReadinessProbeHandler();
            RegisterAgentHandler();
            DoesClusterSynchronizedHandler();
        }

        Task.WaitAll(WaitContextReadiness());
        Log.Information("Cluster context has been created");
    }

    private void DoesClusterSynchronizedHandler()
    {
        _server
            .Given(Request.Create()
                .UsingGet()
                .WithPath(DoesClusterSynchronized))
            .RespondWith(Response.Create()
                .WithCallback(_ =>
                {
                    var response = new ResponseMessage();

                    response.StatusCode = _cache.TryGetValue("registeredAgents", out string? value)
                        ? (int.Parse(value) == _clusterSize ? 200 : 204)
                        : 204;

                    return response;
                })
            );
    }

    private async Task WaitContextReadiness()
    {
        Log.Information("Wait for the Cluster context to be ready...");

        await Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(600, _ =>
            {
                Console.Write(".");
                return TimeSpan.FromMilliseconds(1000);
            })
            .ExecuteAsync(async () =>
            {
                var response = await _client.GetAsync(ReadinessProbe);

                if (response.StatusCode != HttpStatusCode.OK)
                    throw new Exception("Cluster Context is not ready. Exit after timeout of 10 minutes");

                return response;
            });
    }

    private void ReadinessProbeHandler()
    {
        _server
            .Given(Request.Create()
                .UsingGet()
                .WithPath(ReadinessProbe))
            .RespondWith(Response.Create().WithStatusCode(200));
    }

    private void RegisterAgentHandler()
    {
        _server
            .Given(Request.Create()
                .UsingPost()
                .WithPath(RegisterAgent))
            .RespondWith(Response.Create()
                .WithCallback(_ =>
                {
                    var response = new ResponseMessage
                    {
                        StatusCode = 200
                    };

                    lock (_lockObject)
                    {
                        _cache.TryGetValue("registeredAgents", out string registeredAgents);

                        if (_clusterSize == int.Parse(registeredAgents))
                        {
                            response = new ResponseMessage
                            {
                                StatusCode = 204
                            };
                        }
                        else
                        {
                            _cache.AddOrUpdate(
                                "registeredAgents",
                                "1",
                                (key, oldValue) => (int.Parse(oldValue) + 1).ToString());
                        }
                    }

                    return response;
                })
            );
    }

    private void SetScenarioHandler()
    {
        _server
            .Given(Request.Create()
                .UsingPost()
                .WithPath(RegisterScenarioByName))
            .RespondWith(Response.Create()
                .WithCallback(requestMessage =>
                {
                    var name = requestMessage.Query!["name"];

                    lock (_lockObject)
                    {
                        _cache.AddOrUpdate(
                            name[0],
                            "1",
                            (key, oldValue) => (int.Parse(oldValue) + 1).ToString());
                    }

                    var response = new ResponseMessage
                    {
                        StatusCode = 200,
                    };

                    return response;
                })
            );
    }

    private void GetScenarioHandler()
    {
        _server
            .Given(Request.Create()
                .UsingGet()
                .WithPath(GetScenarioExecutorsCountByName))
            .RespondWith(Response.Create()
                .WithCallback(requestMessage =>
                {
                    var name = requestMessage.Query!["name"];
                    var response = new ResponseMessage();

                    if (_cache.TryGetValue(name[0], out string value))
                    {
                        response.StatusCode = 200;
                        response.BodyData = new BodyData
                        {
                            BodyAsString = value,
                            DetectedBodyType = BodyType.String
                        };
                    }
                    else
                    {
                        response.StatusCode = 204;
                    }

                    return response;
                })
            );
    }

    private void SetCacheHandler()
    {
        _server
            .Given(Request.Create()
                .UsingPost()
                .WithPath(SetCacheByName))
            .RespondWith(Response.Create()
                .WithCallback(requestMessage =>
                {
                    var name = requestMessage.Query!["name"];

                    lock (_lockObject)
                    {
                        _cache.AddOrUpdate(
                            name[0],
                            requestMessage.Body,
                            (key, oldValue) => requestMessage.Body);
                    }

                    var response = new ResponseMessage()
                    {
                        StatusCode = 200,
                    };

                    return response;
                })
            );
    }

    private void GetCacheHandler()
    {
        _server
            .Given(Request.Create()
                .UsingGet()
                .WithPath(GetCacheByName))
            .RespondWith(Response.Create()
                .WithCallback(requestMessage =>
                {
                    var name = requestMessage.Query!["name"];
                    var response = new ResponseMessage();

                    if (_cache.TryGetValue(name[0], out string value))
                    {
                        response.StatusCode = 200;
                        response.BodyData = new BodyData()
                        {
                            BodyAsString = value,
                            DetectedBodyType = BodyType.String
                        };
                    }
                    else
                    {
                        response.StatusCode = 204;
                    }

                    return response;
                })
            );
    }

    internal async Task RegisterAgentAsync()
    {
        var httpRequestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(RegisterAgent, UriKind.Relative)
        };

        var response = await _client.SendAsync(httpRequestMessage);

        if (response.StatusCode == HttpStatusCode.NoContent)
            throw new Exception("Cannot register more Agents than defined in MinAgentsCount variable");
        
        Log.Information("Agent has been registered");
    }

    public async Task SetDataByKeyAsync<T>(string key, T payload)
    {
        var httpRequestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"{SetCacheByName}?name={key}", UriKind.Relative),
            Headers =
            {
                { HttpRequestHeader.Accept.ToString(), "text/plain" },
            },
            Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "text/plain")
        };

        await _client.SendAsync(httpRequestMessage);
    }

    public async Task<T?> WaitAndGetDataByKeyAsync<T>(string key, int retryCount = 300, int delayMs = 1000)
    {
        var response = await Policy
            .Handle<Exception>()
            .OrResult<HttpResponseMessage>(r => r.StatusCode != HttpStatusCode.OK)
            .WaitAndRetryAsync(retryCount, retryAttempt => TimeSpan.FromMilliseconds(delayMs))
            .ExecuteAsync(async () => await _client.GetAsync($"{GetCacheByName}?name={key}"));

        var result = JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync());

        if (result == null)
            throw new Exception(
                $"Data with the key '{key}' has not been found in the Cluster context after {retryCount * delayMs} ms");

        return result;
    }

    internal bool DoesClusterReady { get; set; }

    public async Task SynchronizeClusterAsync(int retryCount = 600, int delayMs = 1000)
    {
        if (DoesClusterReady)
            return;

        await Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(retryCount, _ => TimeSpan.FromMilliseconds(delayMs))
            .ExecuteAsync(async () =>
            {
                Console.Write(".");
                var response = await _client.GetAsync(DoesClusterSynchronized);

                if (response.StatusCode != HttpStatusCode.OK)
                    throw new Exception($"Cluster synchronization has been failed after {retryCount * delayMs} ms");
            });

        DoesClusterReady = true;
    }

    internal async Task RegisterScenarioAsync(string scenarioName)
    {
        var httpRequestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"{RegisterScenarioByName}?name={scenarioName}", UriKind.Relative)
        };

        await _client.SendAsync(httpRequestMessage);
    }

    internal void RegisterScenarioInternalAsync(string scenarioName)
    {
        lock (_lockObject)
        {
            _cache.AddOrUpdate(
                scenarioName,
                "1",
                (key, oldValue) => (int.Parse(oldValue) + 1).ToString());
        }
    }

    public int GetScenarioExecutorsCount(string scenarioName)
    {
        if (!DoesClusterReady)
            throw new Exception(
                "Cluster has not been synchronized yet in order to provide Scenario details. " +
                "Please make sure you use 'NBomberClusterFacade.SynchronizeCluster()' in case you set MinAgentsCount > 0");

        if (NBomberClusterFacade.GetAllScenariosNames().All(x => x != scenarioName))
            throw new Exception($"Unknown Scenario name: {scenarioName}");

        var response = _client.GetAsync($"{GetCacheByName}?name={scenarioName}").Result;

        if (response.StatusCode == HttpStatusCode.NoContent)
            return 0;

        var result = JsonConvert.DeserializeObject<int?>(response.Content.ReadAsStringAsync().Result);

        return (int)result!;
    }
}