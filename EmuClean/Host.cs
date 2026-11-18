using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using EmuClean.Engine;

namespace EmuClean
{
    /// <summary>
    /// Frameless WebView2 host: loads ui/index.html and bridges
    /// window.chrome.webview.postMessage(...) to the Engine.Api class.
    /// </summary>
    public class HostForm : Form
    {
        private WebView2 _web;
        private Api _api;
        private Timer _fade;
        private const int W = 1120, H = 760;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public HostForm()
        {
            Text = "EmuClean — Emulator Debloater & Optimizer";
            _api = new Api();
            ClientSize = new Size(W, H);
            MinimumSize = new Size(940, 620);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(10, 14, 26);
            DoubleBuffered = true;

            _web = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.FromArgb(10, 14, 26),
            };
            Controls.Add(_web);

            Load += async (s, e) => await InitAsync();
            Shown += (s, e) => StartFade();
        }

        private async Task InitAsync()
        {
            try
            {
                await _web.EnsureCoreWebView2Async(null);
                var st = _web.CoreWebView2.Settings;
                st.AreDefaultContextMenusEnabled = false;
                st.AreDevToolsEnabled = false;
                st.IsStatusBarEnabled = false;
                st.IsZoomControlEnabled = false;
                st.IsBuiltInErrorPageEnabled = true;
                _web.CoreWebView2.WebMessageReceived += OnWebMessage;
                string ui = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "ui", "index.html");
                _web.CoreWebView2.Navigate(new Uri(ui).AbsoluteUri);
            }
            catch (WebView2RuntimeNotFoundException)
            {
                MessageBox.Show(
                    "Microsoft Edge WebView2 Runtime is required but was not found.\n" +
                    "Install it from: https://developer.microsoft.com/microsoft-edge/webview2/",
                    "EmuClean", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup error: " + ex.Message, "EmuClean",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private void StartFade()
        {
            Opacity = 0;
            _fade = new Timer { Interval = 12 };
            int step = 0;
            _fade.Tick += (s, e) =>
            {
                step++;
                Opacity = Math.Min(1.0, step / 20.0);
                if (Opacity >= 1.0) _fade.Stop();
            };
            _fade.Start();
        }

        // ---- bridge: JS postMessage -> Api -> JS promise resolver ----------
        private void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            JObject msg;
            try { msg = JObject.Parse(e.WebMessageAsJson); }
            catch { return; }

            // drag support from the HTML titlebar
            var type = (string)msg["type"];
            if (type == "drag")
            {
                BeginInvoke((Action)(() =>
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                }));
                return;
            }

            var id = (int)msg["id"];
            var method = (string)msg["method"];
            var args = msg["args"] as JArray ?? new JArray();

            Task.Run(() =>
            {
                object result;
                bool ok = true;
                string error = null;
                try
                {
                    result = _api.Invoke(method, args, this);
                }
                catch (Exception ex)
                {
                    ok = false;
                    result = null;
                    error = ex.Message;
                }
                var payload = new JObject
                {
                    ["__emu"] = 2,
                    ["id"] = id,
                    ["ok"] = ok,
                    ["data"] = ok ? JToken.FromObject(result ?? new object()) :
                                    (result == null ? JValue.CreateNull() : JToken.FromObject(result)),
                    ["error"] = error
                };
                var json = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
                {
                    StringEscapeHandling = StringEscapeHandling.EscapeNonAscii
                });
                try
                {
                    _web.CoreWebView2.PostWebMessageAsJson(json);
                }
                catch { }
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // quick fade-out, then close for real
            if (_fade != null) _fade.Stop();
            if (Opacity > 0.05)
            {
                e.Cancel = true;
                var t = new Timer { Interval = 10 };
                t.Tick += (s, a) =>
                {
                    Opacity -= 0.12;
                    if (Opacity <= 0.02)
                    {
                        t.Stop();
                        Close();
                    }
                };
                t.Start();
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
