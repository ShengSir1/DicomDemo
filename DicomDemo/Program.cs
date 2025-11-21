const string DICOMFOLDER = "0b5e1a5b-a398-45ec-bb86-d01c37713e39";
const string DICOMFILENAME = "1.2.840.113619.2.479.3.2831201656.249.1763329612.429.1.dcm";
var path = Path.Combine(AppContext.BaseDirectory, DICOMFOLDER, DICOMFILENAME);
if (!File.Exists(path))
{
    Console.WriteLine($"DICOM 文件不存在: {path}");
    return;
}

// 如果需要用到 GBK/GB18030 等 CodePage 编码，先注册 CodePages 提供者
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var file = await DicomFile.OpenAsync(path);

// 打印 Specific Character Set 以便分析
if (file.Dataset.TryGetValues<string>(DicomTag.SpecificCharacterSet, out var specific))
{
    Console.WriteLine($"SpecificCharacterSet: {string.Join("\\\\", specific)}");
}
else
{
    Console.WriteLine("SpecificCharacterSet: <未设置>");
}

var patientId = file.Dataset.GetString(DicomTag.PatientID);
var patientName = DicomHelp.ReadDicomStringWithLibraryEncoding(file.Dataset, DicomTag.PatientName);

Console.WriteLine($"Patient ID: {patientId}");
Console.WriteLine($"Patient Name: {patientName}");