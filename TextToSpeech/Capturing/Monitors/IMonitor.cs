﻿using JocysCom.ClassLibrary;
using System;

namespace JocysCom.TextToSpeech.Monitor.Capturing.Monitors
{
	public interface IMonitor
	{
		void Start();
		void Stop();
		event EventHandler<EventArgs<string>> MessageReceived;
		long MessagesReceived { get; set; }
	}
}
