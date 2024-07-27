using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Threading;


namespace SpellChecker
{
    public partial class Form1 : Form
    {
        private static bool notWorking = false;
        private int blinkCount = 0;
        private const int MaxBlinkCount = 6;
        
        private static readonly string base_url = "https://m.search.naver.com/p/csearch/ocontent/util/SpellerProxy";
        private static readonly HttpClient httpClient = new HttpClient();
        
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        private const int MY_HOTKEY_ID = 1;
        
        public Form1()
        {
            InitializeComponent();
        }
        
        private static async Task<string> ReadTokenAsync()
        {
            using (StreamReader sr = new StreamReader("token.txt"))
            {
                return await sr.ReadToEndAsync();
            }
        }

        private static async Task<string> UpdateTokenAsync()
        {
            var response = await httpClient.GetAsync("https://search.naver.com/search.naver?where=nexearch&sm=top_hty&fbm=1&ie=utf8&query=맞춤법검사기");
            var html = await response.Content.ReadAsStringAsync();

            var match = Regex.Match(html, "passportKey=([a-zA-Z0-9]+)");

            if (match.Success)
            {
                string token = Uri.UnescapeDataString(match.Groups[1].Value);
                using (StreamWriter sw = new StreamWriter("token.txt"))
                {
                    await sw.WriteAsync(token);
                }
                return token;
            }
            return null;
        }

        private static string RemoveTags(string text)
        {
            text = $"<content>{text}</content>".Replace("<br>", "\n");
            XDocument xDoc = XDocument.Parse(text);
            return xDoc.Root.Value;
        }

        private static async Task<string> GetDataAsync(string text, string token)
        {
            var requestUri = $"{base_url}?q={Uri.EscapeDataString(text)}&color_blindness=0&passportKey={token}";
            var response = await httpClient.GetAsync(requestUri);
            var json = await response.Content.ReadAsStringAsync();

            return json; 
        }

        private static async Task<string> GetSpellCheckData(string text)
        {
            string token = await ReadTokenAsync();
            string data = await GetDataAsync(text, token);
            
            if (data.Contains("\"error\""))
            {
                token = await UpdateTokenAsync();
                data = await GetDataAsync(text, token);
                if (data.Contains("\"error\""))
                {
                    return "맞춤법 검사가 불가능합니다.";
                }
            }
            
            var match = Regex.Match(data, "\"html\":\"(.*?)\"");
            if (match.Success)
            {
                string html = match.Groups[1].Value;
                return RemoveTags(html);
            }
            return "Error";
        }

        
        private void Form1_Load(object sender, EventArgs e)
        {
            
            if (!RegisterHotKey(this.Handle, MY_HOTKEY_ID, 0x0002, 0x44))
            {
                MessageBox.Show("단축키를 등록할 수 없습니다!");
                Application.Exit();
            }
        }
        
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == 0x0312 && m.WParam.ToInt32() == MY_HOTKEY_ID)
            {
                if (!notWorking)
                {
                    HandleHotkeyPressedAsync();
                }
                
            }
        }
        private async void HandleHotkeyPressedAsync()
        {
            try
            {
                SendKeys.Send("^c");
                Thread.Sleep(100);
                var copiedText = Clipboard.GetText();
                string result = await GetSpellCheckData(copiedText);
                if (result == "Error")
                {
                    MessageBox.Show("오류가 발생했습니다! 다시 시도해주세요. 400자 이상은 불가합니다.");
                }
                else
                {
                    Clipboard.SetText(result);
                    System.Media.SystemSounds.Hand.Play();
                    blinkCount = 0;
                    timer1.Start();
                    Thread.Sleep(100);
                    SendKeys.Send("^v");

                }

            }
            catch (Exception e)
            {
                MessageBox.Show("오류가 발생했습니다! 다시 시도해주세요." + e.Message);
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            UnregisterHotKey(Handle, MY_HOTKEY_ID);
            base.OnClosed(e);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                notWorking = true;
                label1.Text = "작동이 중지되었습니다.";
            }
            else
            {
                notWorking = false;
                label1.Text = "단축키 입력(ctrl + d) 대기중입니다.";
            }
            
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (blinkCount < MaxBlinkCount)
            {
                label1.Text = "맞춤법 수정이 완료되었습니다.";
                checkBox1.Enabled = false;
                // 색상 변경
                if (label1.ForeColor == Color.Aqua)
                {
                    label1.ForeColor = Color.White;
                }
                else
                {
                    label1.ForeColor = Color.Aqua;
                }

                blinkCount++;
            }
            else
            {
                label1.ForeColor = Color.White;
                label1.Text = "단축키 입력(ctrl + d) 대기중입니다.";
                checkBox1.Enabled = true;
                timer1.Stop();
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}