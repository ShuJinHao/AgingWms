using AgingWms.Client;
using AgingWms.UseCases.Services.Notify;
using AgingWms.UseCases.Services.Request;
using AgingWms.Workflow.Services; // 【核心修复】必须引用这个，否则找不到 AgingJobService
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using SharedKernel.Contracts;
using SharedKernel.Dto;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace AgingWms.Client
{
    public partial class MainWindow : Window
    {
        private readonly RealTimeMonitorService _monitorService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly WmsRequestService _wmsService;
        public ObservableCollection<SlotMonitorViewModel> MonitorList { get; set; }

        public MainWindow(
             RealTimeMonitorService monitorService,
             IServiceScopeFactory scopeFactory,
             // 【注入】构造函数直接注入，不要再去 GetRequiredService 了
             WmsRequestService wmsService)
        {
            InitializeComponent();

            _monitorService = monitorService;
            _scopeFactory = scopeFactory;
            _wmsService = wmsService; // 【赋值】

            MonitorList = new ObservableCollection<SlotMonitorViewModel>();
            dgMonitor.ItemsSource = MonitorList;

            _monitorService.OnTelemetryReceived += OnTelemetryReceived;
            _monitorService.OnStepStateReceived += OnStepStateReceived;

            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadActiveSlotsAsync();
        }

        private async Task LoadActiveSlotsAsync()
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<SharedKernel.Repositoy.IReadRepository<AgingWms.Core.Domain.WarehouseSlot>>();
                    var allSlots = await repository.GetListAsync();
                    var activeSlots = allSlots.Where(x => x.Status != AgingWms.Core.Domain.SlotStatus.Empty);

                    if (activeSlots == null || !activeSlots.Any()) return;

                    foreach (var slot in activeSlots)
                    {
                        if (MonitorList.Any(x => x.SlotId == slot.Id)) continue;

                        var vm = new SlotMonitorViewModel
                        {
                            SlotId = slot.Id,
                            TrayBarcode = slot.TrayBarcode ?? "Unknown",
                            Status = slot.Status.ToString(),
                            CurrentStep = "恢复中...",
                            Voltage = 0,
                            Current = 0
                        };
                        MonitorList.Add(vm);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"加载列表失败: {ex.Message}");
            }
        }

        // =============================================================
        // 按钮逻辑：WMS 操作
        // =============================================================
        // 1. 批量入库
        // 1. 批量入库
        private async void btnWrite_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetBatchSlotIds();
            Log($"[开始] 批量入库 {ids.Count} 个...");

            foreach (var id in ids)
            {
                try
                {
                    if (!int.TryParse(txtCellCount.Text, out int cCount)) cCount = 24;
                    var cells = CreateMockCells(cCount, id);
                    string trayCode = $"{txtTrayCode.Text}_{id}";

                    // 直接使用注入的服务成员变量
                    var result = await _wmsService.WriteSlotAsync(id, trayCode, cells);

                    if (result.IsSuccess)
                    {
                        Log($" -> 写入 {id} 成功");
                        var vm = GetOrAddViewModel(id);
                        vm.TrayBarcode = trayCode;
                        vm.Status = "已入库";
                    }
                    else
                    {
                        Log($" -> 写入 {id} 失败: {result.Message}");
                    }
                }
                catch (Exception ex) { Log($"异常: {ex.Message}"); }
            }
        }

        // 2. 库位迁移
        private async void btnMove_Click(object sender, RoutedEventArgs e)
        {
            string src = txtSlotId.Text.Trim();
            string tgt = txtTargetSlotId.Text.Trim();

            try
            {
                // 直接调用，没有 Scope
                var result = await _wmsService.MoveSlotAsync(src, tgt);

                Log(result.IsSuccess ? $"[成功] {result.Message}" : $"[失败] {result.Message}");

                if (result.IsSuccess)
                {
                    var srcVm = MonitorList.FirstOrDefault(x => x.SlotId == src);
                    string movingTray = srcVm?.TrayBarcode ?? "Unknown";
                    if (srcVm != null) MonitorList.Remove(srcVm);

                    var tgtVm = GetOrAddViewModel(tgt);
                    tgtVm.Status = "占用";
                    tgtVm.TrayBarcode = movingTray;
                }
            }
            catch (Exception ex) { Log($"[异常] {ex.Message}"); }
        }

        // 3. 库位移除
        private async void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetBatchSlotIds();

            foreach (var id in ids)
            {
                try
                {
                    // 直接调用
                    var result = await _wmsService.RemoveSlotAsync(id);
                    Log($"移除 {id}: {result.Message}");

                    if (result.IsSuccess)
                    {
                        var vm = MonitorList.FirstOrDefault(x => x.SlotId == id);
                        if (vm != null) MonitorList.Remove(vm);
                    }
                }
                catch (Exception ex) { Log($"异常: {ex.Message}"); }
            }
        }

        // =============================================================
        // 按钮逻辑：工作流控制
        // =============================================================

        // =============================================================
        // 按钮逻辑：工作流控制 (全员 Request/Response)
        // =============================================================

        private async void btnStartWorkflow_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetBatchSlotIds();
            Log($"[指令] 启动 {ids.Count} 个库位的老化流程...");
            // ... (Steps 定义省略，保持原样) ...
            var steps = new List<JobStepConfigDto>
            {
                new JobStepConfigDto { StepType = "Rest", Parameters = JObject.FromObject(new { DurationMinutes = 1 }) },
                new JobStepConfigDto { StepType = "CC_Charge", Parameters = JObject.FromObject(new { TargetCurrent = 10.0, CutoffVoltage = 4.2, MaxDurationMinutes = 60 }) }
            };

            using (var scope = _scopeFactory.CreateScope())
            {
                var jobService = scope.ServiceProvider.GetRequiredService<AgingJobService>();

                foreach (var id in ids)
                {
                    try
                    {
                        var dto = new AgingJobDto { SlotId = id, BatchId = $"BATCH_{DateTime.Now:MMdd}_{id}", Steps = steps };

                        // 等待结果
                        var result = await jobService.StartJobAsync(dto);

                        if (result.IsSuccess) Log($" -> 启动成功: {id}");
                        else Log($" -> 启动失败 {id}: {result.Message}");
                    }
                    catch (Exception ex) { Log($"启动异常 {id}: {ex.Message}"); }
                }
            }
        }

        private async void btnPause_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetBatchSlotIds();
            using (var scope = _scopeFactory.CreateScope())
            {
                var jobService = scope.ServiceProvider.GetRequiredService<AgingJobService>();
                foreach (var id in ids)
                {
                    try
                    {
                        var result = await jobService.PauseJobAsync(id);
                        if (result.IsSuccess) Log($"[暂停] 成功 -> {id}");
                        else Log($"[暂停] 失败 -> {id}: {result.Message}");
                    }
                    catch (Exception ex) { Log($"[暂停] 异常 {id}: {ex.Message}"); }
                }
            }
        }

        private async void btnResume_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetBatchSlotIds();
            using (var scope = _scopeFactory.CreateScope())
            {
                var jobService = scope.ServiceProvider.GetRequiredService<AgingJobService>();
                foreach (var id in ids)
                {
                    try
                    {
                        var result = await jobService.ResumeJobAsync(id);
                        if (result.IsSuccess) Log($"[恢复] 成功 -> {id}");
                        else Log($"[恢复] 失败 -> {id}: {result.Message}");
                    }
                    catch (Exception ex) { Log($"[恢复] 异常 {id}: {ex.Message}"); }
                }
            }
        }

        private async void btnStop_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetBatchSlotIds();
            using (var scope = _scopeFactory.CreateScope())
            {
                var jobService = scope.ServiceProvider.GetRequiredService<AgingJobService>();
                foreach (var id in ids)
                {
                    try
                    {
                        var result = await jobService.StopJobAsync(id);
                        if (result.IsSuccess) Log($"[停止] 成功 -> {id}");
                        else Log($"[停止] 失败 -> {id}: {result.Message}");
                    }
                    catch (Exception ex) { Log($"[停止] 异常 {id}: {ex.Message}"); }
                }
            }
        }

        // =============================================================
        // 辅助方法
        // =============================================================
        private SlotMonitorViewModel GetOrAddViewModel(string slotId)
        {
            var vm = MonitorList.FirstOrDefault(x => x.SlotId == slotId);
            if (vm == null) { vm = new SlotMonitorViewModel { SlotId = slotId }; MonitorList.Add(vm); }
            return vm;
        }

        private void Log(string message)
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n"); txtLog.ScrollToEnd();
        }

        private List<string> GetBatchSlotIds()
        {
            var list = new List<string>();
            string startId = txtSlotId.Text.Trim();
            if (!int.TryParse(txtBatchCount.Text, out int count)) count = 1;
            var parts = startId.Split('-');
            if (parts.Length >= 3 && int.TryParse(parts.Last(), out int index))
            {
                string prefix = string.Join("-", parts.Take(parts.Length - 1));
                for (int i = 0; i < count; i++) list.Add($"{prefix}-{index + i}");
            }
            else list.Add(startId);
            return list;
        }

        // 【确保不重名】增加 slotId 参数
        private List<CellDto> CreateMockCells(int count, string slotId)
        {
            var list = new List<CellDto>();
            for (int i = 1; i <= count; i++)
            {
                list.Add(new CellDto { Barcode = $"CELL_{slotId}_{i:D3}", ChannelIndex = i, IsNg = false });
            }
            return list;
        }

        // =============================================================
        // 1. 实时遥测处理 (更新: 电压/电流/时间/真实工步名)
        // =============================================================
        private void OnTelemetryReceived(SlotTelemetryEvent e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var vm = GetOrAddViewModel(e.SlotId);

                // 电气参数
                vm.Voltage = e.Voltage;
                vm.Current = e.Current;
                vm.Temperature = e.Temperature;
                vm.Capacity = e.Capacity;

                // 【核心修复】工步名称以 Telemetry 为准 (这是真理)
                // 只要设备传来了工步名，就立刻刷新 UI，不管数据库里是啥
                if (!string.IsNullOrEmpty(e.CurrentStepName))
                {
                    vm.CurrentStep = e.CurrentStepName;
                }

                // 【核心修复】更新运行时间 DurationInfo
                // 格式化 TimeSpan 为 "HH:mm:ss"
                string timeStr = e.RunDuration.ToString(@"hh\:mm\:ss");
                vm.DurationInfo = timeStr;
                // 如果你想显示总时间，可以拼字符串: $"{timeStr} / 60min" (需从别处获取总时间)
            });
        }

        // =============================================================
        // 2. 工步状态处理 (更新: 状态/进度条/消息)
        // =============================================================
        private void OnStepStateReceived(SlotStepStateEvent e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var vm = GetOrAddViewModel(e.SlotId);

                // 更新消息
                vm.Message = e.Message;

                // 【核心修复】更新进度条 Progress
                // 只有当进度 > 0 或者 是明确的开始/结束事件时才更新
                // 这样避免了 "恢复" 事件(通常进度为0) 把界面清零
                if (e.ProgressPercent > 0 || e.EventType == StepEventType.Started || e.EventType == StepEventType.Completed)
                {
                    vm.Progress = e.ProgressPercent;
                }

                // 更新状态文字
                switch (e.EventType)
                {
                    case StepEventType.Started:
                        vm.Status = "执行中";
                        break;

                    case StepEventType.Paused:
                        vm.Status = "暂停";
                        break;

                    case StepEventType.Resumed:
                        vm.Status = "执行中";
                        // 恢复时，如果 Telemetry 还没来，暂时沿用数据库里的名，或者显示默认
                        // 但 OnTelemetryReceived 马上会覆盖它，所以这里不用太纠结
                        break;

                    case StepEventType.Faulted:
                        vm.Status = "异常";
                        break;

                    case StepEventType.Completed:
                        vm.Status = "已完成";
                        vm.Progress = 100;
                        break;
                }
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            _monitorService.OnTelemetryReceived -= OnTelemetryReceived;
            _monitorService.OnStepStateReceived -= OnStepStateReceived;
            base.OnClosed(e);
        }
    }
}