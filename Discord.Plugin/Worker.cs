using hu.czompisoftware.libraries.extensions;
using hu.czompisoftware.libraries.general;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TouchPortalApi.Interfaces;
using TouchPortalApi.Models;
using WatsonWebsocket;

namespace Discord.Plugin
{
    internal class Worker : BackgroundService
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly ILogger<Worker> _logger;
        private readonly IMessageProcessor _messageProcessor;
#if !SKIP_WS
        private readonly WatsonWsServer _server;
#endif
        public Worker(IHostApplicationLifetime hostApplicationLifetime, ILogger<Worker> logger, IMessageProcessor messageProcessor)
        {
            _hostApplicationLifetime = hostApplicationLifetime;
            _logger = logger;
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
#if !SKIP_WS
            _server = new WatsonWsServer("127.0.0.1", 4321, false);
#endif
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var stopRequested = false;
#if !SKIP_WS
            // SetUp Server for GoXLR:
            _server.ClientConnected += async (sender, args) =>
            {
                //When GoXLR is connected, ask for Profiles.
                Logger.Error("Client connected: " + args.IpPort);
                _logger.LogInformation("Client connected: " + args.IpPort);

                try
                {
                    //var model = GetProfilesRequest.Create();
                    //var json = System.Text.Json.JsonSerializer.Serialize(model);

                    //_logger.LogInformation(json);
                    //await _server.SendAsync(args.IpPort, json, stoppingToken);
                }
                catch (Exception e)
                {
                    Logger.Error($"{e}");
                    _logger.LogError(e.ToString());
                }
            };

            _server.ClientDisconnected += (sender, args) =>
            {
                Logger.Error("Client disconnected: " + args.IpPort);
            };

            _server.MessageReceived += (sender, args) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(args.Data);
                    Logger.Error("Message received from " + args.IpPort + ": " + json);
                    _logger.LogInformation("Message received from " + args.IpPort);

                    //var response = JsonSerializer.Deserialize<GetProfilesResponse>(json);

                    _messageProcessor.UpdateChoice(new ChoiceUpdate
                    {
                        Id = PluginActionType.DiscordSelfMute.ToString().ToSnakeCase(),
                        //Value = response?.Payload.Profiles ?? new[] { "No profiles!" }
                        Value = new[] { "No profiles!" }
                    });
                }
                catch (Exception e)
                {
                    Logger.Error($"{e}");
                    _logger.LogError(e.ToString());
                }
            };

            _server.Start();
#endif
            //Setup Client for TouchPortal:

            // On Plugin Connect Event
            _messageProcessor.OnConnectEventHandler += () =>
            {
                Logger.Error($"Plugin Connected to TouchPortal");
            };

            // On Action Event
            _messageProcessor.OnActionEvent += async (actionId, dataList) =>
            {
                Logger.Error($"Action Event Fired: {actionId}");
#if !SKIP_WS

                try
                {
                    var clients = _server.ListClients();
                    var client = clients.SingleOrDefault();

                    if (client != null)
                    {
                        Logger.Error($"Discord.Plugin Client found: {client}");

                        Enum.TryParse(typeof(PluginActionType), actionId, out object? output);
                        PluginActionType? actionType = output as PluginActionType?;
                        switch (actionType)
                        {
                            case PluginActionType.DiscordSelfMute:
                            case PluginActionType.DiscordSelfDeafen:
                                {
                                    var profile = dataList.Single().Value;
                                    //await _server.SendAsync(client, json, stoppingToken);

                                    break;
                                }
                        }
                    }
                    else
                    {
                        Logger.Error("No Discord Clients connected. Restart the Discord App");
                    }
                }
                catch (Exception e)
                {
                    Logger.Error($"{e}");
                }

#endif
                foreach (var actionData in dataList)
                {
                    Logger.Error($"Id: {actionData.Id} Value: {actionData.Value}");
                }
            };

            // On List Change Event
            _messageProcessor.OnListChangeEventHandler += (actionId, value) =>
            {
                Logger.Error($"Choice Event Fired.");
            };

            // On Plugin Disconnect
            _messageProcessor.OnCloseEventHandler += () =>
            {
                Console.Write($"Plugin Quit Command");
                stopRequested = true;
            };

            // Send State Update
            _messageProcessor.UpdateState(new StateUpdate { Id = "SomeStateId", Value = "New Value" });

            // Send Choice Update
            _messageProcessor.UpdateChoice(new ChoiceUpdate
            {
                Id = PluginActionType.DiscordSelfMute.ToString().ToSnakeCase(),
                Value = new[] { "No Value" }
            });

            // Run Listen and pairing
            _ = Task.WhenAll(new Task[]
            {
                _messageProcessor.Listen(),
                _messageProcessor.TryPairAsync()
            });

            try
            {
                // Do whatever you want in here
                while (!stoppingToken.IsCancellationRequested && !stopRequested)
                {
                    //_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    await Task.Delay(1000, stoppingToken);
                }
            }
            finally
            {
                _hostApplicationLifetime.StopApplication();
            }
        }
    }
}