using OwenModbusMonitor;
using System;

Console.WriteLine("Запуск мониторинга устройства ПР-103...");

string ipAddress = "127.0.0.1";
int port = 502;

// Создаем контроллер. Using гарантирует вызов Dispose (и Disconnect) при завершении.
using var controller = new DeviceController(ipAddress, port);

try
{
    controller.Connect();
    Console.WriteLine($"Успешное подключение к {ipAddress}:{port}");

    // Подписка на изменение значения давления (регистр 16388, свойство Davlenie)
    controller.Davlenie.ValueChanged += (s, value) =>
    {
        // \r возвращает курсор в начало строки, а Write (без Line) не создает новую строку
        Console.Write($"\r[{DateTime.Now:T}] Давление: {value:F2}   ");
    };

    controller.StartMonitoring();
    Console.WriteLine("Мониторинг запущен. Нажмите Enter для выхода.");
    Console.ReadLine();
}
catch (Exception ex)
{
    Console.WriteLine($"Критическая ошибка: {ex.Message}");
}
