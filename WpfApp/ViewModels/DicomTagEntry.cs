using FellowOakDicom;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfApp.ViewModels
{
    public class DicomTagEntry : INotifyPropertyChanged
    {
        public DicomTag Tag { get; }
        public string Label { get; }

        private string _value = string.Empty;
        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 存储原始文件以进行还原/提示
        /// </summary>
        public string OriginalValue { get; set; } = string.Empty;

        public DicomTagEntry(DicomTag tag, string label, string value)
        {
            Tag = tag;
            Label = label;
            _value = value ?? string.Empty;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
