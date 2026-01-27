using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Runtime.InteropServices;
using static System.Console;

namespace SimpleRecorder;
class Program
{
	static string outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "SimpleRecorder");
	static string config = Path.Combine(outputFolder, ".config");
	static string stereopath = string.Empty;
	static string inpath = string.Empty;
	static string finalpath = string.Empty;
	static ISampleProvider silenceprovider;

	static DateTime start = DateTime.Now;
	static DateTime end;

	static WaveFileWriter waveFile;
	static WaveFileWriter stereoFile;

	static WaveInEvent waveIn;
	static WaveOutEvent silenceOut;
	static WasapiLoopbackCapture loopback;

	static bool isRecording = false;

	[DllImport("Kernel32")]
	private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

	private delegate bool EventHandler(CtrlType sig);
	static EventHandler _handler;

	enum CtrlType
	{
		CTRL_C_EVENT = 0,
		CTRL_BREAK_EVENT = 1,
		CTRL_CLOSE_EVENT = 2,
		CTRL_LOGOFF_EVENT = 5,
		CTRL_SHUTDOWN_EVENT = 6
	}

	private static bool Handler(CtrlType sig)
	{
		switch (sig)
		{
			case CtrlType.CTRL_C_EVENT:
			case CtrlType.CTRL_LOGOFF_EVENT:
			case CtrlType.CTRL_SHUTDOWN_EVENT:
			case CtrlType.CTRL_CLOSE_EVENT:
			default:
				CloseAndWrite();
				return false;
		}
	}

	static Task Main(string[] args)
	{
		_handler += new EventHandler(Handler);
		SetConsoleCtrlHandler(_handler, true);

		(int mic_id, string name) id_info;
		var cts = new CancellationTokenSource();

		if (args.Length == 1 && args[0] == "setup")
		{
			id_info = Setup();
		}
		else
		{
			id_info = ConfigToID();
		}

		CancelKeyPress += (_, args) =>
		{
			cts.Cancel();
			cts.Dispose();
			args.Cancel = true;
		};

		loopback = new WasapiLoopbackCapture();

		waveIn = new NAudio.Wave.WaveInEvent
		{
			DeviceNumber = id_info.mic_id,
			BufferMilliseconds = 100,
			WaveFormat = loopback.WaveFormat
		};

		silenceprovider = new SilenceProvider(loopback.WaveFormat).ToSampleProvider();

		silenceOut = new WaveOutEvent();
		silenceOut.Init(silenceprovider);

		start = DateTime.Now;

		string iso = $"{start:yyyy-MM-ddTHH-mm-ss}";

		inpath =Path.Combine(outputFolder, $".{iso}_mic.wav");
		stereopath = Path.Combine(outputFolder,$".{iso}_stereo.wav");
		finalpath = Path.Combine(outputFolder,$"{iso}.wav");

		waveFile = new WaveFileWriter(inpath,loopback.WaveFormat);
		stereoFile = new WaveFileWriter(stereopath, loopback.WaveFormat);

		waveIn.DataAvailable += new EventHandler<WaveInEventArgs>(waveIn_DataAvailable);

		loopback.DataAvailable += Loopback_DataAvailable;

		isRecording = true;
		silenceOut.Play();
		waveIn.StartRecording();
		loopback.StartRecording();

		return RunLoop(cts.Token);

	}

	static async Task RunLoop(CancellationToken cancellation)
	{
		try
		{
			while (!cancellation.IsCancellationRequested)
			{
				await Task.Delay(1000, cancellation);


				int cursorpos = Console.CursorTop;
				Console.SetCursorPosition(0,cursorpos);

				end = DateTime.Now;

				string content =$"Recording...{deltaToString()}";

				string wslen = new string(' ', Console.BufferWidth - content.Length);

				Write($"{content}{wslen}");
				Console.SetCursorPosition(content.Length, cursorpos);

			}
		}
		catch (TaskCanceledException)
		{
			CloseAndWrite();
		}
		catch (Exception exception)
		{
			WriteLine(exception.ToString());
		}
	}

	static string deltaToString() => (end - start).ToString(@"hh\:mm\:ss");


	static void CloseAndWrite()
	{
		if (!isRecording) return;
		isRecording = false;

		waveIn.StopRecording();
		loopback.StopRecording();
		silenceOut.Stop();

		waveIn.Dispose();
		waveIn = null;

		loopback.Dispose();
		loopback = null;

		waveFile.Dispose();
		waveFile = null;

		stereoFile.Dispose();
		stereoFile = null;

		silenceOut.Dispose();
		silenceOut = null;

		using (var reader1 = new AudioFileReader(inpath))
		{
			using (var reader2 = new AudioFileReader(stereopath))
			{
				var mixer = new MixingSampleProvider([reader1, reader2]);
				WaveFileWriter.CreateWaveFile16(finalpath, mixer);
			}
		}


		File.Delete(inpath);
		File.Delete(stereopath);

		Console.WriteLine();
		Console.WriteLine();
		Console.WriteLine($"Saved {deltaToString()} to {finalpath}");
	}

	static void waveIn_DataAvailable(object sender, WaveInEventArgs e)
	{
		if (waveFile is not null)
		{
			waveFile.Write(e.Buffer, 0, e.BytesRecorded);
			waveFile.Flush();
		}
	}

	private static void Loopback_DataAvailable(object? sender, WaveInEventArgs e)
	{
		if(stereoFile is not null)
		{
			stereoFile.Write(e.Buffer, 0, e.BytesRecorded);
			stereoFile.Flush();
		}
	}

	static (int id,string device_name) Setup()
	{
		Directory.CreateDirectory(outputFolder);

		var inCount = NAudio.Wave.WaveInEvent.DeviceCount;

		for (int i = 0; i < inCount; i++)
		{
			var caps = WaveInEvent.GetCapabilities(i);
			Console.WriteLine($"{i} , {caps.ProductName}");
		}

		var ok = InputToDevice(NAudio.Wave.WaveInEvent.DeviceCount, out var device, out int mic_id);

		if (!ok) userExit("Cancelled setting an input.");

		File.WriteAllText(config, $"{device.ProductName}");
		File.SetAttributes(config, File.GetAttributes(config) | FileAttributes.Hidden);

		return (mic_id,device.ProductName);
	}

	static (int id, string device_name) ConfigToID()
	{
		if (!File.Exists(config))
		{
			return Setup();
		}

		var lines = File.ReadAllLines(config);

		if (lines.Length != 1) userExit("Error, expected one line in the config file. Run setup to fix the malformed config file.");

		string maybe_name = lines[0];

		int device_id = -1;

		var inCount = NAudio.Wave.WaveInEvent.DeviceCount;

		for (int i = 0; i < inCount; i++)
		{
			var caps = WaveInEvent.GetCapabilities(i);
			if (string.Equals(caps.ProductName, maybe_name))
			{
				device_id = i;
				break;
			}
		}

		if (device_id == -1)
		{
			Console.WriteLine($"The device stored in the config file is no longer recognized. That device was {maybe_name} and is not currently an available option. Did you disable it? The app will now continue to setup to pick a new input device.\n\n");
			return Setup();
		}

		return (device_id,maybe_name);
	}

	static bool InputToDevice(int devCount, out WaveInCapabilities devicecaps, out int dev_id)
	{
		var rec_index = GetRecDevice(devCount);
		var device = WaveInEvent.GetCapabilities(rec_index);

		var ok = validateRecDevice(rec_index, device);

		if (!ok)
		{
			devicecaps = default;
			dev_id = -1;
			return InputToDevice(devCount, out devicecaps, out dev_id);
		}
		else
		{
			devicecaps = device;
			dev_id = rec_index;
			return true;
		}
	}

	static void userExit(string msg)
	{
		Console.WriteLine($"UUser aborted, reason: {msg}. Press any key to quit.");
		Console.ReadKey();
		Environment.Exit(0);
	}
	static bool validateRecDevice(int i, WaveInCapabilities device)
	{
		Console.WriteLine();
		Console.WriteLine($"Selected {i} , {device.ProductName}; Is this correct? Y/N");

		var result = Console.ReadKey();

		if (result.Key == ConsoleKey.Y) return true;
		else if (result.Key == ConsoleKey.N) return false;
		else if (result.Key == ConsoleKey.Escape) userExit("User quit the device selection");

		return validateRecDevice(i, device);
	}

	static int GetRecDevice(int max)
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
			return GetRecDevice(max);
		}

		if (int_result > max - 1)
		{
			Console.WriteLine();
			Console.WriteLine($"Error, number is too large. Enter a number between 0 and {max - 1}. Try again.");

			return GetRecDevice(max);
		}

		if (int_result < 0)
		{
			Console.WriteLine();
			Console.WriteLine($"Error, number must be positive. Enter a number between 0 and {max - 1}. Try again.");

			return GetRecDevice(max);
		}


		return int_result;
	}



}