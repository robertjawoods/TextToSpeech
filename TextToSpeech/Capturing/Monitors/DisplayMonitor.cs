﻿using JocysCom.ClassLibrary.Drawing;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace JocysCom.TextToSpeech.Monitor.Capturing.Monitors
{
	public partial class DisplayMonitor : MonitorBase
	{

		public int lastChange;

		private void _Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			try
			{
				int change;
				string message;
				CaptureMessage(out change, out message);
				if (!string.IsNullOrEmpty(message) && change != lastChange)
				{
					lastChange = change;
					OnMessageReceived(message);
				}
				// If message is null then pixels where not found on the screen and full scan was done.
				if (message == null)
				{
					// Wait for 1 second.
					// Logical delay without blocking the current thread.
					System.Threading.Tasks.Task.Delay(1000).Wait();
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
			finally
			{
				//if (IsRunning)
				//	_Timer.Start();
			}
		}

		JocysCom.ClassLibrary.HiResTimer _Timer;

		public int ScanInterval
		{
			get { return _Interval; }
			set
			{
				var isRunning = IsRunning;
				if (isRunning)
					_Timer.Stop();
				_Interval = value;
				if (isRunning)
					Start();
				OnPropertyChanged();
			}
		}

		int _Interval = 100;

		public override void Start()
		{
			lock (monitorLock)
			{
				if (IsDisposing)
					return;
				if (_Timer != null)
				{
					// Server is already running;
					return;
				}
				_Timer = new JocysCom.ClassLibrary.HiResTimer();
				_Timer.Interval = ScanInterval;
				//_Timer.AutoReset = false;
				_Timer.Elapsed += _Timer_Elapsed;
				_Timer.Start();
				_IsRunning = true;
			}
		}

		public override void Stop()
		{
			lock (monitorLock)
			{
				// If server is running then...
				if (_Timer != null)
				{
					_Timer.Dispose();
					_Timer = null;
				}
				_IsRunning = false;
			}
		}

		#region Create Image

		public int _CurrentChangeValue;

		byte[] ColorPrefixBytes;
		byte[] ColorPrefixBytesBlanked;
		bool IsColorPrefixBytesBlanked;

		public void SetColorPrefix(byte[] rgbPrefixBytes)
		{
			ColorPrefixBytes = rgbPrefixBytes;
			ColorPrefixBytesBlanked = InsertBlankPixels(rgbPrefixBytes);
		}

		/// <summary>
		/// BBGGRR,000000,BBGGRR,000000...
		/// </summary>
		/// <param name="bytes"></param>
		/// <returns></returns>
		public static byte[] InsertBlankPixels(byte[] bytes)
		{
			// Insert blank pixels.
			var data = new byte[bytes.Length * 2];
			for (int p = 0; p < bytes.Length; p += 3)
			{
				data[p * 2 + 0] = bytes[p + 0];
				data[p * 2 + 1] = bytes[p + 1];
				data[p * 2 + 2] = bytes[p + 2];
			}
			return data;
		}

		public static void RemoveBlankPixels(byte[] bytes, ref byte[] destination)
		{
			// Remove blank pixels.
			var len = bytes.Length / 2;
			if (destination == null)
				destination = new byte[len];
			if (destination.Length != len)
				Array.Resize(ref destination, len);
			for (var p = 0; p < len; p += 3)
			{
				destination[p + 0] = bytes[p * 2 + 0];
				destination[p + 1] = bytes[p * 2 + 1];
				destination[p + 2] = bytes[p * 2 + 2];
			}
		}

		/// <summary>
		/// Image: prefix[6] + change[1] + size[1] + message_bytes
		/// </summary>
		public Bitmap CreateImage(string message, bool insertBlankPixels = false)
		{
			var messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
			// Create object to write.
			_CurrentChangeValue++;
			// Reset count if too big.
			if (_CurrentChangeValue > 0xFFFFFF)
				_CurrentChangeValue = 0;
			var ms = new MemoryStream();
			var br = new System.IO.BinaryWriter(ms);
			// Write Prefix bytes.
			br.Write(ColorPrefixBytes);
			// Write change value.
			var changePixelBytes = Basic.ColorsToBytes(new[] { _CurrentChangeValue });
			br.Write(changePixelBytes);
			// Write message size value.		
			var sizePixelBytes = Basic.ColorsToBytes(new[] { messageBytes.Length });
			br.Write(sizePixelBytes);
			// Write message bytes.
			br.Write(messageBytes);
			// Write missing bytes for complete color.
			var missingBytes = (3 - (messageBytes.Length % 3)) % 3;
			for (int i = 0; i < missingBytes; i++)
				br.Write((byte)0);
			var allBytes = ms.ToArray();
			br.Dispose();
			if (insertBlankPixels)
			{
				// Insert blank pixels in the middle.
				allBytes = InsertBlankPixels(allBytes);
			}
			var pixelCount = allBytes.Length / 3;
			// Set image format.
			var format = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
			// If format contains Alpha color then add alpha color.
			if (format == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
				allBytes = JocysCom.ClassLibrary.Drawing.Basic.BppFrom24To32Bit(allBytes);
			// Image width will be double since every second pixel is blank.
			var image = new Bitmap(pixelCount, 1, format);
			var g = Graphics.FromImage(image);
			// Image bytes size must match new bytes size.
			// var imageBytes = Basic.GetImageBytes(image);
			Basic.SetImageBytes(image, allBytes);
			g.Dispose();
			return image;
		}

		// Reuse objects to save memory.
		byte[] lineBytes;
		Bitmap lineBitmap;
		byte[] lineBufferNoBlanks;

		/// <summary>
		/// Image: prefix[6] + change[1] + size[1] + message_bytes
		/// </summary>
		public void CaptureTextFromPosition(out int change, out string message, Screen screen)
		{
			change = 0;
			message = null;
			if (screen == null)
				return;
			var x = SettingsManager.Options.DisplayMonitorPositionX;
			var y = SettingsManager.Options.DisplayMonitorPositionY;
			var sw = screen.Bounds.Width;
			var sh = screen.Bounds.Height;
			// Make sure take pixel pairs till the end of the line.
			var length = sw - x;
			length = length - (length % 2);
			// Take screenshot of the line.
			Basic.CaptureImage(ref lineBitmap, screen.Bounds.X + x, screen.Bounds.Y + y, length, 1);
			// var asm = new JocysCom.ClassLibrary.Configuration.AssemblyInfo();
			// var path = asm.GetAppDataPath(false, "Images\\Screen_{0:yyyyMMdd_HHmmss_fff}.png", DateTime.Now);
			// lineBitmap.Save(path, ImageFormat.Png);
			Basic.GetImageBytes(ref lineBytes, lineBitmap);
			var prefixBytes = SettingsManager.Options.DisplayMonitorHavePixelSpaces
				? ColorPrefixBytesBlanked
				: ColorPrefixBytes;
			var index = JocysCom.ClassLibrary.Text.Helper.IndexOf(lineBytes, prefixBytes);
			// If not found then...
			if (index == -1)
				return;
			//	StatusTextBox.Text += string.Format("Prefix found");
			if (SettingsManager.Options.DisplayMonitorHavePixelSpaces)
				RemoveBlankPixels(lineBytes, ref lineBufferNoBlanks);
			else
				lineBufferNoBlanks = lineBytes;
			var prefix = ColorPrefixBytes;
			// Skip prefix.
			var ms = new MemoryStream(lineBufferNoBlanks, prefix.Length, lineBufferNoBlanks.Length - prefix.Length);
			var br = new System.IO.BinaryReader(ms);
			change = ReadRgbInt(br);
			var messageSize = ReadRgbInt(br);
			var messageBytes = new byte[messageSize];
			br.Read(messageBytes, 0, messageSize);
			message = System.Text.Encoding.UTF8.GetString(messageBytes);
			br.Dispose();
		}

		object captureLock = new object();

		/// <summary>
		/// Image: prefix[6] + change[1] + size[1] + message_bytes
		/// </summary>
		public void CaptureMessage(out int change, out string message)
		{
			lock (captureLock)
			{
				CaptureTextFromPosition(out change, out message, CurrentScreen);
				if (message == null)
				{
					var success = FindImagePositionOnScreen();
					if (success)
						CaptureTextFromPosition(out change, out message, CurrentScreen);
				}
			}
		}

		public Screen CurrentScreen;

		// Reuse objects to save memory.
		byte[] screenBytes;
		Bitmap screenBitmap;
		public bool FindImagePositionOnScreen()
		{
			// Look for image on all screens.
			foreach (var screen in Screen.AllScreens)
			{
				//	StatusTextBox.Text = "Wrong Bytes. Searching...";
				Basic.CaptureImage(ref screenBitmap, screen);
				Basic.GetImageBytes(ref screenBytes, screenBitmap);
				// Try to find prefix image without blank pixels.
				var index = JocysCom.ClassLibrary.Text.Helper.IndexOf(screenBytes, ColorPrefixBytes);
				// If not found then try to find with blank pixels.
				var havePixelSpaces = false;
				if (index < 0)
				{
					index = JocysCom.ClassLibrary.Text.Helper.IndexOf(screenBytes, ColorPrefixBytesBlanked);
					havePixelSpaces = index > -1;
				}
				if (SettingsManager.Options.DisplayMonitorHavePixelSpaces != havePixelSpaces)
					SettingsManager.Options.DisplayMonitorHavePixelSpaces = havePixelSpaces;
				//	StatusTextBox.Text = string.Format("Pixel Index  {0}...", index);
				if (index > -1)
				{
					CurrentScreen = screen;
					var sw = screen.Bounds.Width;
					var sh = screen.Bounds.Height;
					// Get new coordinates of image.
					var pixelIndex = index / 3; // 3 bytes per pixel.
					var newX = pixelIndex % sw;
					var newY = (pixelIndex - newX) / sw;
					SettingsManager.Options.DisplayMonitorPositionX = newX;
					SettingsManager.Options.DisplayMonitorPositionY = newY;
					//StatusTextBox.Text += string.Format(" [{0}:{1}]", x, y);
					return true;
				}
			}
			return false;
		}

		public static int ReadRgbInt(BinaryReader br)
		{
			var bytes = br.ReadBytes(3);
			return bytes[2] << 16 | bytes[1] << 8 | bytes[0];
		}

		#endregion

	}
}
