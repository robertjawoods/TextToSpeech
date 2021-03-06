﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace JocysCom.TextToSpeech.Monitor.Capturing.Monitors
{
	public partial class DisplayMonitor
	{
		#region Rounded Colors

		/// <summary>
		/// Convert 0-511 integer to [A]RGB integer. Only RGB values affected.
		/// </summary>
		public static int GetColor(int v)
		{
			// Example:
			// var cols = new List<string>();
			// for (int i = 0; i < 512; i++)
			// 	cols.Add((Capturing.DisPcapDevice.GetColor(i) & // 0xFFFFFF).ToString("X6"));
			// MessageBox.Show(string.Join(" ", cols));
			//
			// DEC   OCT   ARGB HEX
			// ---   ---   --------
			//   0 =   0 = FF000000
			//   1 =   1 = FF000020
			//   2 =   2 = FF000040
			//   3 =   3 = FF000060
			//   4 =   4 = FF000080
			//   5 =   5 = FF0000A0
			//   6 =   6 = FF0000C0
			//   7 =   7 = FF0000E0
			//   8 =  10 = FF002000
			//   9 =  11 = FF002020
			//  10 =  12 = FF002040
			// ...   ...   ........
			// 511 = 777 = FFE0E0E0
			var num = Convert.ToString(v, 8);
			var rgb = num.Select(x => int.Parse(x.ToString()) * 0x20).ToArray();
			Array.Reverse(rgb);
			var b = rgb.Length > 0 ? rgb[0] : 0;
			var g = rgb.Length > 1 ? rgb[1] : 0;
			var r = rgb.Length > 2 ? rgb[2] : 0;
			var c = (0xFF << 24) | (r << 16) | (g << 8) | b;
			return c;
		}

		/// <summary>
		/// Convert [A]RGB integer to 0-511 integer. Only RGB values affected.
		/// </summary>
		public static int GetValue(int color)
		{
			// Example:
			// var vals = new List<int>();
			// for (int i = 0; i < cols.Count; i++)
			// 	vals.Add(Capturing.DisPcapDevice.GetValue(Convert.ToInt32(cols[i], 16)));
			// MessageBox.Show(string.Join(" ", vals));
			//
			//  ARGB HEX   OCT   DEC 
			//  --------   ---   --- 
			//  FF000000 =   0 =   0
			//  FF000020 =   1 =   1
			//  FF000040 =   2 =   2
			//  FF000060 =   3 =   3
			//  FF000080 =   4 =   4
			//  FF0000A0 =   5 =   5
			//  FF0000C0 =   6 =   6
			//  FF0000E0 =   7 =   7
			//  FF002000 =  10 =   8
			//  FF002020 =  11 =   9
			//  FF002040 =  12 =  10
			//  ........   ...   ... 
			//  FFE0E0E0 = 777 = 511
			var r = (color >> 16) & 0xFF;
			var g = (color >> 8) & 0xFF;
			var b = color & 0xFF; ;
			// Round.
			var r8 = Math.Round((decimal)r / (decimal)0x20, 0);
			var g8 = Math.Round((decimal)g / (decimal)0x20, 0);
			var b8 = Math.Round((decimal)b / (decimal)0x20, 0);
			var oct = string.Format("{0}{1}{2}", r8, g8, b8).TrimStart('0');
			if (oct == "")
				oct = "0";
			var v = Convert.ToInt32(oct, 8);
			return v;
		}

		#endregion

		public static int[] ColorsFromRgbs(string text)
		{
			var rx = new Regex("[0-9A-F]{6}", RegexOptions.IgnoreCase);
			var matches = rx.Matches(text);
			var values = new List<int>();
			if (matches.Count > 0)
			{
				var ms = new MemoryStream();
				var bw = new BinaryWriter(ms);
				foreach (Match match in matches)
				{
					var hex = match.Value;
					var v = int.Parse(hex, System.Globalization.NumberStyles.HexNumber);
					values.Add(v);
				}
			}
			return values.ToArray();
		}

	}
}
