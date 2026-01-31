using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleRecorder;

internal delegate SaveResult SaveAction(string micPath, string stereoPath, string isoPart, string outputFolder);

internal ref struct SaveResult
{
	internal required string? FinalPath;
	internal required bool Success;
}
