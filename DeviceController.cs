using System;
using System.Threading;
using System.Threading.Tasks;

namespace OwenModbusMonitor
{
    public class DeviceController(string ip, int port) : IDisposable
    {
        private readonly ModbusService _modbusService = new ModbusService(ip, port);
        private CancellationTokenSource? _cts;
        private bool _isWriting = false;

        // --- КАРТА РЕГИСТРОВ ---
        private const int Addr_Start = 16384;
        private const int Addr_Stop = 16385;
        private const int Addr_Ustavka = 16386;
        private const int Addr_Davlenie = 16388;
        private const int Addr_StopMaxSet = 16390;
        private const int Addr_Utechka = 16391;
        private const int Addr_OverPress = 16392;
        private const int Addr_ErrDD = 16393;
        private const int Addr_Err = 16394;
        private const int Addr_Success = 16395;
        private const int Addr_Fail = 16396;

        private const int UnitId = 1;

        // --- ПЕРЕМЕННЫЕ ---
        // Аналоговые (Float)
        public MonitoredFloat Ustavka { get; } = new MonitoredFloat();
        public MonitoredFloat Davlenie { get; } = new MonitoredFloat();

        // Дискретные (Short)
        public MonitoredShort StartVar { get; } = new MonitoredShort();
        public MonitoredShort StopVar { get; } = new MonitoredShort();
        public MonitoredShort StopMaxSet { get; } = new MonitoredShort();
        public MonitoredShort Utechka { get; } = new MonitoredShort();
        public MonitoredShort OverPress { get; } = new MonitoredShort();
        public MonitoredShort ErrDD { get; } = new MonitoredShort();
        public MonitoredShort Err { get; } = new MonitoredShort();
        public MonitoredShort Success { get; } = new MonitoredShort();
        public MonitoredShort Fail { get; } = new MonitoredShort();

        public bool IsConnected => _modbusService.IsConnected;

        public void Connect() => _modbusService.Connect();
        public void Disconnect() => _modbusService.Disconnect();

        public void StartMonitoring()
        {
            if (_cts != null && !_cts.IsCancellationRequested) return;

            _cts = new CancellationTokenSource();
            // Запускаем цикл опроса в фоновом потоке
            Task.Run(() => PollLoop(_cts.Token));
        }

        public void StopMonitoring()
        {
            _cts?.Cancel();
        }

        private async Task PollLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!_modbusService.IsConnected || _isWriting)
                {
                    await Task.Delay(200, token);
                    continue;
                }

                try
                {
                    // Чтение блока данных (13 регистров) для оптимизации
                    // Читаем всё сразу от Addr_Start до Addr_Fail одним запросом
                    var data = await _modbusService.ReadBlockAsync(UnitId, Addr_Start, 13);

                    // Разбор данных (индексы смещены относительно Addr_Start)
                    StartVar.CurrentValue = data[Addr_Start - Addr_Start];
                    StopVar.CurrentValue = data[Addr_Stop - Addr_Start];

                    // Float занимает 2 регистра, используем вспомогательный метод
                    Ustavka.CurrentValue = ParseFloat(data, Addr_Ustavka - Addr_Start);
                    Davlenie.CurrentValue = ParseFloat(data, Addr_Davlenie - Addr_Start);

                    StopMaxSet.CurrentValue = data[Addr_StopMaxSet - Addr_Start];
                    Utechka.CurrentValue = data[Addr_Utechka - Addr_Start];
                    OverPress.CurrentValue = data[Addr_OverPress - Addr_Start];
                    ErrDD.CurrentValue = data[Addr_ErrDD - Addr_Start];
                    Err.CurrentValue = data[Addr_Err - Addr_Start];
                    Success.CurrentValue = data[Addr_Success - Addr_Start];
                    Fail.CurrentValue = data[Addr_Fail - Addr_Start];
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка Modbus: {ex.Message}");
                }

                await Task.Delay(100, token);
            }
        }

        private float ParseFloat(short[] registers, int offset)
        {
            // Получаем два слова (регистра)
            short highWord = registers[offset];
            short lowWord = registers[offset + 1];

            // Конвертируем слова в байты
            byte[] lowBytes = BitConverter.GetBytes(lowWord);
            byte[] highBytes = BitConverter.GetBytes(highWord);

            // Собираем float: для Little Endian (PC) порядок байт: [LowWord_Low, LowWord_High, HighWord_Low, HighWord_High]
            // Это соответствует перестановке слов (Word Swap), стандартной для Modbus Float
            byte[] floatBytes = { lowBytes[0], lowBytes[1], highBytes[0], highBytes[1] };

            return BitConverter.ToSingle(floatBytes, 0);
        }

        public async Task WriteStartAsync()
        {
            _isWriting = true;
            try
            {
                await _modbusService.WriteShortAsync(UnitId, Addr_Start, 1);
                await _modbusService.WriteShortAsync(UnitId, Addr_Stop, 0);
            }
            finally { _isWriting = false; }
        }

        public async Task WriteStopAsync()
        {
            _isWriting = true;
            try
            {
                await _modbusService.WriteShortAsync(UnitId, Addr_Stop, 1);
                await _modbusService.WriteShortAsync(UnitId, Addr_Start, 0);
            }
            finally { _isWriting = false; }
        }

        public async Task WriteUstavkaAsync(float value)
        {
            _isWriting = true;
            try
            {
                await _modbusService.WriteFloatAsync(UnitId, Addr_Ustavka, value);
            }
            finally { _isWriting = false; }
        }

        public async Task ResetAllAsync()
        {
            if (_modbusService.IsConnected)
            {
                await _modbusService.WriteShortAsync(UnitId, Addr_Start, 0);
                await _modbusService.WriteShortAsync(UnitId, Addr_Stop, 0);
                await _modbusService.WriteFloatAsync(UnitId, Addr_Ustavka, 0);
                // Дополнительные сбросы можно добавить здесь
            }
        }

        public void Dispose()
        {
            StopMonitoring();
            _modbusService?.Dispose();
        }
    }
}