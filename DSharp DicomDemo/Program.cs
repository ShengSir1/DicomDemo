using FellowOakDicom;
const string DICOMFOLDER = "0b5e1a5b-a398-45ec-bb86-d01c37713e39/";
var file = await DicomFile.OpenAsync($"{DICOMFOLDER}1.2.840.113619.2.479.3.2831201656.249.1763329612.429.1.dcm");
var patientId = file.Dataset.GetString(DicomTag.PatientID);
Console.WriteLine($"Patient ID: {patientId}");