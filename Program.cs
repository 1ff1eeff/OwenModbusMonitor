using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OwenModbusMonitor;
using System;
using System.IO;

// Создаем папку wwwroot, если она отсутствует, чтобы избежать ошибки WebRootPath
Directory.CreateDirectory("wwwroot");

var builder = WebApplication.CreateBuilder(args);

// Настраиваем прослушивание порта 5000
builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(5000));

// Регистрируем DeviceController как Singleton сервис
builder.Services.AddSingleton<DeviceController>(sp =>
{
    var controller = new DeviceController("127.0.0.1", 502);
    controller.Connect();
    controller.StartMonitoring();
    return controller;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/data", (DeviceController device) =>
{
    return new
    {
        pressure = device.Davlenie.HasValue ? device.Davlenie.CurrentValue : (float?)null,
        setpoint = device.Ustavka.HasValue ? device.Ustavka.CurrentValue : (float?)null,
        timestamp = DateTime.Now
    };
});

// Эндпоинты для управления опросом
app.MapPost("/api/start", (DeviceController device) => device.StartMonitoring());
app.MapPost("/api/stop", (DeviceController device) => device.StopMonitoring());
app.MapPost("/api/setpoint", async (DeviceController device, SetpointRequest req) => await device.WriteUstavkaAsync(req.Value));

Console.WriteLine("Веб-сервер запущен: http://localhost:5000");
app.Run();

record SetpointRequest(float Value);
