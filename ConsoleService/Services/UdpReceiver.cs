﻿using System.Net.Sockets;
using System.Text;
using Common;
using Common.Models;
using ConsoleService.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConsoleService.Services;

public class UdpReceiver : BackgroundService
{
    private readonly ILogger<UdpReceiver> _logger;
    private readonly LbsService _lbsService;
    private readonly UdpReceiverSettings _settings;
    private readonly WaitingForAppStartupService _waitingService;

    public UdpReceiver(WaitingForAppStartupService waitingService, IOptions<UdpReceiverSettings> settings, ILogger<UdpReceiver> logger, LbsService lbsService)
    {
        _logger = logger;
        _settings = settings.Value;
        _lbsService = lbsService;
        _waitingService = waitingService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!await _waitingService.WaitForAppStartup(stoppingToken))
            {
                return;
            }
            
            using var receiver = new UdpClient(_settings.ListeningPort);
            using var sender = new UdpClient(_settings.Ip, _settings.SendingPort);

            while (!stoppingToken.IsCancellationRequested)
            {
                var result = await receiver.ReceiveAsync(stoppingToken);

                var message = Encoding.UTF8.GetString(result.Buffer);

                if (Point.TryParse(message, out var point))
                {
                    if (_lbsService.TryGetCoordinates(point!, out var coords))
                    {
                        _logger.LogInformation(coords.ToString());

                        await sender.SendAsync(Encoding.UTF8.GetBytes(coords.ToString()), stoppingToken);
                    }
                }
            }
        }
        catch (OperationCanceledException exception)
        {
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }
}