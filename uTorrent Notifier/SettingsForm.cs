﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Security.Principal;
using System.Diagnostics;
using System.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace uTorrentNotifier
{
    public partial class SettingsForm : Form
    {
        private Config Config = new Config();
        private WebUIAPI utorrent;
        private Prowl prowl;
		private Growl growl;
        private Twitter twitter;
        private Boxcar boxcar;
        private int loginErrors = 25; //only show balloon tip every 25 attempts
        private oAuthTwitter oAuth;

        private Version _Version;

        public SettingsForm()
        {
            this._Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (this.Config.CheckForUpdates)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(this.CheckForUpdates);
            }

            InitializeComponent();

            this.utorrent = new WebUIAPI(this.Config);
            this.utorrent.TorrentAdded += new WebUIAPI.TorrentAddedEventHandler(utorrent_TorrentAdded);
            this.utorrent.DownloadComplete += new WebUIAPI.DownloadFinishedEventHandler(utorrent_DownloadComplete);
            this.utorrent.LoginError += new WebUIAPI.LoginErrorEventHandler(utorrent_LoginError);
            this.utorrent.Start();

            this.prowl = new Prowl(this.Config.Prowl);
            this.growl = new Growl(this.Config.Growl);
            this.twitter = new Twitter(this.Config.Twitter);
            this.boxcar = new Boxcar(this.Config.Boxcar);

            if (this.Config.Prowl.Enable)
            {
                this.prowl.ProwlError += new Prowl.ProwlErrorHandler(prowl_ProwlError);
            }

            this.btnBoxcarInvite.Enabled = this.Config.Boxcar.Enable;
        }

        void prowl_ProwlError(object sender, Exception e)
        {
        }

        void utorrent_LoginError(object sender, Exception e)
        {
            if (this.loginErrors >= 5)
            {
				if (!this.Config.Growl.Enable)
					this.systrayIcon.ShowBalloonTip(5000, "Login Error", e.Message, ToolTipIcon.Error);
				else
					growl.Add(GrowlNotificationType.Error, e.Message);

                this.loginErrors = 0;
            }
            else
            {
                this.loginErrors++;
            }
        }

        void utorrent_DownloadComplete(List<TorrentFile> finished)
        {
            if (this.Config.Notifications.DownloadComplete)
            {
                foreach (TorrentFile f in finished)
                {
                    if (this.Config.Prowl.Enable)
                    {
                        this.prowl.Add("Download Complete", f.Name);
                    }

					if (this.Config.Growl.Enable)
					{
						this.growl.Add(GrowlNotificationType.InfoComplete, f.Name);
					}

                    if (this.Config.Twitter.Enable)
                    {
                        this.twitter.Update("Downloaded " + f.Name);
                    }

                    if (this.Config.Boxcar.Enable)
                    {
                        this.boxcar.Add("Download Complete: " + f.Name);
                    }

                    if (this.Config.ShowBalloonTips)
                    {
                        this.systrayIcon.ShowBalloonTip(5000, "Download Complete", f.Name, ToolTipIcon.Info);
                    }
                }
            }
        }

        void utorrent_TorrentAdded(List<TorrentFile> added)
        {
            if (this.Config.Notifications.TorrentAdded)
            {
                foreach (TorrentFile f in added)
                {
                    if (this.Config.Prowl.Enable)
                    {
                        this.prowl.Add("Torrent Added", f.Name);
                    }

					if (this.Config.Growl.Enable)
					{
						this.growl.Add(GrowlNotificationType.InfoAdded, f.Name);
					}

                    if (this.Config.Twitter.Enable)
                    {
                        this.twitter.Update("Added " + f.Name + " | " + Utilities.FormatBytes((long)f.Size));
                    }

                    if (this.Config.Boxcar.Enable)
                    {
                        this.boxcar.Add("Torrent Added: " + f.Name);
                    }

                    if (this.Config.ShowBalloonTips)
                    {
                        this.systrayIcon.ShowBalloonTip(5000, "Torrent Added", f.Name, ToolTipIcon.Info);
                    }
                }
            }
        }

        private void BackToSystray()
        {
            this.Hide();
        }

        private void RestoreFromSystray()
        {
            this.Show();
        }

        private void Settings_Shown(object sender, System.EventArgs e)
        {
            if (Properties.Settings.Default.FirstRun)
            {
                this.RestoreFromSystray();
                Properties.Settings.Default.Upgrade();
                this.Config = new Config();
                Properties.Settings.Default.FirstRun = false;
            }
            else
            {
                this.BackToSystray();
            }
            this.lblVersion.Text = this._Version.ToString();
            this.SetConfig();
        }

        private void SetConfig()
        {
            this.tbPassword.Text                                = this.Config.Password;
            this.tbUsername.Text                                = this.Config.Username;
            this.tbWebUI_URL.Text                               = this.Config.URI;
            this.cbRunOnStartup.Checked                         = this.Config.RunOnStartup;
            this.cbShowBalloonTips.Checked                      = this.Config.ShowBalloonTips;
            this.cbCheckForUpdates.Checked                      = this.Config.CheckForUpdates;

            this.tbProwlAPIKey.Text                             = this.Config.Prowl.APIKey;
            this.cbProwlEnable.Checked                          = this.Config.Prowl.Enable;

			this.cbGrowlEnable.Checked							= this.Config.Growl.Enable;
			this.tbGrowlPassword.Text							= this.Config.Growl.Password;
			this.tbGrowlHost.Text								= this.Config.Growl.Host;
			this.tbGrowlPort.Text								= this.Config.Growl.Port;

            this.tbTwitterPIN.Text                              = this.Config.Twitter.PIN;
            this.cbTwitterEnable.Checked                        = this.Config.Twitter.Enable;

            this.cbBoxcarEnable.Checked                         = this.Config.Boxcar.Enable;
            this.tbBoxcarEmail.Text                             = this.Config.Boxcar.Email;

            this.cbProwlNotification_TorentAdded.Checked        = this.Config.Notifications.TorrentAdded;
            this.cbTorrentNotification_DownloadComplete.Checked = this.Config.Notifications.DownloadComplete;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            this.Config.URI                                 = this.tbWebUI_URL.Text;
            this.Config.Username                            = this.tbUsername.Text;
            this.Config.Password                            = this.tbPassword.Text;
            this.Config.RunOnStartup                        = this.cbRunOnStartup.Checked;
            this.Config.ShowBalloonTips                     = this.cbShowBalloonTips.Checked;
            this.Config.CheckForUpdates                     = this.cbCheckForUpdates.Checked;

            this.Config.Prowl.APIKey                        = this.tbProwlAPIKey.Text;
            this.Config.Prowl.Enable                        = this.cbProwlEnable.Checked;

			this.Config.Growl.Enable						= this.cbGrowlEnable.Checked;
			this.Config.Growl.Password						= this.tbGrowlPassword.Text;
			this.Config.Growl.Host							= this.tbGrowlHost.Text;
			this.Config.Growl.Port							= this.tbGrowlPort.Text;

            this.Config.Twitter.Enable                      = this.cbTwitterEnable.Checked;

            this.Config.Boxcar.Enable                       = this.cbBoxcarEnable.Checked;
            this.Config.Boxcar.Email                        = this.tbBoxcarEmail.Text;

            this.Config.Notifications.TorrentAdded          = this.cbProwlNotification_TorentAdded.Checked;
            this.Config.Notifications.DownloadComplete      = this.cbTorrentNotification_DownloadComplete.Checked;

            this.Config.Save();
            this.BackToSystray();

            if (this.utorrent.Stopped)
                this.utorrent.Start();
        }

        private void btnBoxcarInvite_Click(object sender, EventArgs e)
        {
            this.Config.Boxcar.Email = this.tbBoxcarEmail.Text;
            this.boxcar.SendInvite();
        }

        private void cbBoxcarEnable_CheckedChanged(object sender, EventArgs e)
        {
            if (this.cbBoxcarEnable.Checked)
                this.btnBoxcarInvite.Enabled = true;
            else
                this.btnBoxcarInvite.Enabled = false;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.BackToSystray();
        }

        private void tsmiClose_Click(object sender, EventArgs e)
        {
            this.Dispose();
        }

        private void tsmiSettings_Click(object sender, EventArgs e)
        {
            this.RestoreFromSystray();
        }

        private void systrayIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.RestoreFromSystray();
            }
        }

        private void tsmiPauseAll_Click(object sender, EventArgs e)
        {
            this.utorrent.PauseAll();
        }

        private void tsmiStartAll_Click(object sender, EventArgs e)
        {
            this.utorrent.StartAll();
        }

        private void CheckForUpdates(object sender)
        {
            System.Net.WebClient webclient = new System.Net.WebClient();
            string latestVersion = webclient.DownloadString(Config.LatestVersion);

            string[] components = latestVersion.Split(".".ToCharArray());
            if (components.Length < 3)
            {
                components[2] = "0";
            }

            if(Int32.Parse(components[0]) > this._Version.Major || 
                (Int32.Parse(components[0]) == this._Version.Major && Int32.Parse(components[1]) > this._Version.Minor) ||
                (Int32.Parse(components[0]) == this._Version.Major && Int32.Parse(components[1]) == this._Version.Minor && Int32.Parse(components[2]) > this._Version.Build ))
            {
                if (MessageBox.Show("You are using version " + this._Version.ToString() + ". Would you like to download version " + latestVersion + "?",
                    "New Version Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Process.Start(Config.LatestDownload);
                }
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://ejholmes.github.com/uTorrent-Notifier/");
        }

        private void btnStartAuthorization_Click(object sender, EventArgs e)
        {
            oAuth = new oAuthTwitter();
            oAuth.ConsumerKey = this.Config.Twitter.ConsumerKey;
            oAuth.ConsumerSecret = this.Config.Twitter.ConsumerSecret;

            this.Config.Twitter.PIN = String.Empty;
            this.Config.Twitter.Token = String.Empty;
            this.Config.Twitter.TokenSecret = String.Empty;

            string oAuthLink = oAuth.AuthorizationLinkGet();
            try
            {
                Process.Start(oAuthLink);
                tbTwitterPIN.Text = String.Empty;
                tbTwitterPIN.Enabled = true;
                lblTwitterPIN.Enabled = true;
                btnTwitterAuthorize.Enabled = true;
            }
            catch
            {
                tbTwitterPIN.Text = String.Empty;
                lblTwitterPIN.Enabled = false;
                tbTwitterPIN.Enabled = false;
                btnTwitterAuthorize.Enabled = false;
                MessageBox.Show("An error occurred trying to authenticate. Check your network settings and browser config, and try again.", "uTorrent Notifier - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnTwitterAuthorize_Click(object sender, EventArgs e)
        {
            string PIN = tbTwitterPIN.Text;
            if (!string.IsNullOrEmpty(PIN))
            {
                PIN = PIN.Trim();
            }
            else
            {
                MessageBox.Show("Copy and paste the authentication PIN code from Twitter", "uTorrent Notifier - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                oAuth.AccessTokenGet(oAuth.OAuthToken, PIN);
                this.Config.Twitter.Token = oAuth.Token;
                this.Config.Twitter.TokenSecret = oAuth.TokenSecret;
                this.Config.Twitter.PIN = PIN;
                tbTwitterPIN.Enabled = false;
                btnTwitterAuthorize.Enabled = false;
                MessageBox.Show("Successfully authenticated with Twitter", "uTorrent Notifier - Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                this.Config.Twitter.PIN = String.Empty;
                tbTwitterPIN.Text = String.Empty;
                lblTwitterPIN.Enabled = false;
                tbTwitterPIN.Enabled = false;
                btnTwitterAuthorize.Enabled = false;
                MessageBox.Show("An error occurred trying to authenticate. Check your network settings and browser config, and try again.", "uTorrent Notifier - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSendTestTweet_Click(object sender, EventArgs e)
        {
            bool bCanSend=true;
            if(this.twitter==null) bCanSend=false;
            if(!this.cbTwitterEnable.Checked) bCanSend=false;

            if(bCanSend)
            {
                this.twitter.Update("Congratulations! Twitter notifications from uTorrent Notifier are working correctly :)");
                MessageBox.Show("A test tweet has been sent...", "uTorrent Notifier - Test sent", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("uTorrent Notifier must be authenticated and enabled before testing. Please check your settings, and try again.", "uTorrent Notifier - Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
