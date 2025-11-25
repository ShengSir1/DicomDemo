# DICOM文件编辑器

一个基于.NET 10、WPF MVVM、HandyControl库和fo-dicom和fo-dicom.Imaging.ImageSharp库开发的DICOM文件编辑器，支持对DICOM标签进行完整的增删改查操作，支持展示、动态调整DICOM图像窗宽窗位

## 功能特点

### 核心功能

- 完整的DICOM标签管理：支持添加、删除、修改和查询DICOM文件中的所有标签

- DICOM图像处理：支持展示、动态调整窗宽窗位

- 多框架支持：基于fo-dicom库，完全兼容DICOM 3.0标准

- 直观的UI界面：使用WPF+HandyControl开源UI库开发，提供友好的图形化操作界面

- 序列数据支持：完整处理DICOM序列数据，包括多帧图像

### 标签操作

- 标签读取：支持通过DicomTag类直接访问标准标签

- 标签修改：使用Dataset.AddOrUpdate()方法添加或修改标签值

- 标签删除：提供完整的标签移除功能

- 批量操作：支持对多个标签进行批量处理

### 图像处理

- 图像渲染：使用fo-dicom扩展库 ImageSharp 高质量的DICOM图像渲染

- 窗宽窗位调整：支持动态调整窗宽(WindowWidth)和窗位(WindowCenter)

- 多帧支持：完整支持多帧DICOM图像的显示和操作（待开发...）

- 图像导出：支持将DICOM图像导出为常见格式(如BMP、PNG、JPEG)（待开发...）

## 系统要求
### 开发环境

- .NET 10.0 或更高版本

- Windows 10/11 操作系统
### 运行环境
- Windows 7/10/11 操作系统

- 磁盘空间：至少137MB可用空间

- RAM：至少160MB可用空间

## 安装步骤

1. 获取最新版本
   
   - 克隆源代码自行编译

   - 或从Release页面下载最新版本的安装包

2. 依赖安装
   ### 通过NuGet安装fo-dicom库
   Install-Package fo-dicom
   ### 通过NuGet安装HandyControl库
   Install-Package HandyControl
   ### 通过NuGet安装fo-dicom扩展库
   Install-Package fo-dicom.Imaging.ImageSharp
4. 运行应用
   
   - 通过编译器/命令行启动

   - 或双击解压后的可执行文件

## 使用指南

### 基本操作

1. 打开DICOM文件
   - 点击"选择DCM文件" → "打开"，选择DICOM文件

   - 支持拖放方式直接打开文件（暂不支持...）

2. 查看标签信息
   - 下方面板显示完整的DICOM标签树状结构

3. 编辑标签值
   - 双击需要修改的标签值

   - 输入新值并确认保存更改

### 图像操作技巧

// 图像渲染示例代码
new DicomSetupBuilder().RegisterServices(s => s.AddFellowOakDicom().AddImageManager<ImageSharpImageManager>()).Build();
DicomFile dicomFile = DicomFile.Open(@"path/to/file.dcm");
DicomImage dicomImage = new DicomImage(dicomFile.Dataset);
var sharpImage = dicomImage.RenderImage().AsSharpImage();


## 高级功能（暂不支持...）

- 序列查看：对于多帧数据，使用帧控制条浏览不同帧

- 标签搜索：通过搜索框快速定位特定标签

- 批量保存：支持对多个文件进行批量标签操作

## 开发信息

### 技术架构

- 前端：WPF (XAML)

- DICOM处理：fo-dicom 5.0+

- 图像渲染：WPF成像管道

### 项目结构

```
DicomDemo/            # 控制台项目可忽略
└── Utils/            # 工具类
WpfApp/
├── wwwroot/          # 静态icon资源
├── Utils/            # 工具类
├── ViewModels/       # 视图模型
└── Views/            # 用户界面
```

## 常见问题

Q: 无法打开某些DICOM文件？

A: 请确保文件符合DICOM标准，尝试使用DicomFile.Open方法的强制读取模式

Q: 如何保存修改后的文件？

A: 使用DicomFile.Save()或SaveAsync()方法保存更改

## 贡献指南

我们欢迎社区贡献！请阅读以下指南：
1. Fork本项目
2. 创建功能分支
3. 提交更改
4. 推送到分支
5. 创建Pull Request

## 许可证

本项目采用MIT许可证。详见LICENSE文件。

## 技术支持

- 提交Issue：GitHub Issues页面

- 文档更新：欢迎提交Pull Request完善文档

- 联系方式：通过项目主页获取最新支持信息

## 更新日志

v1.0.0 (2025-11-21)

- 初始版本发布

- 支持基本的DICOM标签操作

注意：本工具仅用于学习和研究目的，医疗数据操作请遵循相关法规和标准。
