﻿using Amazon;
using Amazon.Polly.Model;
using JocysCom.ClassLibrary.Controls;
using JocysCom.TextToSpeech.Monitor.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace JocysCom.TextToSpeech.Monitor.Controls.Options
{
	public partial class AmazonPollyUserControl : UserControl
	{
		public AmazonPollyUserControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			AccessKeyTextBox.Text = SettingsManager.Options.AmazonAccessKey;
			SecretKeyTextBox.Text = SettingsManager.Options.AmazonSecretKey;
			AccessKeyTextBox.TextChanged += AccessKeyTextBox_TextChanged;
			SecretKeyTextBox.TextChanged += SecretKeyTextBox_TextChanged;
			var regions = RegionEndpoint.EnumerableAllRegions
				.OrderBy(x => x.ToString())
				.ToArray();
			RegionComboBox.DataSource = regions;
			var region = regions.FirstOrDefault(x => x.SystemName == SettingsManager.Options.AmazonRegionSystemName);
			if (region == null)
				region = regions.FirstOrDefault(x => x.ToString().Contains("EU West") && x.ToString().Contains("London"));
			if (region == null)
				region = regions.FirstOrDefault(x => x.ToString().Contains("EU West"));
			if (region == null)
				region = regions.FirstOrDefault();
			if (region != null)
				RegionComboBox.SelectedItem = region;
			RegionComboBox.SelectedIndexChanged += RegionComboBox_SelectedIndexChanged;
			RegionComboBox_SelectedIndexChanged(null, null);
			//AmazonEnabledCheckBox.DataBindings.Add(nameof(AmazonEnabledCheckBox.Checked), SettingsManager.Options, nameof(SettingsManager.Options.AmazonEnabled));
		}

		private void RegionComboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			var region = (RegionEndpoint)RegionComboBox.SelectedItem;
			if (region != null)
				SettingsManager.Options.AmazonRegionSystemName = region.SystemName;
			RefreshVoicesButton_Click(null, null);
		}

		private void SecretKeyTextBox_TextChanged(object sender, EventArgs e)
		{
			SettingsManager.Options.AmazonSecretKey = SecretKeyTextBox.Text;
		}

		private void AccessKeyTextBox_TextChanged(object sender, EventArgs e)
		{
			SettingsManager.Options.AmazonAccessKey = AccessKeyTextBox.Text;
		}

		private void AwsLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			var link = ((LinkLabel)sender).Text;
			MainHelper.OpenUrl(link);
		}

		private void RefreshVoicesButton_Click(object sender, EventArgs e)
		{
			var voices = GetAmazonVoices();
			VoicesComboBox.DataSource = voices;
		}

		List<InstalledVoiceEx> GetAmazonVoices()
		{
			StatusTextBox.Text = "";
			var client = new Voices.AmazonPolly(
				SettingsManager.Options.AmazonAccessKey,
				SettingsManager.Options.AmazonSecretKey,
				SettingsManager.Options.AmazonRegionSystemName
			);
			var voices = client.GetVoices();
			var result = client.LastException == null ? "Success" : client.LastException.Message;
			StatusTextBox.Text = string.Format("{0:HH:mm:ss}: {1}", DateTime.Now, result);
			var installedVoices = voices.Select(x => new InstalledVoiceEx(x));
			return installedVoices.OrderBy(x => x.Name).ToList();
		}

		private void Global_Exception(object sender, ClassLibrary.EventArgs<Exception> e)
		{
			throw new NotImplementedException();
		}

		private void SpeakButton_Click(object sender, EventArgs e)
		{
			StatusTextBox.Text = "";
			var promptBuilder = new System.Speech.Synthesis.PromptBuilder();
			promptBuilder.AppendText(MessageTextBox.Text);
			var client = new Voices.AmazonPolly(
				SettingsManager.Options.AmazonAccessKey,
				SettingsManager.Options.AmazonSecretKey,
				SettingsManager.Options.AmazonRegionSystemName
			);
			var voice = (Voice)((InstalledVoiceEx)VoicesComboBox.SelectedItem).Voice;
			Amazon.Polly.Engine engine = null;
			if (voice.SupportedEngines.Contains(Amazon.Polly.Engine.Neural))
				engine = Amazon.Polly.Engine.Neural;
			var buffer = client.SynthesizeSpeech(voice.Id, promptBuilder.ToXml(), null, engine);
			var result = client.LastException == null ? "Success" : client.LastException.Message;
			StatusTextBox.Text = string.Format("{0:HH:mm:ss}: {1}", DateTime.Now, result);
			var item = Global.DecodeToPlayItem(buffer);
			Global.playlist.Add(item);
		}

	}
}
