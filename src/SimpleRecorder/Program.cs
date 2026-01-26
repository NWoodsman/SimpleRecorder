using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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

	static WaveFileWriter waveFile;
	static WaveFileWriter stereoFile;

	static WaveInEvent waveIn;
	static WaveOutEvent silenceOut;
	static WasapiLoopbackCapture loopback;

	static Task Main(string[] args)
	{
		int mic_id;
		var cts = new CancellationTokenSource();

		if (args.Length == 1 && args[0] == "setup")
		{
			mic_id = Setup();
		}
		else
		{
			mic_id = ConfigToID();
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
			DeviceNumber = mic_id,
			BufferMilliseconds = 100,
			WaveFormat = loopback.WaveFormat
		};

		silenceprovider = new SilenceProvider(loopback.WaveFormat).ToSampleProvider();

		silenceOut = new WaveOutEvent();
		silenceOut.Init(silenceprovider);

		MMDeviceEnumerator e = new MMDeviceEnumerator();

		var def = e.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

		start = DateTime.Now;

		string iso = $"{start:yyyy-MM-ddTHH-mm-ss}";

		inpath =Path.Combine(outputFolder, $".{iso}_mic.wav");
		stereopath = Path.Combine(outputFolder,$".{iso}_stereo.wav");
		finalpath = Path.Combine(outputFolder,$"{iso}.wav");

		waveFile = new WaveFileWriter(inpath,loopback.WaveFormat);
		stereoFile = new WaveFileWriter(stereopath, loopback.WaveFormat);

		waveIn.DataAvailable += new EventHandler<WaveInEventArgs>(waveIn_DataAvailable);

		loopback.DataAvailable += Loopback_DataAvailable;

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

				var time = DateTime.Now;

				var delta = time - start;

				string format = @"hh\:mm\:ss";

				string content =$"Recording...{delta.ToString(format)}";

				string wslen = new string(' ', Console.BufferWidth - content.Length);

				Write($"{content}{wslen}");
				Console.SetCursorPosition(content.Length, cursorpos);

			}
		}
		catch (TaskCanceledException)
		{
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

			Environment.Exit(0);
		}
		catch (Exception exception)
		{
			WriteLine(exception.ToString());
		}
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

	static int Setup()
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

		File.WriteAllText(config, $"{mic_id}\n{-1}");
		File.SetAttributes(config, File.GetAttributes(config) | FileAttributes.Hidden);

		return mic_id;
	}

	static int ConfigToID()
	{
		if (!File.Exists(config))
		{
			return Setup();
		}

		var lines = File.ReadAllLines(config);

		if (lines.Length != 1) userExit("Error, expected one line in the config file. Run setup to fix the malformed config file.");

		bool inparsed = int.TryParse(lines[0], out int inresult);

		if (!inparsed) userExit("Error, config line 1 is not an integer. Run setup to fix the malformed config file.");

		return inresult;
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