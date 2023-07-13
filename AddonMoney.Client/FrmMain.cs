﻿using AddonMoney.Client.Services;
using AddonMoney.Data.API;
using ChromeDriverLibrary;
using Serilog;
using System.Text.RegularExpressions;

namespace AddonMoney.Client
{
    public partial class FrmMain : Form
    {
        public static int Timeout { get; private set; } = 30;
        public static int TimeSleep { get; private set; } = 30;
        public static string ReferLinkRoot { get; private set; } = string.Empty;
        public static string ReferLinkFirst { get; set; } = string.Empty;
        public static string ReferLinkSecond { get; set; } = string.Empty;

        private CancellationTokenSource CancellationToken = null!;
        private List<AddonMoneyService> _services = new();
        private string _proxyPrefix = "http://";

        public FrmMain()
        {
            InitializeComponent();
            VPSNameTextBox.Text = HostService.GetHostName();
            ProfilesTextBox.Lines = HostService.ReadUserDataDirs();
            ProxyTypeComboBox.SelectedIndex = 0;
            ReferLinkRoot = HostService.GetRefLink();
            ReferLinkTxtBox.Text = HostService.GetRefLink();
            ActiveControl = kryptonLabel5;
        }

        private async void StartBtn_Click(object sender, EventArgs e)
        {
            ActiveControl = kryptonLabel5;
            try
            {
                var lines = new HashSet<string>();
                _services.Clear();
                foreach (var line in ProfilesTextBox.Lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    lines.Add(line);
                    _services.Add(new AddonMoneyService(line, _proxyPrefix));
                }
                _services = _services.DistinctBy(s => s.ProfileInfo.ProfilePath).ToList();
                ProfilesTextBox.Lines = lines.ToArray();
                HostService.WriteUserDataDirs(lines.ToArray());

                CancellationToken = new();
                EnableBtn(false);
                var matchRef = Regex.Match(ReferLinkRoot, "(https://addon.money/p/\\d{1,})");
                if (!matchRef.Success || matchRef.Value != ReferLinkRoot) 
                {
                    Invoke(() =>
                    {
                        MessageBox.Show(this, "Vui lòng kiểm tra lại referal link", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    });
                    return;
                }
                HostService.SaveRefLink(ReferLinkRoot);
                if (!_services.Any()) return;

                #region Always Check To Enable Extension
                //var nextScan = DateTime.UtcNow;
                //while (!CancellationToken.IsCancellationRequested)
                //{
                //    var needToScan = nextScan <= DateTime.UtcNow;
                //    if (needToScan)
                //    {
                //        nextScan = DateTime.UtcNow.AddMinutes(TimeSleep);
                //        _services.ForEach(async sv => await sv.Close().ConfigureAwait(false));
                //        ChromeDriverInstance.KillAllChromes();
                //    }

                //    var now = GetGMT7Now();
                //    var start = now.Hour >= 4 && now.Hour < 16 ? 0 : _services.Count / 2;
                //    var end = now.Hour >= 4 && now.Hour < 16 ? _services.Count / 2 : _services.Count;

                //    for (int i = 0; i < _services.Count; i++)
                //    {
                //        if (!(start <= i && i < end)) await _services[i].Close().ConfigureAwait(false);
                //        await Task.Delay(1000, CancellationToken.Token).ConfigureAwait(false);
                //    }
                //    for (int i = 0; i < _services.Count; i++)
                //    {
                //        if (start <= i && i < end) await Run(needToScan, _services[i]).ConfigureAwait(false);
                //        await Task.Delay(3000, CancellationToken.Token).ConfigureAwait(false);
                //    }
                //    await Task.Delay(5000, CancellationToken.Token).ConfigureAwait(false);
                //}
                #endregion

                #region Check And Enable Extension Each 15 Minutes
                var nextScan = DateTime.UtcNow;
                var nextEnable = DateTime.UtcNow;

                while (!CancellationToken.IsCancellationRequested)
                {
                    ReferLinkFirst = string.Empty;
                    ReferLinkSecond = string.Empty;

                    var needToScan = nextScan <= DateTime.UtcNow;
                    var needEnable = nextEnable <= DateTime.UtcNow;
                    if (needToScan || needEnable)
                    {
                        nextEnable = DateTime.UtcNow.AddMinutes(15);
                        if (needToScan)
                        {
                            nextScan = DateTime.UtcNow.AddMinutes(TimeSleep);
                            _services.ForEach(async sv => await sv.Close().ConfigureAwait(false));
                            ChromeDriverInstance.KillAllChromes();
                        }

                        var now = GetGMT7Now();
                        var start = now.Hour >= 4 && now.Hour < 16 ? 0 : _services.Count / 2;
                        var end = now.Hour >= 4 && now.Hour < 16 ? _services.Count / 2 : _services.Count;

                        for (int i = 0; i < _services.Count; i++)
                        {
                            if (!(start <= i && i < end)) await _services[i].Close();
                            await Task.Delay(1000, CancellationToken.Token).ConfigureAwait(false);
                        }
                        for (int i = 0; i < _services.Count; i++)
                        {
                            if (start <= i && i < end) await Run(needToScan, _services[i]).ConfigureAwait(false);
                            await Task.Delay(1000, CancellationToken.Token).ConfigureAwait(false);
                        }
                    }
                    await Task.Delay(10000, CancellationToken.Token).ConfigureAwait(false);
                }
                #endregion
            }
            catch (Exception ex)
            {
                Invoke(() =>
                {
                    if (CancellationToken.IsCancellationRequested) MessageBox.Show(this, "Bạn đã dừng chương trình", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else MessageBox.Show(this, $"Có lỗi xảy ra: {ex.Message}", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
            finally
            {
                EnableBtn(true);
                Invoke(() =>
                {
                    MessageBox.Show(this, "Chương trình đã dừng lại", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
        }

        private async Task Run(bool needToScan, AddonMoneyService service)
        {
            try
            {
                var account = await Task.Run(() => service.ScanInfo(needToScan, CancellationToken.Token)).ConfigureAwait(false);
                if (account.Success)
                {
                    var balanceRq = new UpdateBalanceRequest
                    {
                        Id = account.Id,
                        Balance = account.Balance,
                        Name = account.Name,
                        TodayEarn = account.TodayEarn,
                        Profile = account.Profile,
                        VPS = HostService.GetHostName()
                    };
                    await ApiService.SendBalance(balanceRq).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Got exception when running scan service for {service.Profile}.", ex);
            }
        }

        private void StopBtn_Click(object sender, EventArgs e)
        {
            ActiveControl = kryptonLabel5;
            CancellationToken.Cancel();
            ChromeDriverInstance.KillAllChromes();
        }

        private void EnableBtn(bool enable)
        {
            Invoke(() =>
            {
                StartBtn.Enabled = enable;
                ProxyTypeComboBox.Enabled = enable;
                StopBtn.Enabled = !enable;

                ProfilesTextBox.ReadOnly = !enable;
                ReferLinkTxtBox.ReadOnly = !enable;
                SleepTimeUpDown.Enabled = enable;

                RunStatusTextBox.Text = enable ? "Đã dừng" : "Đang chạy";
                RunStatusTextBox.StateCommon.Back.Color1 = enable ? Color.FromArgb(255, 128, 128) : Color.GreenYellow;
            });
        }

        private void TimeoutUpDown_ValueChanged(object sender, EventArgs e)
        {
            Timeout = (int)TimeoutUpDown.Value;
        }

        private void ProfilesTextBox_TextChanged(object sender, EventArgs e)
        {
            ProfileCountTextBox.Text = ProfilesTextBox.Lines.Count(line => !string.IsNullOrWhiteSpace(line)).ToString();
        }

        public static DateTime GetGMT7Now()
        {
            DateTime utcNow = DateTime.UtcNow;
            TimeZoneInfo gmt7TimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, gmt7TimeZone);
        }

        private void SleepTimeUpDown_ValueChanged(object sender, EventArgs e)
        {
            TimeSleep = (int)SleepTimeUpDown.Value;
        }

        private void TopMostCheckBtn_CheckedChanged(object sender, EventArgs e)
        {
            TopMost = TopMostCheckBtn.Checked;
        }

        private void VPSNameTextBox_TextChanged(object sender, EventArgs e)
        {
            HostService.SetHost(VPSNameTextBox.Text);
        }

        private void ProxyTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ProxyTypeComboBox.SelectedIndex == 1)
            {
                _proxyPrefix = "socks5://";
            }
            else
            {
                _proxyPrefix = "http://";
            }
        }

        private void ReferLinkTxtBox_TextChanged(object sender, EventArgs e)
        {
            ReferLinkRoot = ReferLinkTxtBox.Text.Trim();
        }
    }
}
