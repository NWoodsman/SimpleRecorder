using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SimpleRecorder;

internal static class Repair
{
	const string fileRegexString = @"^.(\d\d\d\d-\d\d-\d\dT\d\d-\d\d-\d\d)_(mic|stereo).wav$";
	static Regex FileRegex = new Regex(fileRegexString, RegexOptions.Compiled, new TimeSpan(3000));

	struct FixItem
	{
		public bool IsSuccess;
		public string ErrorMessage;
		public string MicPath;
		public string MicName;
		public string StereoPath;
		public string StereoName;
		public string IsoPart;


	}
	static FixItem Failed => new FixItem { IsSuccess = false};

	internal static void RepairFiles(string outputFolder, Config config)
	{
		Console.WriteLine($"Repairing files in {outputFolder}:\n\n");

		var files = Directory.GetFiles(outputFolder).Select(file => (file, Path.GetFileName(file))).ToArray();

		var dotfiles = files.Select(f => (f,FileRegex.Match(f.Item2))).Where(f=>f.Item2.Success).ToArray();

		var matchgroups = dotfiles
			.Select(match => Deconstruct(match.f.file,match.Item2)).ToArray();

		var groups = matchgroups
			.GroupBy(m => m.Item3).ToArray();

		var parsed = groups.Select(g => TryParse(g,outputFolder)).ToArray();

		foreach (var f in parsed)
		{
			if (f.IsSuccess)
			{
				var saveAction = config.FormatToSaveAction();
				var result = saveAction(f.MicPath, f.StereoPath, f.IsoPart, outputFolder);
				Console.WriteLine($"\nSuccessfully repaired {f.IsoPart}_mic and stereo.\n");
			}
			else
			{
				Console.WriteLine($"Error saving file. Reason: {f.ErrorMessage}");
			}
		}

		Console.ReadKey();
	}

	static FixItem TryParse(IEnumerable<(string fullPath, string matchedFileName, string dateMatch, string micOrStereo)> raw, string outputFolder)
	{
		string? micPath = null;
		string? stereoPath = null;
		string? isoPart = null;
		string? micName = null;
		string? stereoName = null;

		foreach (var s in raw)
		{
			isoPart = s.dateMatch;
			if (s.micOrStereo == "mic")
			{
				micPath = s.fullPath;
				micName = s.matchedFileName;
			}
			else if (s.micOrStereo == "stereo")
			{
				stereoPath = s.fullPath;
				stereoName = s.matchedFileName;
			}
			else
			{
				return new FixItem
				{
					IsSuccess = false,
					ErrorMessage = $"Error parsing file; {s.matchedFileName} is not recognized as a valid track."
				};
			}
		}

		if (isoPart is null)
		{
			return new FixItem
			{
				IsSuccess = false,
				ErrorMessage =$"Error: could not determine an ISO date from a pair of files."
			};
		}

		if (micPath is null)
		{
			return new FixItem
			{
				IsSuccess = false,
				ErrorMessage = $"Error: for file {isoPart}, expected a valid microphone channel file. Please check that a mic channel file is present)"
			};
		}

		if (stereoPath is null)
		{
			return new FixItem
			{
				IsSuccess = false,
				ErrorMessage = $"Error: for file {isoPart} missing a valid stereo channel file. Please check that a stereo channel file is present)"
			};
		}

		
		
		return new FixItem 
		{
			IsSuccess = true,
			StereoName = stereoName,
			StereoPath = stereoPath,
			MicName = micName,
			MicPath = micPath,
			IsoPart = isoPart,
		};


	}

	static void mixFiles(string outputFolder, string path1, string path2, string finalpath)
	{

		using (var reader1 = new AudioFileReader(path1))
		{
			using (var reader2 = new AudioFileReader(path2))
			{
				var mixer = new MixingSampleProvider([reader1, reader2]);
				WaveFileWriter.CreateWaveFile16(finalpath, mixer);
			}
		}

	}

	static (string fullPath, string matchedFileName,string dateMatch, string micOrStereo) Deconstruct(string path, Match match)
	{
		return 
			(
			path,
			match.Value,
			match.Groups[1].Value,
			match.Groups[2].Value
			);
	}

}
