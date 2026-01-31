using NAudio.CoreAudioApi;
using NAudio.MediaFoundation;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleRecorder;


internal class Config
{
	static string configFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimpleRecorder");
	
	static string config = Path.Combine(configFolder, ".config");

	internal required string MicName;
	internal required AudioFormat Format;
	internal required int Mic_ID;

	internal void WriteToConfigFile()
	{
		File.WriteAllText(config, $"{MicName}\n{Format}");
		File.SetAttributes(config, File.GetAttributes(config) | FileAttributes.Hidden);
	}

	internal static Config Setup()
	{
		Directory.CreateDirectory(configFolder);

		var inCount = NAudio.Wave.WaveInEvent.DeviceCount;

		for (int i = 0; i < inCount; i++)
		{
			var caps = WaveInEvent.GetCapabilities(i);
			Console.WriteLine($"{i} , {caps.ProductName}");
		}

		var ok = InputToDevice(NAudio.Wave.WaveInEvent.DeviceCount, out var device, out int mic_id);

		if (!ok) UserExit("Cancelled setting an input.");

		Config config = new()
		{
			MicName = device.ProductName,
			Format = AudioFormat.Wav,
			Mic_ID = mic_id,
		};

		config.WriteToConfigFile();

		return config;
	}

	internal static Config FromFile()
	{
		if (!File.Exists(config))
		{
			return Setup();
		}

		var lines = File.ReadAllLines(config);

		switch (lines.Length)
		{
			case 0:
				Console.WriteLine("The config file is empty. We will run the setup process to fix the cofig file.");
				return Setup();
			case 1:
				string maybe_name_1 = lines[0];

				if (!TryStringToMicID(maybe_name_1, out int mic_id_1))
				{
					Console.WriteLine($"The device stored in the config file is no longer recognized. That device was {maybe_name_1} and is not currently an available option. Did you disable it? The app will now continue to setup to pick a new input device.\n\n");
					return Setup();
				}
				else
				{
					return new Config
					{
						Format = AudioFormat.Mp3,
						MicName = maybe_name_1,
						Mic_ID = mic_id_1
					};
				}
			case 2:
				string maybe_name_2 = lines[0];

				if (!TryStringToMicID(maybe_name_2, out int mic_id_2))
				{
					Console.WriteLine($"The device stored in the config file is no longer recognized. That device was {maybe_name_2} and is not currently an available option. Did you disable it? The app will now continue to setup to fix the config file.\n\n");
					return Setup();
				}

				string maybe_fmt_2 = lines[1];

				if (!Enum.TryParse<AudioFormat>(maybe_fmt_2, out var fmt))
				{
					Console.WriteLine($"The format stored in the config file ({maybe_fmt_2}) is not recognized. The app will now continue to setup to fix the config file.\n\n");

				}

				return new Config
				{
					Format = fmt,
					MicName = maybe_name_2,
					Mic_ID = mic_id_2
				};
			default:
				Console.WriteLine($"There are too many lines in the config file. The app will now continue to setup to fix the config file.\n\n");
				return Setup();
		}

	}

	static bool TryStringToMicID(string maybe_name, out int deviceID)
	{
		deviceID = -1;

		var inCount = NAudio.Wave.WaveInEvent.DeviceCount;

		for (int i = 0; i < inCount; i++)
		{
			var caps = WaveInEvent.GetCapabilities(i);
			if (string.Equals(caps.ProductName, maybe_name))
			{
				deviceID = i;
				break;
			}
		}

		if (deviceID == -1) return false;

		return true;

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

	static bool validateRecDevice(int i, WaveInCapabilities device)
	{
		Console.WriteLine();
		Console.WriteLine($"Selected {i} , {device.ProductName}; Is this correct? Y/N");

		var result = Console.ReadKey();

		if (result.Key == ConsoleKey.Y) return true;
		else if (result.Key == ConsoleKey.N) return false;
		else if (result.Key == ConsoleKey.Escape) UserExit("User quit the device selection");

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
			UserExit("User quit device selection.");
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

	/// <summary>
	/// Return a delegate representing the means to save files using the user-desired format.
	/// </summary>
	/// <returns>A delegate representing the way to save files in the user-desired format.</returns>
	/// <exception cref="NotImplementedException"></exception>
	internal SaveAction FormatToSaveAction() => Format switch
	{
		AudioFormat.Wav => SaveToWav,
		AudioFormat.Mp3 => SaveToMp3,
	};

	static SaveResult SaveToWav(string micPath, string stereoPath, string isoPart, string outputFolder)
	{
		var filename = $"{isoPart}.wav";
		var finalpath = Path.Combine(outputFolder, filename);

		using (var reader1 = new AudioFileReader(micPath))
		{
			using (var reader2 = new AudioFileReader(stereoPath))
			{
				var mixer = new MixingSampleProvider([reader1, reader2]);

				WaveFileWriter.CreateWaveFile16(finalpath, mixer);

			}
		}

		return new SaveResult { FinalPath = finalpath, Success = true};
	}

	static SaveResult SaveToMp3(string micPath, string stereoPath, string isoPart, string outputFolder)
	{
		MediaFoundationApi.Startup();

		var filename = $"{isoPart}.mp3";
		var finalpath = Path.Combine(outputFolder, filename);

		using (var reader1 = new AudioFileReader(micPath))
		{
			using (var reader2 = new AudioFileReader(stereoPath))
			{
				var mixer = new MixingSampleProvider([reader1, reader2]);

				MediaFoundationEncoder.EncodeToMp3(mixer.ToWaveProvider16(), finalpath, 128_000);

			}

		}

		return new SaveResult { FinalPath = finalpath, Success = true};
	}
}
