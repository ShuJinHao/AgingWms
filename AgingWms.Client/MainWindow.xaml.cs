using AgingWms.Client;
using AgingWms.UseCases.Services;
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
        private readonly IBus _bus;
        private readonly RealTimeMonitorService _monitorService;
        private readonly IServiceScopeFactory _scopeFactory;

        public ObservableCollection<SlotMonitorViewModel> MonitorList { get; set; }

        public MainWindow(
            IBus bus,
            RealTimeMonitorService monitorService,
            IServiceScopeFactory scopeFactory)
        {
            InitializeComponent();

            _bus = bus;
            _monitorService = monitorService;
            _scopeFactory = scopeFactory;

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
        private async void btnWrite_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetBatchSlotIds();
            Log($"[开始] 批量入库 {ids.Count} 个...");

            var client = _bus.CreateRequestClient<SaveSlotData>();

            foreach (var id in ids)
            {
                try
                {
                    if (!int.TryParse(txtCellCount.Text, out int cCount)) cCount = 24;
                    // 【注意】这里必须传入 id 防止主键冲突
                    var cells = CreateMockCells(cCount, id);
                    string trayCode = $"{txtTrayCode.Text}_{id}";

                    var response = await client.GetResponse<OperationResult>(new
                    {
                        SlotId = id,
                        SlotName = id,
                        TrayCode = trayCode,
                        Cells = cells,
                        DataJson = ""
                    });

                    if (response.Message.IsSuccess)
                    {
                        Log($" -> 写入 {id} 成功");
                        var vm = GetOrAddViewModel(id);
                        vm.TrayBarcode = trayCode;
                        vm.Status = "已入库";
                    }
                    else
                    {
                        Log($" -> 写入 {id} 失败: {response.Message.Message}");
                    }
                }
                catch (Exception ex) { Log($"异常: {ex.Message}"); }
            }
        }

        private async void btnMove_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string src = txtSlotId.Text.Trim();
                string tgt = txtTargetSlotId.Text.Trim();

                var client = _bus.CreateRequestClient<RelocateSlot>();
                var response = await client.GetResponse<OperationResult>(new { SlotId = src, TargetSlotId = tgt });

                Log(response.Message.IsSuccess ? $"[成功] {response.Message.Message}" : $"[失败] {response.Message.Message}");

                if (response.Message.IsSuccess)
                {
                    // 1. 找到源 VM
                    var srcVm = MonitorList.FirstOrDefault(x => x.SlotId == src);

                    // 【核心修复】先保存托盘码，再移除
                    string movingTray = srcVm?.TrayBarcode ?? "Unknown";

                    // 2. 移除源
                    if (srcVm != null) MonitorList.Remove(srcVm);

                    // 3. 更新目标
                    var tgtVm = GetOrAddViewModel(tgt);
                    tgtVm.Status = "占用";
                    tgtVm.TrayBarcode = movingTray; // 【修复】把托盘码赋值过去
                }
            }
            catch (Exception ex) { Log($"[异常] {ex.Message}"); }
        }

        private async void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetBatchSlotIds();
            var client = _bus.CreateRequestClient<ClearSlot>();

            foreach (var id in ids)
            {
                try
                {
                    var response = await client.GetResponse<OperationResult>(new { SlotId = id });
                    Log($"移除 {id}: {response.Message.Message}");

                    if (response.Message.IsSuccess)
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

        private void OnTelemetryReceived(SlotTelemetryEvent e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var vm = GetOrAddViewModel(e.SlotId);
                // 更新电压电流
                vm.Voltage = e.Voltage;
                vm.Current = e.Current;
                // 【补全】更新温度和容量
                vm.Temperature = e.Temperature;
                vm.Capacity = e.Capacity;
            });
        }

        private void OnStepStateReceived(SlotStepStateEvent e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var vm = GetOrAddViewModel(e.SlotId);

                // 更新基础信息
                vm.CurrentStep = e.StepName;
                vm.Message = e.Message;

                // 【核心修复 A】必须赋值进度条，否则界面一直是灰的
                vm.Progress = e.ProgressPercent;

                // 【核心修复 B】增加 Resumed (恢复) 和 Completed (完成) 的状态判断
                switch (e.EventType)
                {
                    case StepEventType.Started:
                        vm.Status = "执行中";
                        break;

                    case StepEventType.Paused:
                        vm.Status = "暂停";
                        break;

                    case StepEventType.Resumed: // 之前漏了这个，导致恢复后状态文字没变
                        vm.Status = "执行中";
                        break;

                    case StepEventType.Faulted:
                        vm.Status = "异常";
                        break;

                    case StepEventType.Completed:
                        vm.Status = "已完成";
                        vm.Progress = 100; // 强制进度满格
                        break;

                    default:
                        vm.Status = e.EventType.ToString();
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