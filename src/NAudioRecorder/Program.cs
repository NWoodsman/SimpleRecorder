// See https://aka.ms/new-console-template for more information
using NAudio.CoreAudioApi;
using NAudio.Wave;


MMDeviceEnumerator enumerator = new();

var es = enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active);

for (int i = 0; i < es.Count; i++)
{
	var e = es[i];
	Console.WriteLine($"{i} , {e.FriendlyName}");
}

var device = InputToDevice();



Console.WriteLine();
Console.WriteLine($"Selected {device.FriendlyName}");
//Record(device)

MMDevice InputToDevice()
{
	var rec_index = GetRecDevice();
	var device = es[rec_index];

	var ok = validateRecDevice(rec_index, device);

	if (!ok) return InputToDevice();
	else return device;
}

void userExit(string msg)
{
	Console.WriteLine($"UUser aborted, reason: {msg}. Press any key to quit.");
	Console.ReadKey();
	Environment.Exit(0);
}
bool validateRecDevice(int i, MMDevice device)
{
	Console.WriteLine();
	Console.WriteLine($"Selected {i} , {device.FriendlyName}; Is this correct? Y/N");

	var result = Console.ReadKey();

	if (result.Key == ConsoleKey.Y) return true;
	else if (result.Key == ConsoleKey.N) return false;
	else if (result.Key == ConsoleKey.Escape) userExit("User quit the device selection");

	return validateRecDevice(i, device);
}

int GetRecDevice()
{
	Console.WriteLine();
	Console.WriteLine("Enter the number of the recording device to listen to:");
	Console.WriteLine();
	var rawinput = Console.ReadLine();

	if (rawinput == "quit" || rawinput == "q")
	{
		userExit("User quit device selection.");
	}
	if (!int.TryParse(rawinput, out var int_result))
	{
		Console.WriteLine("Error, input an integer corresponding with a device. Try again.");
		return GetRecDevice();
	}

	if(int_result > es.Count - 1)
	{
		Console.WriteLine();
		Console.WriteLine($"Error, number is too large. Enter a number between 0 and {es.Count-1}. Try again.");

		return GetRecDevice();
	}

	if(int_result < 0)
	{
		Console.WriteLine();
		Console.WriteLine($"Error, number must be positive. Enter a number between 0 and {es.Count - 1}. Try again.");

		return GetRecDevice();
	}


	return int_result;
}

void Record(MMDevice device)
{
	var outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "NAudio");
	Directory.CreateDirectory(outputFolder);
	var outputFilePath = Path.Combine(outputFolder, "recorded.wav");
	var capture = new WasapiLoopbackCapture();
	var writer = new WaveFileWriter(outputFilePath, capture.WaveFormat);

	capture.RecordingStopped += (s, a) =>
	{
		writer.Dispose();
		writer = null;
		capture.Dispose();
	};

	capture.DataAvailable += (s, a) =>
	{
		writer.Write(a.Buffer, 0, a.BytesRecorded);
		if (writer.Position > capture.WaveFormat.AverageBytesPerSecond * 20)
		{
			capture.StopRecording();
		}
	};

	capture.StartRecording();

	while (capture.CaptureState != NAudio.CoreAudioApi.CaptureState.Stopped)
	{
		Thread.Sleep(500);
	}
}