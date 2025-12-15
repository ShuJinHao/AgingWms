using System;
using System.ComponentModel;
using System.Windows.Media;

namespace AgingWms.Client
{
    public class SlotMonitorViewModel : INotifyPropertyChanged
    {
        // --- 基础信息 ---
        public string SlotId { get; set; }

        public string TrayBarcode { get; set; }

        // --- 实时状态 ---
        private string _status;

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        // --- 工步信息 ---
        private string _currentStep;

        public string CurrentStep // 如 "恒流充 (Step 2)"
        {
            get => _currentStep;
            set { _currentStep = value; OnPropertyChanged(nameof(CurrentStep)); }
        }

        private double _progress;

        public double Progress // 进度条 0-100
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(nameof(Progress)); }
        }

        private string _durationInfo;

        public string DurationInfo // 如 "00:05:12 / 60min"
        {
            get => _durationInfo;
            set { _durationInfo = value; OnPropertyChanged(nameof(DurationInfo)); }
        }

        // --- 实时电气数据 (变色提示) ---
        private double _voltage;

        public double Voltage
        {
            get => _voltage;
            set { _voltage = value; OnPropertyChanged(nameof(Voltage)); }
        }

        private double _current;

        public double Current
        {
            get => _current;
            set { _current = value; OnPropertyChanged(nameof(Current)); }
        }

        private double _temperature;

        public double Temperature
        {
            get => _temperature;
            set
            {
                _temperature = value;
                OnPropertyChanged(nameof(Temperature));
                OnPropertyChanged(nameof(TempColor)); // 温度变了，颜色也要变
            }
        }

        // 温度过高变红
        public Brush TempColor => Temperature > 40 ? Brushes.Red : Brushes.Black;

        private double _capacity;

        public double Capacity
        {
            get => _capacity;
            set { _capacity = value; OnPropertyChanged(nameof(Capacity)); }
        }

        private string _message;

        public string Message // 附加消息 (如 "温度正常")
        {
            get => _message;
            set { _message = value; OnPropertyChanged(nameof(Message)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}