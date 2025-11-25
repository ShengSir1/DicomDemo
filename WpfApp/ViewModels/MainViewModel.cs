using FellowOakDicom;
using FellowOakDicom.Imaging;
using HandyControl.Controls;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfApp.Utils;

namespace WpfApp.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        //事件
        public event PropertyChangedEventHandler? PropertyChanged;

        //通知
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        //属性
        public ObservableCollection<KeyValuePair<string, string>> FileBasic { get; } = [];
        public ObservableCollection<DicomTagEntry> DicomTags { get; } = [];
        private string _selectedFileName = string.Empty;
        /// <summary>
        /// 当前打开文件名称
        /// </summary>
        public string SelectedFileName
        {
            get => _selectedFileName;
            set { _selectedFileName = value; OnPropertyChanged(); }
        }

        private string _selectedFilePath = string.Empty;
        /// <summary>
        /// 当前打开文件绝对路径
        /// </summary>
        public string SelectedFilePath
        {
            get => _selectedFilePath;
            set { _selectedFilePath = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }
        private bool _isBusy = false;
        /// <summary>
        /// 是否显示加载组件
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }
        /// <summary>
        /// 修改提示控制标志
        /// </summary>
        private bool _suppressChangePrompt = false;
        
        private ImageSource? _imageSource;
        /// <summary>
        /// 图像源
        /// </summary>
        public ImageSource? ImageSource
        {
            get => _imageSource;
            set { _imageSource = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 持有当前图像实例
        /// </summary>
        private DicomImage? _currentDicomImage; 
        /// <summary>
        /// 简单的渲染锁，防止卡顿
        /// </summary>
        private bool _isRendering = false;

        private double _windowCenter;
        /// <summary>
        /// 当前窗位
        /// </summary>
        public double WindowCenter
        {
            get => _windowCenter;
            set { _windowCenter = value; OnPropertyChanged(); }
        }

        private double _windowWidth;
        /// <summary>
        /// 当前窗宽
        /// </summary>
        public double WindowWidth
        {
            get => _windowWidth;
            set { _windowWidth = value; OnPropertyChanged(); }
        }

        //命令
        public ICommand BrowseCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand AddTagCommand { get; }
        public ICommand RemoveTagCommand { get; }
        public ICommand LoadImageCommand { get; }


        public MainViewModel()
        {
            BrowseCommand = new RelayCommand(async _ => await BrowseFileAsync());
            ClearCommand = new RelayCommand(_ => ClearData(), _ => DicomTags.Count > 0 || FileBasic.Count > 0 || !string.IsNullOrEmpty(SelectedFileName) || !string.IsNullOrEmpty(SelectedFilePath));
            AddTagCommand = new RelayCommand(async _ => await AddTagAsync(), _ => !string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath));
            RemoveTagCommand = new RelayCommand(async p => await RemoveSelectedTagAsync(p as DicomTagEntry), p => p is DicomTagEntry && !string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath));
            LoadImageCommand = new RelayCommand(async _ => await LoadImageAsync(SelectedFilePath), _ => !string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath));
        }

        //方法
        /// <summary>
        /// 浏览打开DCM文件
        /// </summary>
        /// <remarks>
        /// 此方法在用户选择文件后加载DICOM数据和图像。
        /// </remarks>
        private async Task BrowseFileAsync()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "DICOM files|*.dcm|All files|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                await LoadDicomAsync(dlg.FileName);
                await LoadImageAsync(dlg.FileName);
            }
        }

        /// <summary>
        /// 清除所有加载的数据并将选择状态重置为默认值。
        /// </summary>
        /// <remarks>
        /// 此方法取消订阅事件处理程序、清除内部集合并重置相关属性。
        /// 应该调用它来释放资源并为新数据准备对象，或者在加载新文件之前，确保状态干净。
        /// </remarks>
        private void ClearData()
        {
            foreach (var e in DicomTags) e.PropertyChanged -= OnDicomEntryPropertyChanged;
            DicomTags.Clear();
            FileBasic.Clear();
            SelectedFileName = string.Empty;
            SelectedFilePath = string.Empty;
            ImageSource = null;
            CommandManager.InvalidateRequerySuggested();
        }

        #region 图像相关方法

        /// <summary>
        /// 加载图像
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private async Task LoadImageAsync(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            // 如果已经在忙，就不重复加载（或者根据需求取消上一次）
            // 这里简单处理，重置状态
            _currentDicomImage = null;

            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() => IsBusy = true);

                await Task.Run(async () =>
                {
                    // 1. 初始化 DicomImage 并保存实例
                    _currentDicomImage = new DicomImage(path);

                    // 2. 获取初始的窗宽窗位
                    _windowCenter = _currentDicomImage.WindowCenter;
                    _windowWidth = _currentDicomImage.WindowWidth;

                    // 更新 UI 上的数值
                    OnPropertyChanged(nameof(WindowCenter));
                    OnPropertyChanged(nameof(WindowWidth));

                    // 3. 初始渲染
                    await RenderCurrentDicomImageAsync();
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() => Growl.Error($"加载图像失败：{ex.Message}"));
            }
            finally
            {
                await Application.Current.Dispatcher.InvokeAsync(() => IsBusy = false);
            }
        }

        /// <summary>
        /// 调整窗宽窗位
        /// </summary>
        /// <param name="deltaX">鼠标水平移动距离 (影响窗宽)</param>
        /// <param name="deltaY">鼠标垂直移动距离 (影响窗位)</param>
        public void AdjustWindowLevel(double deltaX, double deltaY)
        {
            if (_currentDicomImage == null) return;

            // 调整灵敏度，可以根据需要修改
            const double sensitivity = 2.0;

            // 也就是：水平拖动改变窗宽(Contrast)，垂直拖动改变窗位(Brightness)
            // DICOM 习惯：
            // Window Width (Contrast): 越小对比度越高
            // Window Center (Brightness): 越小越亮

            _windowWidth += deltaX * sensitivity;
            _windowCenter += deltaY * sensitivity;

            // 窗宽不能小于 1
            if (_windowWidth < 1) _windowWidth = 1;

            // 更新到 DicomImage 对象
            _currentDicomImage.WindowWidth = _windowWidth;
            _currentDicomImage.WindowCenter = _windowCenter;

            // 通知 UI 数值变化
            OnPropertyChanged(nameof(WindowWidth));
            OnPropertyChanged(nameof(WindowCenter));

            // 触发重新渲染
            _ = RenderCurrentDicomImageAsync();
        }

        /// <summary>
        /// 核心渲染逻辑：将当前的 DicomImage 渲染为 WPF Bitmap
        /// </summary>
        private async Task RenderCurrentDicomImageAsync()
        {
            // 简单的并发控制：如果正在渲染，跳过本次请求（丢帧策略，保证流畅度）
            if (_isRendering || _currentDicomImage == null) return;

            _isRendering = true;
            try
            {
                // 在后台线程渲染
                await Task.Run(async () =>
                {
                    // 使用 ImageSharp 渲染
                    using var sharpImage = _currentDicomImage.RenderImage().AsSharpImage();
                    using var ms = new MemoryStream();
                    await sharpImage.SaveAsBmpAsync(ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        ImageSource = bitmap;
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"渲染出错: {ex.Message}");
            }
            finally
            {
                _isRendering = false;
            }
        }

        #endregion

        #region 标签相关方法
        /// <summary>
        /// 加载标签
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private async Task LoadDicomAsync(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            _ = Task.Run(async () =>
            {
                try
                {

                    await Application.Current.Dispatcher.InvokeAsync(() => ClearData());

                    await Application.Current.Dispatcher.InvokeAsync(() => IsBusy = true);

                    SelectedFileName = Path.GetFileName(path);
                    SelectedFilePath = path;

                    var fi = new FileInfo(path);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        FileBasic.Add(new KeyValuePair<string, string>("File Name", fi.Name));
                        FileBasic.Add(new KeyValuePair<string, string>("Path", fi.FullName));
                        FileBasic.Add(new KeyValuePair<string, string>("Size", fi.Length.ToString()));
                        FileBasic.Add(new KeyValuePair<string, string>("Last Modified", fi.LastWriteTime.ToString()));
                    });

                    var file = await DicomFile.OpenAsync(path);
                    var dataset = file.Dataset;
                    //var patientId = dataset.GetString(DicomTag.PatientID);
                    //var patientName = DicomHelp.ReadDicomStringWithLibraryEncoding(dataset, DicomTag.PatientName);
                    //var patientBirthDate = dataset.GetString(DicomTag.PatientBirthDate);
                    //var patientSex = DicomHelp.GetPatientSexDisplayText(dataset.GetString(DicomTag.PatientSex));
                    //var patientAge = dataset.GetString(DicomTag.PatientAge);
                    //var institutionName = DicomHelp.ReadDicomStringWithLibraryEncoding(dataset, DicomTag.InstitutionName);
                    //var institutionalDepartmentName = DicomHelp.ReadDicomStringWithLibraryEncoding(dataset, DicomTag.InstitutionalDepartmentName);
                    //var bodyPartExamined = DicomHelp.ReadDicomStringWithLibraryEncoding(dataset, DicomTag.BodyPartExamined);
                    //var modality = dataset.GetString(DicomTag.Modality);
                    //var instanceCreationDateTime = dataset.GetDateTime(DicomTag.InstanceCreationDate, DicomTag.InstanceCreationTime).ToString();

                    var entries = new List<DicomTagEntry>
                    {
                        //new(DicomTag.PatientID, "患者ID", patientId),
                        //new(DicomTag.PatientName, "患者Name", patientName),
                        //new(DicomTag.PatientBirthDate, "患者生日", patientBirthDate),
                        //new(DicomTag.PatientSex, "患者性别", patientSex),
                        //new(DicomTag.PatientAge, "患者年龄", patientAge),
                        //new(DicomTag.InstitutionName, "机构名称", institutionName),
                        //new(DicomTag.InstitutionalDepartmentName, "机构部门名称", institutionalDepartmentName),
                        //new(DicomTag.BodyPartExamined, "检查部位", bodyPartExamined),
                        //new(DicomTag.Modality, "设备类型", modality),
                        //new(DicomTag.InstanceCreationDate, "创建时间", instanceCreationDateTime)
                    };

                    // 加载数据集中尚未添加的任何其他标签
                    foreach (var item in dataset)
                    {
                        if (item == null) continue;
                        var t = item.Tag;
                        //if (t == DicomTag.PatientID || t == DicomTag.PatientName || t == DicomTag.PatientBirthDate || t == DicomTag.PatientSex || t == DicomTag.PatientAge || t == DicomTag.InstitutionName || t == DicomTag.InstitutionalDepartmentName || t == DicomTag.BodyPartExamined || t == DicomTag.Modality || t == DicomTag.InstanceCreationDate) continue;
                        try
                        {
                            var text = DicomHelp.ReadDicomStringWithLibraryEncoding(dataset, t);
                            entries.Add(new DicomTagEntry(t, t.DictionaryEntry?.Keyword ?? t.ToString(), text));
                        }
                        catch { }
                    }

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        foreach (var e in entries) AddDicomEntry(e);
                    });
                }
                catch (Exception ex)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => Growl.Error($"加载DICOM失败：{ex.Message}"));
                }
                finally
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => IsBusy = false);
                }
            });
        }

        private async Task AddTagAsync()
        {
            #region 动态构建一个小的模态对话框来收集标签、VR和值
            var owner = Application.Current?.MainWindow;
            System.Windows.Window win = new()
            {
                Title = "添加 DICOM 标签",
                Owner = owner,
                Width = 520,
                Height = 260,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Content = null
            };

            var grid = new Grid { Margin = new Thickness(12) };
            // rows: tag, spacer, vr, spacer, value, spacer, buttons
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 标签行
            var tagPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(tagPanel, 0);
            tagPanel.Children.Add(new TextBlock { Text = "标签：", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            var tagBox = new System.Windows.Controls.TextBox { Width = 390, ToolTip = "支持格式：0010,0010 或 00100010 或 PatientName" };
            tagPanel.Children.Add(tagBox);
            grid.Children.Add(tagPanel);

            // VR行
            var vrPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(vrPanel, 2);
            vrPanel.Children.Add(new TextBlock { Text = "VR：", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            var vrCombo = new System.Windows.Controls.ComboBox { Width = 200, IsEditable = false };
            
            vrCombo.Items.Add("自动 (从字典)");
            // 常见VR
            var vrList = new[] { DicomVR.AE, DicomVR.AS, DicomVR.AT, DicomVR.CS, DicomVR.DA, DicomVR.DS, DicomVR.DT, DicomVR.FL, DicomVR.FD, DicomVR.IS, DicomVR.LO, DicomVR.LT, DicomVR.OB, DicomVR.OD, DicomVR.OF, DicomVR.OW, DicomVR.PN, DicomVR.SH, DicomVR.SL, DicomVR.SQ, DicomVR.SS, DicomVR.ST, DicomVR.TM, DicomVR.UI, DicomVR.UL, DicomVR.UN, DicomVR.US, DicomVR.UT };
            foreach (var v in vrList) vrCombo.Items.Add(v);
            vrCombo.SelectedIndex = 0;
            vrPanel.Children.Add(vrCombo);
            var vrHint = new TextBlock { Text = "", Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            vrPanel.Children.Add(vrHint);
            grid.Children.Add(vrPanel);

            // 值行
            var valuePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(valuePanel, 4);
            valuePanel.Children.Add(new TextBlock { Text = "值：", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            var valueBox = new System.Windows.Controls.TextBox { Width = 390 };
            valuePanel.Children.Add(valueBox);
            grid.Children.Add(valuePanel);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(buttonPanel, 6);
            var okBtn = new Button { Content = "确定", Width = 80, Margin = new Thickness(6, 0, 0, 0) };
            var cancelBtn = new Button { Content = "取消", Width = 80, Margin = new Thickness(6, 0, 0, 0) };
            buttonPanel.Children.Add(okBtn);
            buttonPanel.Children.Add(cancelBtn);
            grid.Children.Add(buttonPanel);

            okBtn.Click += (s, e) => { win.DialogResult = true; win.Close(); };
            cancelBtn.Click += (s, e) => { win.DialogResult = false; win.Close(); };

            // 当标签文本框失去焦点时，尝试解析标签并建议 VR
            tagBox.LostFocus += (s, e) =>
            {
                var text = tagBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(text)) return;
                var parsed = DicomHelp.ParseDicomTagFromInput(text);
                if (parsed != null)
                {
                    var suggested = parsed.DictionaryEntry?.ValueRepresentations?.FirstOrDefault() ?? DicomVR.UN;
                    vrHint.Text = $"建议 VR: {suggested}";
                }
                else
                {
                    vrHint.Text = "无法识别标签，使用自动或手动选择VR";
                }
            };

            // 当 VR 选择改变时，启用/禁用值输入框（针对非文本 VR）
            vrCombo.SelectionChanged += (s, e) =>
            {
                var sel = vrCombo.SelectedItem;
                if (sel is string) { valueBox.IsEnabled = true; }
                else if (sel is DicomVR dv)
                {
                    // 对于二进制/序列 VR，禁用文本输入
                    if (dv == DicomVR.OB || dv == DicomVR.OW || dv == DicomVR.OF || dv == DicomVR.SQ || dv == DicomVR.UT || dv == DicomVR.UN)
                    {
                        valueBox.IsEnabled = false;
                        valueBox.Text = string.Empty;
                        vrHint.Text = "所选 VR 不适合文本输入；请使用高级编辑或导入二进制数据。";
                    }
                    else
                    {
                        valueBox.IsEnabled = true;
                    }
                }
            };

            win.Content = grid;
            #endregion

            #region 校验数据
            if (win.ShowDialog() != true) return;

            var tagInput = tagBox.Text?.Trim();
            var valueInput = valueBox.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(tagInput))
            {
                Growl.Warning("标签为空，已取消。");
                return;
            }

            DicomTag? parsedTag = null;
            try { parsedTag = DicomHelp.ParseDicomTagFromInput(tagInput); } catch { parsedTag = null; }
            if (parsedTag == null)
            {
                Growl.Warning("无法识别输入的标签，请使用形如 0010,0010 或标准关键词。");
                return;
            }

            if (string.IsNullOrEmpty(SelectedFilePath) || !File.Exists(SelectedFilePath))
            {
                Growl.Error("未找到打开的DICOM文件，无法保存。请先打开文件后再添加标签。");
                return;
            }

            // 确定最终的 VR：如果选择了自动 -> 使用字典建议（如果有的话）
            DicomVR finalVr;
            var selItem = vrCombo.SelectedItem;
            if (selItem is string) finalVr = parsedTag.DictionaryEntry?.ValueRepresentations?.FirstOrDefault() ?? DicomVR.UN;
            else if (selItem is DicomVR dv2) finalVr = dv2;
            else finalVr = DicomVR.UN;

            // 对于非文本 VR，不允许空值或文本输入
            if (finalVr == DicomVR.OB || finalVr == DicomVR.OW || finalVr == DicomVR.OF || finalVr == DicomVR.SQ || finalVr == DicomVR.UT || finalVr == DicomVR.UN)
            {
                Growl.Warning($"所选 VR ({finalVr}) 不支持简单文本输入。请使用高级编辑或选择其他 VR。");
                return;
            }

            object[] valuesToSave;
            try
            {
                valuesToSave = DicomHelp.ConvertStringToValuesForVr(valueInput, finalVr);
                if (valuesToSave == null || valuesToSave.Length == 0)
                {
                    Growl.Warning("无法从输入中生成值，请检查格式。已取消。 ");
                    return;
                }
            }
            catch (Exception ex)
            {
                Growl.Warning($"输入校验失败：{ex.Message}");
                return;
            }
            #endregion

            _ = Task.Run(async () =>
            {
                try
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => IsBusy = true);

                    await Task.Run(() =>
                    {
                        var dicomFile = DicomFile.Open(SelectedFilePath);
                        var stringValues = valuesToSave.Select(v => v?.ToString() ?? string.Empty).ToArray();
                        dicomFile.Dataset.AddOrUpdate<string>(finalVr, parsedTag, stringValues);
                        dicomFile.Save(SelectedFilePath);
                    });

                    await Application.Current.Dispatcher.InvokeAsync(() => Growl.Success($"已添加标签 {parsedTag} (VR={finalVr}) 并保存到文件。"));
                    await LoadDicomAsync(SelectedFilePath);
                }
                catch (Exception ex)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => Growl.Error($"添加标签失败：{ex.Message}"));
                }
                finally
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => IsBusy = false);
                }
            });
        }

        /// <summary>
        /// 用户确认后，从当前选定的文件中删除指定的DICOM标签。
        /// </summary>
        /// <remarks>
        /// 此方法在删除标记之前提示用户进行确认。移除是永久且无法撤销。成功删除后，文件将被保存并重新加载，以反映变化。
        ///</remarks>
        /// <param name="entry">要从文件中删除的DICOM标签条目。如果为null，则该方法不执行任何操作。</param>
        private async Task RemoveSelectedTagAsync(DicomTagEntry? entry)
        {
            if (entry == null) return;
            var confirm = HandyControl.Controls.MessageBox.Show($"确认从文件中删除标签 '{entry.Label}' ({entry.Tag}) 吗？删除操作不可回退！！", "删除标签", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => IsBusy = true);
                    await Task.Run(() =>
                    {
                        var dicomFile = DicomFile.Open(SelectedFilePath);
                        dicomFile.Dataset.Remove(entry.Tag);
                        dicomFile.Save(SelectedFilePath);
                    });

                    await Application.Current.Dispatcher.InvokeAsync(() => Growl.Success($"已删除标签 {entry.Tag} 并保存到文件。"));
                    await LoadDicomAsync(SelectedFilePath);
                }
                catch (Exception ex)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => Growl.Error($"删除标签失败：{ex.Message}"));
                }
                finally
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => IsBusy = false);
                }
            });
        }

        private void AddDicomEntry(DicomTagEntry entry)
        {
            entry.OriginalValue = entry.Value;
            entry.PropertyChanged += OnDicomEntryPropertyChanged;
            DicomTags.Add(entry);
        }

        /// <summary>
        /// 处理Dicom标记条目的属性更改事件，提示用户确认并将更改保存到DICOM标签值。
        /// </summary>
        /// <remarks>
        /// 此方法旨在用作上属性更改的事件处理程序Dicom标签条目对象。
        /// 当Value属性更改时，系统会提示用户确认是否保存为关联的DICOM文件添加新值。
        /// 如果用户拒绝或保存失败，则该值将恢复。这如果值不变或更改提示被抑制，则该方法会忽略更改。
        /// </remarks>
        /// <param name="sender">事件的来源，预计是属性已更改的Dicom标记条目。</param>
        /// <param name="e">包含事件数据的对象，包括更改的属性的名称。</param>
        private void OnDicomEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressChangePrompt) return;
            if (e.PropertyName != nameof(DicomTagEntry.Value)) return;
            if (sender is not DicomTagEntry entry) return;

            // if value unchanged, ignore
            if (entry.Value == entry.OriginalValue) return;

            // prompt user
            var result = HandyControl.Controls.MessageBox.Show($"是否将标签 '{entry.Label}' 的值从 '{entry.OriginalValue}' 修改为 '{entry.Value}' 并保存到文件？", "确认修改", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                RevertEntryValue(entry);
                return;
            }

            if (string.IsNullOrEmpty(SelectedFilePath) || !File.Exists(SelectedFilePath))
            {
                Growl.Error("未找到打开的DICOM文件，无法保存。");
                RevertEntryValue(entry);
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => IsBusy = true);

                    await Task.Run(() =>
                    {
                        var dicomFile = DicomFile.Open(SelectedFilePath);
                        var ds = dicomFile.Dataset;
                        DicomHelp.WriteDicomStringWithChineseHandling(ds, entry.Tag, entry.Value);
                        dicomFile.Save(SelectedFilePath);
                    });

                    await Application.Current.Dispatcher.InvokeAsync(() => Growl.Success($"成功将标签 '{entry.Label}' 的值从 '{entry.OriginalValue}' 修改为 '{entry.Value}' 并保存到文件"));
                    await Application.Current.Dispatcher.InvokeAsync(() => entry.OriginalValue = entry.Value);
                    await LoadDicomAsync(SelectedFilePath);
                }
                catch (Exception ex)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => Growl.Error($"保存失败：{ex.Message}"));
                    await Application.Current.Dispatcher.InvokeAsync(() => RevertEntryValue(entry));
                }
                finally
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => IsBusy = false);
                }
            });
        }

        /// <summary>
        /// 将指定DICOM标签条目的值恢复到其原始值。
        /// </summary>
        /// <param name="entry">DICOM标签条目，其值将恢复到原始状态。不能为null。</param>
        private void RevertEntryValue(DicomTagEntry entry)
        {
            try
            {
                _suppressChangePrompt = true;
                entry.Value = entry.OriginalValue;
            }
            finally
            {
                _suppressChangePrompt = false;
            }
        }

        #endregion
    }
}
