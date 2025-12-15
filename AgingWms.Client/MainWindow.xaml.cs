using AgingWms.Client; // 确保引用了 ViewModel 所在的命名空间
using AgingWms.UseCases.Services;
using AgingWms.Workflow.Services;
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
        // 注入的三个核心服务
        private readonly SlotCommandService _slotService;

        private readonly AgingJobService _jobService;
        private readonly RealTimeMonitorService _monitorService;

        // UI 绑定的数据源 (DataGrid 会自动监听这个集合的变化)
        public ObservableCollection<SlotMonitorViewModel> MonitorList { get; set; }

        // 声明在类顶部
        private readonly SharedKernel.Repositoy.IReadRepository<AgingWms.Core.Domain.WarehouseSlot> _repository;

        // 构造函数：通过依赖注入获取服务
        public MainWindow(
            SlotCommandService slotService,
            AgingJobService jobService,
            RealTimeMonitorService monitorService,
            SharedKernel.Repositoy.IReadRepository<AgingWms.Core.Domain.WarehouseSlot> repository)
        {
            InitializeComponent();

            _slotService = slotService;
            _jobService = jobService;
            _monitorService = monitorService;

            // 初始化列表
            MonitorList = new ObservableCollection<SlotMonitorViewModel>();
            dgMonitor.ItemsSource = MonitorList;

            // =========================================================
            // 【核心】订阅事件 (Event-Driven)
            // 当后台 Activity 有动作时，这里会被触发
            // =========================================================
            _monitorService.OnTelemetryReceived += OnTelemetryReceived;
            _monitorService.OnStepStateReceived += OnStepStateReceived;

            // ... 其他赋值 ...
            _repository = repository; // <--- 赋值

            // ... 其他初始化 ...
            this.Loaded += MainWindow_Loaded; // <--- 绑定加载事件
        }

        // 窗口加载完毕时触发
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadActiveSlotsAsync();
        }

        // 核心加载逻辑
        // 核心加载逻辑
        private async Task LoadActiveSlotsAsync()
        {
            try
            {
                // =========================================================
                // 【修正】使用接口定义的 GetListAsync()
                // =========================================================
                // 因为你的接口只接受 ISpecification，不支持直接传 lambda。
                // 所以这里先获取所有库位，再在内存里过滤 (几百个数据量级没有任何性能问题)
                var allSlots = await _repository.GetListAsync();

                // 内存筛选：状态不是 "Empty" (空闲) 的库位
                var activeSlots = allSlots.Where(x => x.Status != AgingWms.Core.Domain.SlotStatus.Empty);

                if (activeSlots == null || !activeSlots.Any()) return;

                // 2. 遍历并添加到界面列表
                foreach (var slot in activeSlots)
                {
                    // 防止重复添加
                    if (MonitorList.Any(x => x.SlotId == slot.SlotId)) continue;

                    var vm = new SlotMonitorViewModel
                    {
                        SlotId = slot.SlotId,
                        TrayBarcode = slot.TrayBarcode ?? "Unknown",

                        // 状态翻译
                        Status = slot.Status switch
                        {
                            AgingWms.Core.Domain.SlotStatus.Occupied => "占用",
                            AgingWms.Core.Domain.SlotStatus.Running => "运行中",
                            AgingWms.Core.Domain.SlotStatus.Paused => "暂停",
                            AgingWms.Core.Domain.SlotStatus.Error => "异常",
                            _ => "未知"
                        },

                        // 恢复显示一些默认值
                        Voltage = 0,
                        Current = 0,
                        Temperature = 25,
                        Capacity = 0,
                        // 如果是运行中，给个提示
                        CurrentStep = slot.Status == AgingWms.Core.Domain.SlotStatus.Running ? "等待恢复..." : "待机"
                    };

                    MonitorList.Add(vm);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载数据失败: {ex.Message}");
                // 如果想在日志框显示，也可以调用 Log($"加载失败: {ex.Message}");
            }
        }

        // =============================================================
        // 1. 事件处理 (UI 刷新逻辑)
        // =============================================================

        // 处理高频遥测数据 (1秒/次：电压、电流、容量、温度)
        private void OnTelemetryReceived(SlotTelemetryEvent e)
        {
            // 必须切换到 UI 线程更新界面
            Application.Current.Dispatcher.Invoke(() =>
            {
                var vm = GetOrAddViewModel(e.SlotId);

                // 更新实时数值
                vm.Voltage = e.Voltage;
                vm.Current = e.Current;
                vm.Temperature = e.Temperature;
                vm.Capacity = e.Capacity;

                // 更新时间显示
                vm.DurationInfo = $"{e.RunDuration:hh\\:mm\\:ss}";

                // 如果 Activity 传来了工步名，同步更新
                if (!string.IsNullOrEmpty(e.CurrentStepName))
                {
                    vm.CurrentStep = e.CurrentStepName;
                }
            });
        }

        // 处理状态变更 (工步开始、结束、暂停、报错)
        private void OnStepStateReceived(SlotStepStateEvent e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var vm = GetOrAddViewModel(e.SlotId);

                vm.CurrentStep = e.StepName;
                vm.Message = e.Message;
                vm.Progress = e.ProgressPercent; // 更新进度条

                // 根据事件类型更新“当前状态”文字
                switch (e.EventType)
                {
                    case StepEventType.Started:
                        vm.Status = "执行中";
                        break;

                    case StepEventType.Completed:
                        vm.Status = "已完成";
                        vm.Progress = 100;
                        break;

                    case StepEventType.Faulted:
                        vm.Status = "异常";
                        break;

                    case StepEventType.Paused:
                        vm.Status = "暂停";
                        break;

                    case StepEventType.Resumed:
                        vm.Status = "执行中";
                        break;
                }
            });
        }

        // 辅助：获取或创建 ViewModel 行
        private SlotMonitorViewModel GetOrAddViewModel(string slotId)
        {
            var vm = MonitorList.FirstOrDefault(x => x.SlotId == slotId);
            if (vm == null)
            {
                vm = new SlotMonitorViewModel
                {
                    SlotId = slotId,
                    Status = "就绪",
                    TrayBarcode = "Loading...",
                    // 初始化一些默认值
                    Voltage = 0,
                    Current = 0,
                    Temperature = 25
                };
                // 保持列表有序 (可选)
                InsertInOrder(vm);
            }
            return vm;
        }

        private void InsertInOrder(SlotMonitorViewModel vm)
        {
            // 简单插入，如果需要严格排序可以写个比较逻辑
            MonitorList.Add(vm);
        }

        // 窗口关闭时取消订阅，防止内存泄漏
        protected override void OnClosed(EventArgs e)
        {
            _monitorService.OnTelemetryReceived -= OnTelemetryReceived;
            _monitorService.OnStepStateReceived -= OnStepStateReceived;
            base.OnClosed(e);
        }

        // =============================================================
        // 2. 按钮逻辑 (WMS 基础操作)
        // =============================================================

        // 批量入库
        private async void btnWrite_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetBatchSlotIds();
            Log($"[开始] 批量入库 {ids.Count} 个...");

            foreach (var id in ids)
            {
                try
                {
                    var cells = CreateMockCells(int.Parse(txtCellCount.Text));
                    string trayCode = $"{txtTrayCode.Text}_{id}"; // 生成唯一托盘码

                    var result = await _slotService.WriteSlotAsync(id, trayCode, cells);
                    if (result.IsSuccess)
                    {
                        Log($" -> 写入 {id} 成功");
                        // 手动添加一行到监控列表，方便用户立即看到
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

        // 库位迁移 (仅演示单库位)
        private async void btnMove_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string src = txtSlotId.Text.Trim();
                string tgt = txtTargetSlotId.Text.Trim();

                var result = await _slotService.MoveSlotAsync(src, tgt);
                Log(result.IsSuccess ? $"[成功] {result.Message}" : $"[失败] {result.Message}");

                if (result.IsSuccess)
                {
                    // 刷新列表：移除源，添加目标
                    var srcVm = MonitorList.FirstOrDefault(x => x.SlotId == src);
                    if (srcVm != null) MonitorList.Remove(srcVm);

                    var tgtVm = GetOrAddViewModel(tgt);
                    tgtVm.Status = "占用";
                }
            }
            catch (Exception ex) { Log($"[异常] {ex.Message}"); }
        }

        // 批量移除
        private async void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetBatchSlotIds();
            foreach (var id in ids)
            {
                try
                {
                    var result = await _slotService.RemoveSlotAsync(id);
                    Log($"移除 {id}: {result.Message}");

                    // 从界面移除
                    var vm = MonitorList.FirstOrDefault(x => x.SlotId == id);
                    if (vm != null) MonitorList.Remove(vm);
                }
                catch (Exception ex) { Log($"异常: {ex.Message}"); }
            }
        }

        // =============================================================
        // 3. 按钮逻辑 (工作流控制)
        // =============================================================

        // 启动流程 (Start)
        private async void btnStartWorkflow_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetBatchSlotIds();
            Log($"[指令] 启动 {ids.Count} 个库位的老化流程...");

            // 定义工步配方 (Recipe)
            var steps = new List<JobStepConfigDto>
            {
                // 1. 静置 1 分钟 (测试用)
                new JobStepConfigDto { StepType = "Rest", Parameters = JObject.FromObject(new { DurationMinutes = 1 }) },
                // 2. 恒流充 (10A, 截止4.2V)
                new JobStepConfigDto { StepType = "CC_Charge", Parameters = JObject.FromObject(new { TargetCurrent = 10.0, CutoffVoltage = 4.2, MaxDurationMinutes = 60 }) },
                // 3. 静置 0.5 分钟
                new JobStepConfigDto { StepType = "Rest", Parameters = JObject.FromObject(new { DurationMinutes = 0.5 }) },
                // 4. 放电 (5A, 截止3.0V)
                new JobStepConfigDto { StepType = "Discharge", Parameters = JObject.FromObject(new { TargetCurrent = 5.0, CutoffVoltage = 3.0, MaxDurationMinutes = 60 }) }
            };

            foreach (var id in ids)
            {
                try
                {
                    var dto = new AgingJobDto
                    {
                        SlotId = id,
                        BatchId = $"BATCH_{DateTime.Now:MMdd}_{id}",
                        Steps = steps
                    };

                    await _jobService.StartJobAsync(dto);
                    Log($" -> 启动指令已发送: {id}");
                }
                catch (Exception ex) { Log($"启动失败 {id}: {ex.Message}"); }
            }
        }

        // 暂停 (Pause)
        private async void btnPause_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetBatchSlotIds();
            foreach (var id in ids)
            {
                await _jobService.PauseJobAsync(id);
                Log($"[暂停] 指令 -> {id}");
            }
        }

        // 恢复 (Resume)
        private async void btnResume_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetBatchSlotIds();
            foreach (var id in ids)
            {
                await _jobService.ResumeJobAsync(id);
                Log($"[恢复] 指令 -> {id}");
            }
        }

        // 强制停止 (Stop)
        private async void btnStop_Click(object sender, RoutedEventArgs e)
        {
            var ids = GetBatchSlotIds();
            foreach (var id in ids)
            {
                await _jobService.StopJobAsync(id);
                Log($"[停止] 指令 -> {id}");
            }
        }

        // =============================================================
        // 4. 辅助方法
        // =============================================================

        // 解析批量 ID (如 1-1-1 到 1-1-5)
        private List<string> GetBatchSlotIds()
        {
            var list = new List<string>();
            string startId = txtSlotId.Text.Trim();

            if (!int.TryParse(txtBatchCount.Text, out int count)) count = 1;

            var parts = startId.Split('-');
            // 尝试解析最后一位数字进行递增
            if (parts.Length >= 3 && int.TryParse(parts.Last(), out int index))
            {
                string prefix = string.Join("-", parts.Take(parts.Length - 1));
                for (int i = 0; i < count; i++)
                {
                    list.Add($"{prefix}-{index + i}");
                }
            }
            else
            {
                list.Add(startId);
            }
            return list;
        }

        private List<CellDto> CreateMockCells(int count)
        {
            var list = new List<CellDto>();
            for (int i = 1; i <= count; i++)
            {
                list.Add(new CellDto
                {
                    Barcode = $"CELL_{DateTime.Now:mm}_{i:D3}",
                    ChannelIndex = i,
                    IsNg = false
                });
            }
            return list;
        }

        private void Log(string message)
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            txtLog.ScrollToEnd();
        }
    }
}