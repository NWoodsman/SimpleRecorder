using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Markup;
using static System.Console;

namespace SimpleRecorder;
class Program
{
	static string outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "SimpleRecorder");
	
	static string stereopath = string.Empty;
	static string inpath = string.Empty;
	static string iso = string.Empty;

	static ISampleProvider silenceprovider;

	static DateTime start = DateTime.Now;
	static DateTime end;

	static WaveFileWriter waveFile;
	static WaveFileWriter stereoFile;

	static WaveInEvent waveIn;
	static WaveOutEvent silenceOut;
	static WasapiLoopbackCapture loopback;

	static Config config;

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
				CloseAndWrite(config);
				return false;
		}
	}

	static Task Main(string[] args)
	{
		_handler += new EventHandler(Handler);
		SetConsoleCtrlHandler(_handler, true);

		var cts = new CancellationTokenSource();

		switch (args.Length)
		{
			case 1:
				switch (args[0])
				{
					case "setup" or "Setup":
						config = Config.Setup();
						break;
					case "fix" or "Fix":
						config = Config.FromFile();
						Repair.RepairFiles(outputFolder,config);
						return Task.CompletedTask;
					default:
						Console.WriteLine($"{args[0]} is not a recognized argument. Exiting now.");
						return Task.CompletedTask;
				}
				break;
			case 2:
				switch (args[0])
				{
					case "format" or "Format":
						string newfmt = args[1];

						if(Regex.Match("", @"^\.?(?:Wav|WAV|wav)$").Success)
						{
							config = Config.FromFile();
							config.Format = AudioFormat.Wav;
							Console.WriteLine("Changed format to .wav");
							config.WriteToConfigFile();
							return Task.CompletedTask;
						}
						else if(Regex.Match("", @"^\.?(?:Mp3|MP3|mp3)$").Success)
						{
							config = Config.FromFile();
							config.Format = AudioFormat.Wav;
							Console.WriteLine("Changed format to .wav");
							config.WriteToConfigFile();
							return Task.CompletedTask;
						}
						else
						{
							Console.WriteLine($"{newfmt} is not a recognized argument. Exiting now.");
							return Task.CompletedTask;

						}
					default:
						Console.WriteLine($"{args[0]} is not a recognized argument. Exiting now.");
						return Task.CompletedTask;
				}
			default:
				config = Config.FromFile();
				break;
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
			DeviceNumber = config.Mic_ID,
			BufferMilliseconds = 100,
			WaveFormat = loopback.WaveFormat
		};

		silenceprovider = new SilenceProvider(loopback.WaveFormat).ToSampleProvider();

		silenceOut = new WaveOutEvent();
		silenceOut.Init(silenceprovider);

		start = DateTime.Now;

		iso = $"{start:yyyy-MM-ddTHH-mm-ss}";

		inpath =Path.Combine(outputFolder, $".{iso}_mic.wav");
		stereopath = Path.Combine(outputFolder,$".{iso}_stereo.wav");

		waveFile = new WaveFileWriter(inpath,loopback.WaveFormat);
		stereoFile = new WaveFileWriter(stereopath, loopback.WaveFormat);

		waveIn.DataAvailable += new EventHandler<WaveInEventArgs>(waveIn_DataAvailable);

		loopback.DataAvailable += Loopback_DataAvailable;

		isRecording = true;
		silenceOut.Play();
		waveIn.StartRecording();
		loopback.StartRecording();

		return RunLoop(cts.Token, config);

	}

	static async Task RunLoop(CancellationToken cancellation, Config config)
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
			CloseAndWrite(config);
		}
		catch (Exception exception)
		{
			WriteLine(exception.ToString());
		}
	}

	static string deltaToString() => (end - start).ToString(@"hh\:mm\:ss");


	static void CloseAndWrite(Config config)
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

		var saveAction = config.FormatToSaveAction();

		var result = saveAction(inpath, stereopath, iso, outputFolder);

		if (result.Success)
		{
			File.Delete(inpath);
			File.Delete(stereopath);
			Console.WriteLine($"\n\nSaved {deltaToString()} to {result.FinalPath}");
		}
		else
		{
			Console.WriteLine("\n\n Recording failed. The recorded channels can be fixed by running the app with the \"Fix\" argument.");
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




}