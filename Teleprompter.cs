// Teleprompter liviano para Windows. Solo usa .NET Framework (incluido en Windows).
// Se compila automaticamente con "Abrir Teleprompter.bat" la primera vez.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TeleprompterApp
{
    static class Native
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint p);
        [DllImport("dwmapi.dll")] public static extern int DwmFlush();
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int idx);
        [StructLayout(LayoutKind.Sequential)]
        public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int x; public int y; }
        [DllImport("user32.dll")] public static extern bool PeekMessage(out MSG msg, IntPtr hWnd, uint min, uint max, uint remove);

        public const int WM_NCHITTEST = 0x84, WM_NCLBUTTONDBLCLK = 0xA3;
        public const int HTTRANSPARENT = -1, HTCAPTION = 2, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12,
            HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        public static bool AppIdle() { MSG m; return !PeekMessage(out m, IntPtr.Zero, 0, 0, 0); }
    }

    // ---------------------------------------------------------------- ajustes
    class Settings
    {
        public const string DefaultText =
            "Pegá o escribí acá tu texto.\r\n\r\n" +
            "Cuando termines, tocá \"Listo\" (o Ctrl+Enter) para pasar al modo lectura.\r\n\r\n" +
            "Espacio = play / pausa. Flechas arriba y abajo = velocidad.";
        public string Text = DefaultText;
        public int Speed = 20;
        public float FontSize = 34f;
        public Rectangle Bounds = Rectangle.Empty;

        public static Settings Load(string path)
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(path)) return s;
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i), v = line.Substring(i + 1);
                    if (k == "speed") s.Speed = int.Parse(v, CultureInfo.InvariantCulture);
                    else if (k == "font") s.FontSize = float.Parse(v, CultureInfo.InvariantCulture);
                    else if (k == "text") s.Text = Encoding.UTF8.GetString(Convert.FromBase64String(v));
                    else if (k == "bounds")
                    {
                        string[] p = v.Split(',');
                        s.Bounds = new Rectangle(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]));
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save(string path)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("speed=" + Speed.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("font=" + FontSize.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("bounds=" + Bounds.X + "," + Bounds.Y + "," + Bounds.Width + "," + Bounds.Height);
                sb.AppendLine("text=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Text ?? "")));
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }

    // Paneles que dejan "pasar" el clic a la ventana: asi se puede arrastrar desde el fondo.
    class PassThroughPanel : Panel
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_NCHITTEST) { m.Result = (IntPtr)Native.HTTRANSPARENT; return; }
            base.WndProc(ref m);
        }
    }

    // ---------------------------------------------------------------- boton discreto (no toma foco)
    class BarButton : Label
    {
        public bool Hot;
        bool over;
        public BarButton(string text, Font f)
        {
            Text = text; Font = f; AutoSize = false; TextAlign = ContentAlignment.MiddleCenter;
            Cursor = Cursors.Hand; BackColor = Color.Transparent; UpdateColors();
        }
        public void UpdateColors()
        {
            ForeColor = over ? Color.White : (Hot ? Color.FromArgb(225, 225, 230) : Color.FromArgb(105, 105, 112));
            BackColor = over ? Color.FromArgb(60, 60, 68) : Color.Transparent;
        }
        protected override void OnMouseEnter(EventArgs e) { over = true; UpdateColors(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { over = false; UpdateColors(); base.OnMouseLeave(e); }
    }

    // ---------------------------------------------------------------- slider de velocidad
    class SpeedSlider : Control
    {
        public int Minimum = 1, Maximum = 100;
        int val = 20;
        public bool Hot;
        public event EventHandler ValueChanged;
        public SpeedSlider()
        {
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent; Cursor = Cursors.Hand;
        }
        public int Value
        {
            get { return val; }
            set
            {
                int v = Math.Max(Minimum, Math.Min(Maximum, value));
                if (v == val) return;
                val = v; Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }
        float Pad { get { return Height / 2f; } }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float cy = Height / 2f, x0 = Pad, x1 = Width - Pad;
            float t = (val - Minimum) / (float)(Maximum - Minimum);
            float kx = x0 + t * (x1 - x0);
            float th = Math.Max(2f, Height / 9f);
            Color dim = Hot ? Color.FromArgb(90, 90, 98) : Color.FromArgb(55, 55, 60);
            Color acc = Hot ? Color.FromArgb(255, 190, 70) : Color.FromArgb(130, 110, 70);
            using (SolidBrush b = new SolidBrush(dim)) g.FillRectangle(b, x0, cy - th / 2, x1 - x0, th);
            using (SolidBrush b = new SolidBrush(acc)) g.FillRectangle(b, x0, cy - th / 2, kx - x0, th);
            float r = Height * 0.27f;
            using (SolidBrush b = new SolidBrush(Hot ? Color.White : Color.FromArgb(150, 150, 155))) g.FillEllipse(b, kx - r, cy - r, 2 * r, 2 * r);
        }
        void SetFromX(int x)
        {
            float t = (x - Pad) / Math.Max(1f, Width - 2 * Pad);
            Value = (int)Math.Round(Minimum + Math.Max(0f, Math.Min(1f, t)) * (Maximum - Minimum));
        }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Capture = true; SetFromX(e.X); } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (e.Button == MouseButtons.Left) SetFromX(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }
    }

    // ---------------------------------------------------------------- vista de lectura
    class ReaderView : Control
    {
        class TLine { public string T; public float X; public string Bullet; public float BX; }
        List<TLine> lines = new List<TLine>();
        string text = "";
        Font font;
        float lineH = 40f, marginX = 24f;
        public float Offset;   // pixeles recorridos
        public float MaxOffset { get { return Math.Max(0f, (lines.Count - 1) * lineH); } }
        public float GuideY { get { return Height / 3f; } }

        public ReaderView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_NCHITTEST) { m.Result = (IntPtr)Native.HTTRANSPARENT; return; }
            base.WndProc(ref m);
        }
        public void SetContent(string t, Font f, float scale)
        {
            float frac = MaxOffset > 0 ? Offset / MaxOffset : 0f;
            text = (t ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            if (font != null && font != f) font.Dispose();
            font = f; marginX = 22f * scale;
            Relayout();
            Offset = frac * MaxOffset;
            Invalidate();
        }
        protected override void OnResize(EventArgs e)
        {
            if (font != null)
            {
                float frac = MaxOffset > 0 ? Offset / MaxOffset : 0f;
                Relayout(); Offset = frac * MaxOffset;
            }
            base.OnResize(e);
        }
        void Relayout()
        {
            lines.Clear();
            if (font == null || Width <= 0) return;
            using (Graphics g = CreateGraphics())
            {
                StringFormat sf = StringFormat.GenericTypographic;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                lineH = font.GetHeight(g) * 1.28f;
                float em = g.MeasureString("M", font, PointF.Empty, sf).Width;
                float bulletW = em * 1.1f;   // espacio entre la viñeta y el texto
                foreach (string raw in text.Split('\n'))
                {
                    // sangria: espacios/tabs al principio de la linea (tab = 4 espacios)
                    int sp = 0, i = 0;
                    while (i < raw.Length && (raw[i] == ' ' || raw[i] == '\t')) { sp += raw[i] == '\t' ? 4 : 1; i++; }
                    string para = raw.Substring(i).TrimEnd();
                    int level = (sp + 1) / 3;   // ~3 espacios por nivel
                    string bullet = null;
                    if (para.Length >= 2 && "*-•·–◦▪".IndexOf(para[0]) >= 0 && (para[1] == ' ' || para[1] == '\t'))
                    {
                        bullet = level == 0 ? "•" : "◦";
                        para = para.Substring(2).TrimStart();
                    }
                    float baseX = marginX + level * em * 1.5f;
                    float textX = bullet != null ? baseX + bulletW : baseX;
                    float maxW = Math.Max(em * 4, Width - marginX - textX);

                    string[] words = para.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length == 0)
                    {
                        if (bullet == null) { lines.Add(new TLine { T = "", X = textX }); continue; }
                        words = new string[] { "" };
                    }
                    bool firstLine = true;
                    string cur = "";
                    Action<string> emit = delegate(string s)
                    {
                        lines.Add(new TLine { T = s, X = textX, Bullet = firstLine ? bullet : null, BX = baseX });
                        firstLine = false;
                    };
                    foreach (string w0 in words)
                    {
                        string w = w0;
                        string cand = cur.Length == 0 ? w : cur + " " + w;
                        if (g.MeasureString(cand, font, PointF.Empty, sf).Width <= maxW) { cur = cand; continue; }
                        if (cur.Length > 0) { emit(cur); cur = ""; }
                        // palabra mas larga que el ancho: cortarla
                        while (g.MeasureString(w, font, PointF.Empty, sf).Width > maxW && w.Length > 1)
                        {
                            int n = w.Length - 1;
                            while (n > 1 && g.MeasureString(w.Substring(0, n), font, PointF.Empty, sf).Width > maxW) n--;
                            emit(w.Substring(0, n)); w = w.Substring(n);
                        }
                        cur = w;
                    }
                    if (cur.Length > 0 || firstLine) emit(cur);
                }
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.TextRenderingHint = TextRenderingHint.AntiAlias;   // permite posiciones sub-pixel = scroll suave
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float gy = GuideY;
            if (font != null && lines.Count > 0)
            {
                float top0 = gy - lineH / 2f - Offset;
                int first = Math.Max(0, (int)Math.Floor((-top0) / lineH) - 1);
                int last = Math.Min(lines.Count - 1, (int)Math.Ceiling((Height - top0) / lineH));
                float ascentPad = (lineH - font.GetHeight(g)) / 2f;
                using (SolidBrush bright = new SolidBrush(Color.White))
                using (SolidBrush faded = new SolidBrush(Color.FromArgb(120, 120, 125)))
                using (SolidBrush bullet = new SolidBrush(Color.FromArgb(255, 190, 70)))
                {
                    for (int i = first; i <= last; i++)
                    {
                        float y = top0 + i * lineH;
                        bool above = y + lineH < gy - lineH * 0.15f;
                        TLine ln = lines[i];
                        if (ln.Bullet != null)
                            g.DrawString(ln.Bullet, font, above ? faded : bullet, ln.BX, y + ascentPad, StringFormat.GenericTypographic);
                        g.DrawString(ln.T, font, above ? faded : bright, ln.X, y + ascentPad, StringFormat.GenericTypographic);
                    }
                }
            }
            // linea guia tenue en el tercio superior
            using (Pen p = new Pen(Color.FromArgb(55, 255, 255, 255), 1f)) g.DrawLine(p, marginX * 0.4f, gy, Width - marginX * 0.4f, gy);
            float s = Math.Max(5f, lineH * 0.14f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 255, 190, 70)))
                g.FillPolygon(b, new PointF[] { new PointF(2, gy - s), new PointF(2 + s * 1.2f, gy), new PointF(2, gy + s) });
        }
    }

    // ---------------------------------------------------------------- ventana principal
    class MainForm : Form
    {
        readonly Settings st;
        readonly string cfgPath;
        readonly float S;
        readonly int grip, corner;

        ReaderView reader;
        PassThroughPanel editWrap, bar;
        TextBox editor;
        BarButton bReset, bPlay, bSmaller, bBigger, bEdit, bDone, bClose;
        Label lSpeed, lHint;
        SpeedSlider slider;
        System.Windows.Forms.Timer timer;
        Stopwatch clock = Stopwatch.StartNew();
        double lastT, lastFrame, lastSave;
        bool playing, editMode = true, barHot, dirty;

        public SelfTest Test;   // solo para la prueba automatica

        public MainForm(Settings s, string cfg)
        {
            st = s; cfgPath = cfg;
            AutoScaleMode = AutoScaleMode.None;
            using (Graphics g = CreateGraphics()) S = g.DpiX / 96f;
            grip = (int)(7 * S); corner = (int)(16 * S);

            Text = "Teleprompter";
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            TopMost = true;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(16, 16, 19);
            Opacity = 0.93;
            KeyPreview = true;
            DoubleBuffered = true;
            MinimumSize = new Size((int)(380 * S), (int)(150 * S));
            Padding = new Padding(grip / 2);

            Font ui = new Font("Segoe UI", 10f);
            Font sym = new Font("Segoe UI Symbol", 11f);

            reader = new ReaderView { Dock = DockStyle.Fill, BackColor = BackColor, Visible = false };
            reader.MouseWheel += OnWheel;

            editWrap = new PassThroughPanel { Dock = DockStyle.Fill, Padding = new Padding((int)(12 * S)), BackColor = BackColor };
            editor = new TextBox
            {
                Multiline = true, AcceptsReturn = true, AcceptsTab = true, MaxLength = 0, ScrollBars = ScrollBars.None, Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(30, 30, 35), ForeColor = Color.White,
                Font = new Font("Segoe UI", 13f), Text = st.Text
            };
            editor.TextChanged += delegate { dirty = true; };
            editWrap.Controls.Add(editor);

            bar = new PassThroughPanel { Dock = DockStyle.Bottom, Height = (int)(38 * S), BackColor = BackColor };
            bReset = new BarButton("↺", sym);
            bPlay = new BarButton("▶", sym);
            bSmaller = new BarButton("A-", ui);
            bBigger = new BarButton("A+", ui);
            bEdit = new BarButton("Editar", ui);
            bDone = new BarButton("Listo", new Font("Segoe UI", 10f, FontStyle.Bold));
            bClose = new BarButton("✕", sym);
            lSpeed = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(105, 105, 112), BackColor = Color.Transparent };
            lHint = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(105, 105, 112), BackColor = Color.Transparent, Text = "Pegá tu texto  ·  Ctrl+Enter = Listo" };
            slider = new SpeedSlider { Value = st.Speed };
            slider.ValueChanged += delegate { st.Speed = slider.Value; UpdateSpeedLabel(); dirty = true; };

            bReset.Click += delegate { ResetScroll(); };
            bPlay.Click += delegate { TogglePlay(); };
            bSmaller.Click += delegate { ChangeFont(-3f); };
            bBigger.Click += delegate { ChangeFont(+3f); };
            bEdit.Click += delegate { EnterEdit(); };
            bDone.Click += delegate { EnterRead(); };
            bClose.Click += delegate { Close(); };
            ToolTip tt = new ToolTip();
            tt.SetToolTip(bReset, "Reiniciar (Inicio)"); tt.SetToolTip(bPlay, "Play / Pausa (Espacio)");
            tt.SetToolTip(bSmaller, "Letra más chica"); tt.SetToolTip(bBigger, "Letra más grande");
            tt.SetToolTip(slider, "Velocidad (flechas arriba/abajo)"); tt.SetToolTip(bClose, "Cerrar (guarda todo)");

            bar.Controls.AddRange(new Control[] { bReset, bPlay, slider, lSpeed, bSmaller, bBigger, bEdit, bDone, lHint, bClose });
            bar.Resize += delegate { LayoutBar(); };

            Controls.Add(reader);
            Controls.Add(editWrap);
            Controls.Add(bar);   // agregado al final => se acopla abajo primero

            // posicion / tamano
            Rectangle b = st.Bounds;
            bool visible = false;
            foreach (Screen sc in Screen.AllScreens) if (b.Width > 0 && sc.WorkingArea.IntersectsWith(b)) visible = true;
            if (!visible)
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                int w = (int)(620 * S), h = (int)(250 * S);
                b = new Rectangle(wa.Left + (wa.Width - w) / 2, wa.Top + (int)(8 * S), w, h);   // arriba al centro, bajo la camara
            }
            Bounds = b;

            UpdateSpeedLabel();
            ApplyFont();
            SetMode(true);

            timer = new System.Windows.Forms.Timer { Interval = 15 };
            timer.Tick += OnTimer;
            timer.Start();
            Application.Idle += OnIdle;
        }

        // ---------- arrastre y redimension nativos
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_NCLBUTTONDBLCLK) { m.Result = IntPtr.Zero; return; }   // sin maximizar con doble clic
            if (m.Msg == 0x0112)   // WM_SYSCOMMAND: ignorar minimizar / maximizar (solo se cierra con la X)
            {
                int cmd = m.WParam.ToInt32() & 0xFFF0;
                if (cmd == 0xF020 || cmd == 0xF030) { m.Result = IntPtr.Zero; return; }
            }
            if (m.Msg == Native.WM_NCHITTEST)
            {
                long lp = m.LParam.ToInt64();
                Point p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                int W = ClientSize.Width, H = ClientSize.Height;
                bool l = p.X < grip, r = p.X >= W - grip, t = p.Y < grip, bt = p.Y >= H - grip;
                bool lc = p.X < corner, rc = p.X >= W - corner, tc = p.Y < corner, bc = p.Y >= H - corner;
                int ht = Native.HTCAPTION;
                if ((t && lc) || (l && tc)) ht = Native.HTTOPLEFT;
                else if ((t && rc) || (r && tc)) ht = Native.HTTOPRIGHT;
                else if ((bt && lc) || (l && bc)) ht = Native.HTBOTTOMLEFT;
                else if ((bt && rc) || (r && bc)) ht = Native.HTBOTTOMRIGHT;
                else if (l) ht = Native.HTLEFT;
                else if (r) ht = Native.HTRIGHT;
                else if (t) ht = Native.HTTOP;
                else if (bt) ht = Native.HTBOTTOM;
                m.Result = (IntPtr)ht;
                return;
            }
            base.WndProc(ref m);
        }

        // ---------- barra de controles
        void LayoutBar()
        {
            int h = bar.Height, pad = (int)(6 * S), bh = h - 2 * pad, x = (int)(8 * S);
            int bw = (int)(34 * S);
            bClose.SetBounds(bar.Width - bw - (int)(8 * S), pad, bw, bh);
            if (editMode)
            {
                int dw = (int)(70 * S);
                bDone.SetBounds(bClose.Left - dw - pad, pad, dw, bh);
                lHint.SetBounds(x, pad, Math.Max(0, bDone.Left - x - pad), bh);
                return;
            }
            bReset.SetBounds(x, pad, bw, bh); x += bw + 2;
            bPlay.SetBounds(x, pad, bw, bh); x += bw + pad;
            int right = bClose.Left - pad;
            int ew = (int)(56 * S), aw = (int)(32 * S);
            bEdit.SetBounds(right - ew, pad, ew, bh); right -= ew + 2;
            bBigger.SetBounds(right - aw, pad, aw, bh); right -= aw + 2;
            bSmaller.SetBounds(right - aw, pad, aw, bh); right -= aw + pad;
            int lw = (int)(30 * S);
            lSpeed.SetBounds(right - lw, pad, lw, bh); right -= lw + 2;
            slider.SetBounds(x, pad + 2, Math.Max(40, right - x), bh - 4);
        }

        void SetBarHot(bool hot)
        {
            barHot = hot;
            bar.BackColor = hot ? Color.FromArgb(32, 32, 38) : BackColor;
            foreach (Control c in bar.Controls)
            {
                BarButton bb = c as BarButton;
                if (bb != null) { bb.Hot = hot; bb.UpdateColors(); }
            }
            slider.Hot = hot; slider.Invalidate();
            Color lc = hot ? Color.FromArgb(200, 200, 205) : Color.FromArgb(105, 105, 112);
            lSpeed.ForeColor = lc; lHint.ForeColor = lc;
        }

        void UpdateSpeedLabel() { lSpeed.Text = st.Speed.ToString(); }

        // ---------- modos
        void SetMode(bool edit)
        {
            editMode = edit;
            editWrap.Visible = edit; reader.Visible = !edit;
            bDone.Visible = edit; lHint.Visible = edit;
            bReset.Visible = bPlay.Visible = slider.Visible = lSpeed.Visible = bSmaller.Visible = bBigger.Visible = bEdit.Visible = !edit;
            LayoutBar();
        }
        public void EnterRead()
        {
            st.Text = editor.Text;
            reader.Offset = 0;
            ApplyFont();
            reader.Offset = 0;
            SetMode(false);
            SetPlaying(false);
            ActiveControl = null; Focus();
            SaveNow();
        }
        public void EnterEdit()
        {
            SetPlaying(false);
            SetMode(true);
            editor.Focus(); editor.SelectionStart = editor.TextLength;
        }

        void ApplyFont() { reader.SetContent(st.Text, new Font("Segoe UI", st.FontSize, FontStyle.Regular), S); }
        void ChangeFont(float d)
        {
            st.FontSize = Math.Max(14f, Math.Min(110f, st.FontSize + d));
            ApplyFont(); dirty = true;
        }

        public void SetPlaying(bool p)
        {
            if (p && reader.Offset >= reader.MaxOffset - 0.5f) reader.Offset = 0;   // si estaba al final, arranca de nuevo
            playing = p;
            bPlay.Text = p ? "❚❚" : "▶";
            lastT = clock.Elapsed.TotalSeconds;
        }
        public void TogglePlay() { SetPlaying(!playing); }
        public void ResetScroll() { reader.Offset = 0; lastT = clock.Elapsed.TotalSeconds; reader.Invalidate(); }
        public void SetSpeed(int v) { slider.Value = v; }

        // ---------- teclado
        protected override bool ProcessCmdKey(ref Message msg, Keys k)
        {
            if (HandleKey(k)) return true;
            return base.ProcessCmdKey(ref msg, k);
        }
        public bool HandleKey(Keys k)
        {
            if (editMode)
            {
                if (k == (Keys.Control | Keys.Enter)) { EnterRead(); return true; }
                if (k == (Keys.Control | Keys.A)) { editor.SelectAll(); return true; }
                return false;
            }
            switch (k)
            {
                case Keys.Space: TogglePlay(); return true;
                case Keys.Up: SetSpeed(st.Speed + 2); return true;
                case Keys.Down: SetSpeed(st.Speed - 2); return true;
                case Keys.Home: ResetScroll(); return true;
                case Keys.Control | Keys.Oemplus: case Keys.Control | Keys.Add: ChangeFont(+3f); return true;
                case Keys.Control | Keys.OemMinus: case Keys.Control | Keys.Subtract: ChangeFont(-3f); return true;
            }
            return false;
        }
        void OnWheel(object sender, MouseEventArgs e)
        {
            if (editMode) return;
            reader.Offset = Math.Max(0f, Math.Min(reader.MaxOffset, reader.Offset - e.Delta / 120f * 40f * S));
            reader.Invalidate();
        }
        protected override void OnMouseWheel(MouseEventArgs e) { OnWheel(this, e); base.OnMouseWheel(e); }

        // ---------- animacion
        public float PixelsPerSecond { get { return st.Speed * 1.6f * S; } }
        public float Offset { get { return reader.Offset; } }
        public bool Playing { get { return playing; } }
        public int Speed { get { return st.Speed; } }

        void Step()
        {
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.1, now - lastT);
            lastT = now;
            if (!playing || editMode) return;
            reader.Offset += (float)(PixelsPerSecond * dt);
            if (reader.Offset >= reader.MaxOffset) { reader.Offset = reader.MaxOffset; SetPlaying(false); }
            lastFrame = now;
            if (Test != null) Test.OnFrame(now, reader.Offset);
        }

        // Bucle sincronizado con el refresco de pantalla (DwmFlush) => desplazamiento fluido
        void OnIdle(object sender, EventArgs e)
        {
            while (playing && !editMode && Native.AppIdle() && !IsDisposed)
            {
                Step();
                reader.Refresh();
                if (Native.DwmFlush() != 0) System.Threading.Thread.Sleep(8);
            }
        }

        void OnTimer(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            // respaldo: mientras se arrastra/redimensiona la ventana el bucle Idle no corre
            if (playing && now - lastFrame > 0.03) { Step(); reader.Invalidate(); }
            bool hot = Bounds.Contains(Cursor.Position) && Cursor.Position.Y >= bar.PointToScreen(Point.Empty).Y - (int)(10 * S);
            if (hot != barHot) SetBarHot(hot);
            if (dirty && now - lastSave > 5) SaveNow();
            if (now - lastTopCheck > 0.3) { lastTopCheck = now; KeepOnTop(); }
            if (Test != null) Test.Tick(now);
        }

        public void SaveNow()
        {
            lastSave = clock.Elapsed.TotalSeconds; dirty = false;
            if (WindowState == FormWindowState.Normal) st.Bounds = Bounds;
            if (editMode) st.Text = editor.Text;
            st.Save(cfgPath);
        }
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        double lastTopCheck;
        // Si otra app (PowerPoint en pantalla completa, etc.) se pone encima, o la ventana se minimizo,
        // la volvemos a traer adelante sin robar el foco.
        public void KeepOnTop()
        {
            if (!IsHandleCreated || IsDisposed) return;
            if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
            if (!Visible) Show();
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040 | 0x0200);   // NOSIZE|NOMOVE|NOACTIVATE|SHOWWINDOW|NOOWNERZORDER
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized) BeginInvoke((MethodInvoker)delegate { WindowState = FormWindowState.Normal; });
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            TopMost = true;
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);   // NOSIZE|NOMOVE|NOACTIVATE
            Activate();
            editor.Focus(); editor.SelectionLength = 0; editor.SelectionStart = editor.TextLength;
        }
        protected override void OnResizeEnd(EventArgs e) { dirty = true; base.OnResizeEnd(e); }
        protected override void OnMove(EventArgs e) { dirty = true; base.OnMove(e); }
        protected override void OnFormClosing(FormClosingEventArgs e) { SaveNow(); base.OnFormClosing(e); }

        // acceso para la prueba automatica
        public TextBox Editor { get { return editor; } }
    }

    // ---------------------------------------------------------------- prueba automatica (--selftest)
    class SelfTest
    {
        readonly MainForm f; readonly string outDir;
        readonly StringBuilder log = new StringBuilder();
        readonly List<double> frameT = new List<double>();
        readonly List<float> frameO = new List<float>();
        double t0 = -1; int phase = 0;
        double phaseStart; float phaseStartOffset;

        public SelfTest(MainForm form, string dir) { f = form; outDir = dir; }
        public void OnFrame(double t, float off) { frameT.Add(t); frameO.Add(off); }

        void Shot(string name)
        {
            Rectangle r = f.Bounds;
            using (Bitmap bmp = new Bitmap(r.Width, r.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
                bmp.Save(Path.Combine(outDir, name), ImageFormat.Png);
            }
        }
        void Line(string s) { log.AppendLine(s); }

        void Measure(string label, double now)
        {
            double secs = now - phaseStart;
            float moved = f.Offset - phaseStartOffset;
            Line(string.Format(CultureInfo.InvariantCulture, "{0}: {1:F2}s, avanzó {2:F1}px => {3:F1}px/s (esperado {4:F1}px/s)",
                label, secs, moved, moved / secs, f.PixelsPerSecond));
        }
        void Begin(double now) { phaseStart = now; phaseStartOffset = f.Offset; }

        public void Tick(double now)
        {
            if (t0 < 0) t0 = now;
            double t = now - t0;
            switch (phase)
            {
                case 0:
                    if (t < 0.8) return;
                    Line("TopMost (siempre encima): " + f.TopMost + " | WS_EX_TOPMOST=" + ((Native.GetWindowLong(f.Handle, -20) & 0x8) != 0));
                    Line("Sin barra de titulo: " + (f.FormBorderStyle == FormBorderStyle.None) + " | Opacidad: " + f.Opacity);
                    Shot("1_edicion.png");
                    StringBuilder sb = new StringBuilder();
                    for (int i = 1; i <= 60; i++) sb.Append("Línea de prueba número " + i + ": hola, este es un texto de ejemplo para el teleprompter. ");
                    string custom = Path.Combine(outDir, "selftest_text.txt");
                    f.Editor.Text = File.Exists(custom) ? File.ReadAllText(custom, Encoding.UTF8) : sb.ToString();
                    Line("Caracteres cargados: " + f.Editor.TextLength);
                    f.HandleKey(Keys.Control | Keys.Enter);   // "Listo"
                    f.SetSpeed(30);
                    f.HandleKey(Keys.Space);                 // play
                    Begin(now); phase = 1; return;
                case 1:
                    if (now - phaseStart < 3) return;
                    Line("Playing tras Espacio: " + f.Playing);
                    Measure("Velocidad 30", now);
                    Shot("2_lectura.png");
                    for (int i = 0; i < 10; i++) f.HandleKey(Keys.Up);   // +20
                    Line("Tras 10x flecha arriba, velocidad = " + f.Speed);
                    Begin(now); frameT.Clear(); frameO.Clear(); phase = 2; return;
                case 2:
                    if (now - phaseStart < 3) return;
                    Measure("Velocidad 50 (en vivo)", now);
                    Shot("3_lectura.png");
                    ReportFrames();
                    f.HandleKey(Keys.Space);   // pausa
                    Line("Playing tras 2do Espacio: " + f.Playing);
                    Begin(now); phase = 3; return;
                case 3:
                    if (now - phaseStart < 1.5) return;
                    Line(string.Format(CultureInfo.InvariantCulture, "En pausa 1.5s, se movió {0:F2}px (debe ser 0)", f.Offset - phaseStartOffset));
                    for (int i = 0; i < 20; i++) f.HandleKey(Keys.Down);
                    f.HandleKey(Keys.Space);
                    Begin(now); phase = 4; return;
                case 4:
                    if (now - phaseStart < 2) return;
                    Measure("Tras 20x flecha abajo (velocidad " + f.Speed + ")", now);
                    f.ResetScroll();
                    Line(string.Format(CultureInfo.InvariantCulture, "Tras Reiniciar offset = {0:F1}", f.Offset));
                    f.HandleKey(Keys.Space);
                    phase = 5; phaseStart = now; return;
                case 5:
                    if (now - phaseStart < 0.5) return;
                    File.WriteAllText(Path.Combine(outDir, "selftest.txt"), log.ToString(), Encoding.UTF8);
                    phase = 6;
                    f.Close();
                    return;
            }
        }

        void ReportFrames()
        {
            if (frameT.Count < 3) { Line("Frames: insuficientes"); return; }
            List<double> d = new List<double>();
            List<double> stepPx = new List<double>();
            for (int i = 1; i < frameT.Count; i++) { d.Add((frameT[i] - frameT[i - 1]) * 1000); stepPx.Add(frameO[i] - frameO[i - 1]); }
            d.Sort();
            double sum = 0; foreach (double x in d) sum += x;
            double mean = sum / d.Count;
            int slow = 0; foreach (double x in d) if (x > 25) slow++;
            Line(string.Format(CultureInfo.InvariantCulture,
                "Frames: {0} en 3s => {1:F1} fps | intervalo medio {2:F2}ms, p95 {3:F2}ms, max {4:F2}ms | frames >25ms: {5}",
                d.Count, d.Count / 3.0, mean, d[(int)(d.Count * 0.95)], d[d.Count - 1], slow));
            double mx = 0; foreach (double x in stepPx) mx = Math.Max(mx, x);
            Line(string.Format(CultureInfo.InvariantCulture, "Salto máximo por frame: {0:F2}px", mx));
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); } catch { }
            try { Native.timeBeginPeriod(1); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string dir = AppDomain.CurrentDomain.BaseDirectory;
            bool selftest = args.Length > 0 && args[0] == "--selftest";
            string cfg = selftest ? Path.Combine(Path.GetTempPath(), "teleprompter_selftest.txt") : Path.Combine(dir, "teleprompter_config.txt");
            if (selftest && File.Exists(cfg)) File.Delete(cfg);

            MainForm f = new MainForm(Settings.Load(cfg), cfg);
            if (selftest) f.Test = new SelfTest(f, args.Length > 1 ? args[1] : dir);
            Application.Run(f);
        }
    }
}
