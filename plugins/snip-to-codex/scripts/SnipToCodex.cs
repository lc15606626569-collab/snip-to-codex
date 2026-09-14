using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Automation;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;

static class Native {
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int mode);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle,out WindowRect rect);
    [StructLayout(LayoutKind.Sequential)] public struct WindowRect {public int Left,Top,Right,Bottom;public Rectangle Rectangle{get{return Rectangle.FromLTRB(Left,Top,Right,Bottom);}}}
}

static class Program {
    public static string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SnipToCodex");
    public static string CaptureDir = Path.Combine(DataDir,"Captures");
    public static void Log(string message) {
        try { Directory.CreateDirectory(DataDir); File.WriteAllText(Path.Combine(DataDir,"status.txt"),DateTime.Now.ToString("s")+" "+message); } catch { }
    }
    [STAThread] static int Main(string[] args) {
        try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { Native.SetProcessDPIAware(); }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try {
            if(args.Length>0 && args[0]=="--render-ui") { Preview.Export(args[1]); return 0; }
            if(args.Length>0 && args[0]=="--quit") { try { using(var e=EventWaitHandle.OpenExisting("Local\\SnipToCodex.Quit"))e.Set(); } catch(WaitHandleCannotBeOpenedException){} return 0; }
            if (args.Length>0 && args[0]=="--self-test") return SelfTest(args[1]);
            if (args.Length>0 && args[0]=="--probe") {
                IntPtr w=Bridge.Target(); var editor=Bridge.Editor(w);
                File.WriteAllText(args[1],"window="+w+"; composer="+(editor!=null)); return editor==null?2:0;
            }
            if (args.Length>0 && args[0]=="--test-attach") {
                using (Bitmap b=new Bitmap(420,120)) { using(Graphics g=Graphics.FromImage(b)) {g.Clear(Color.White);g.DrawString("Snip to Codex - attachment test",SystemFonts.DefaultFont,Brushes.Black,20,45);} Clipboard.SetImage(b); }
                bool sent=Bridge.Paste(Bridge.Target()); File.WriteAllText(args[1],"paste="+sent);return sent?0:2;
            }
            if (args.Length>0 && args[0]=="--capture") {
                if(args.Length!=2) return 2;
                using(Bitmap image=Capture()) { if(image==null) return 3; Save(image,args[1]); } return 0;
            }
            bool created;
            using(Mutex mutex=new Mutex(true,"Local\\SnipToCodex.Tray.v1",out created)) {
                if(!created) { try {using(var e=EventWaitHandle.OpenExisting("Local\\SnipToCodex.Show"))e.Set();}catch(WaitHandleCannotBeOpenedException){} return 0; }
                Application.Run(new TrayContext(args.Length>0 && args[0]=="--quiet"));
            }
            return 0;
        } catch(Exception e) { Log(e.ToString()); MessageBox.Show("程序未能完成操作：\n"+e.Message+"\n\n可在截图文件夹的上一级查看 status.txt。","截图到 Codex",MessageBoxButtons.OK,MessageBoxIcon.Warning); return 1; }
    }
    public static Bitmap Capture() {
        using(var signal=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\SnipToCodex.Capture")) {
        signal.Set();try {
        Thread.Sleep(300);
        Rectangle bounds=SystemInformation.VirtualScreen;
        using(Bitmap desktop=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb)) {
            using(Graphics g=Graphics.FromImage(desktop)) g.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size,CopyPixelOperation.SourceCopy);
            using(SnipForm form=new SnipForm(desktop,bounds)) {
                if(form.ShowDialog()!=DialogResult.OK) return null;
                return desktop.Clone(form.Selection,PixelFormat.Format32bppArgb);
            }
        }
        } finally {signal.Reset();}
        }
    }
    public static void Save(Bitmap image,string path) {
        path=Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(path));
        image.Save(path,ImageFormat.Png);
    }
    static int SelfTest(string folder) {
        Directory.CreateDirectory(folder);
        DataDir=Path.Combine(folder,"settings-test");
        Settings.AutoPaste=false;if(Settings.AutoPaste)throw new Exception("Clipboard-only preference failed");
        Settings.AutoPaste=true;if(!Settings.AutoPaste)throw new Exception("Auto-paste preference failed");
        ShortcutTests.Run();
        using(Bitmap b=new Bitmap(300,200)) {
            using(Graphics g=Graphics.FromImage(b)) {g.Clear(Color.Navy);g.FillRectangle(Brushes.Lime,40,50,80,70);}
            Rectangle r=SnipForm.Normalize(new Point(120,120),new Point(40,50),b.Size);
            if(r!=new Rectangle(40,50,80,70)) throw new Exception("Reverse drag failed");
            Rectangle edge=SnipForm.Normalize(new Point(-20,-30),new Point(500,400),b.Size);
            if(edge!=new Rectangle(0,0,300,200)) throw new Exception("Clipping failed");
            using(Bitmap cropped=b.Clone(r,PixelFormat.Format32bppArgb)) {
                Save(cropped,Path.Combine(folder,"crop.png"));
                using(Bitmap loaded=new Bitmap(Path.Combine(folder,"crop.png")))
                    if(loaded.Size!=r.Size || loaded.GetPixel(79,69).ToArgb()!=Color.Lime.ToArgb()) throw new Exception("PNG crop failed");
            }
            using(SnipForm f=new SnipForm(b,new Rectangle(0,0,300,200))) {
                var timer=new System.Windows.Forms.Timer();timer.Interval=120;
                timer.Tick+=(s,e)=>{timer.Stop();f.DialogResult=DialogResult.Cancel;f.Close();};
                timer.Start();if(f.ShowDialog()!=DialogResult.Cancel) throw new Exception("Cancel failed");timer.Dispose();
            }
            using(SnipForm f=new SnipForm(b,new Rectangle(0,0,300,200))) {
                var timer=new System.Windows.Forms.Timer();timer.Interval=120;
                Exception failure=null;
                timer.Tick+=(s,e)=>{timer.Stop();try{f.TestSelectionControls();}catch(Exception ex){failure=ex;f.Close();}};
                timer.Start();var result=f.ShowDialog();timer.Dispose();if(failure!=null)throw failure;if(result!=DialogResult.OK)throw new Exception("Complete button failed");
            }
        }
        File.WriteAllText(Path.Combine(folder,"self-test.txt"),"PASS: reverse drag, bounds clipping, pixel-accurate PNG, overlay cancellation, reselect, complete button, saved preference.\r\n");return 0;
    }
}

static class Bridge {
    public static bool IsTarget(IntPtr handle) {
        if(handle==IntPtr.Zero) return false;
        try {
            uint id;Native.GetWindowThreadProcessId(handle,out id);
            using(Process p=Process.GetProcessById((int)id)) {
                string n=p.ProcessName.ToLowerInvariant();
                string path=p.MainModule.FileName;
                return (n=="chatgpt" || n=="codex") && path.IndexOf("OpenAI",StringComparison.OrdinalIgnoreCase)>=0;
            }
        } catch { return false; }
    }
    public static IntPtr Target() {
        IntPtr active=Native.GetForegroundWindow();if(IsTarget(active)) return active;
        IntPtr found=IntPtr.Zero;
        foreach(string name in new string[]{"ChatGPT","Codex"}) foreach(Process p in Process.GetProcessesByName(name)) {
            using(p) { IntPtr w=p.MainWindowHandle;if(IsTarget(w)) {if(found!=IntPtr.Zero && found!=w) return IntPtr.Zero;found=w;} }
        }
        return found;
    }
    public static AutomationElement Editor(IntPtr window) {
        if(!IsTarget(window)) return null;
        var root=AutomationElement.FromHandle(window);
        var condition=new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit);
        var edits=root.FindAll(TreeScope.Descendants,condition);
        AutomationElement found=null;
        foreach(AutomationElement e in edits) {
            var c=e.Current;
            if(c.IsOffscreen || !c.IsEnabled || !c.IsKeyboardFocusable) continue;
            string cls=c.ClassName??"";
            if(cls.IndexOf("ProseMirror",StringComparison.OrdinalIgnoreCase)<0) continue;
            if(found!=null) return null;found=e;
        }
        return found;
    }
    public static bool Paste(IntPtr window) {
        try {
            if(!IsTarget(window)) return false;
            if(Native.IsIconic(window)) Native.ShowWindow(window,9);
            if(Native.GetForegroundWindow()!=window) Native.SetForegroundWindow(window);
            Thread.Sleep(180);
            if(Native.GetForegroundWindow()!=window) return false;
            var editor=Editor(window); if(editor==null) return false;
            editor.SetFocus();Thread.Sleep(100);
            var focused=AutomationElement.FocusedElement;
            if(Native.GetForegroundWindow()!=window || !Automation.Compare(editor,focused)) return false;
            SendKeys.SendWait("^v"); return true;
        } catch(Exception e) { Program.Log("Clipboard ready; paste fallback: "+e.Message);return false; }
    }
}

sealed class HotKeyWindow:NativeWindow,IDisposable {
    public event EventHandler Pressed;
    int activeId;public HotkeyChoice Current{get;private set;}
    public string Shortcut{get{return activeId==0?"未设置快捷键":Current.Label;}}
    public HotKeyWindow(bool registerDefault=true) {
        CreateHandle(new CreateParams());
        Current=HotkeyChoice.Default;
        if(registerDefault){string error;var desired=Settings.Shortcut;
            if(!Change(desired,false,out error) && !Change(HotkeyChoice.Default,false,out error))Change(new HotkeyChoice(3,Keys.F8),false,out error);
        }
    }
    public bool Change(HotkeyChoice choice,bool persist,out string error){
        error=null;if(!choice.Valid){error="请使用 Ctrl 或 Alt，搭配字母、数字或功能键。";return false;}
        bool same=activeId!=0 && Current.Equals(choice);int next=activeId==1?2:1;
        if(!same && !Native.RegisterHotKey(Handle,next,choice.Modifiers|0x4000,(uint)choice.Key)){error="这个快捷键已被其他程序占用，请换一组。原快捷键仍可使用。";return false;}
        try{if(persist)Settings.Shortcut=choice;}catch(Exception e){if(!same)Native.UnregisterHotKey(Handle,next);error="设置未能保存，原快捷键已保留。"+e.Message;return false;}
        if(!same){if(activeId!=0)Native.UnregisterHotKey(Handle,activeId);activeId=next;Current=choice;}
        return true;
    }
    protected override void WndProc(ref Message m) {if(m.Msg==0x0312 && m.WParam.ToInt32()==activeId && Pressed!=null) Pressed(this,EventArgs.Empty);base.WndProc(ref m);}
    public void Dispose(){if(activeId!=0)Native.UnregisterHotKey(Handle,activeId);DestroyHandle();}
}

sealed class TrayContext:ApplicationContext {
    NotifyIcon tray; HotKeyWindow hotkey; bool busy,configuring; WelcomeForm welcome;
    FloatingScissors floating;ToolStripMenuItem captureMenu;
    EventWaitHandle quitEvent,showEvent; System.Windows.Forms.Timer signals;
    public TrayContext(bool quiet) {
        hotkey=new HotKeyWindow();hotkey.Pressed+=(s,e)=>BeginCapture();
        var menu=new ContextMenuStrip();
        menu.Font=Theme.Body;menu.BackColor=Theme.Paper;menu.ForeColor=Theme.Ink;
        captureMenu=new ToolStripMenuItem("截图到 Codex（"+hotkey.Shortcut+"）",null,(s,e)=>BeginCapture());menu.Items.Add(captureMenu);
        menu.Items.Add("更改截图快捷键",null,(s,e)=>ConfigureShortcut());
        menu.Items.Add("打开使用面板",null,(s,e)=>ShowWelcome());
        menu.Items.Add("查看图文教程",null,(s,e)=>Theme.OpenGuide());
        menu.Items.Add("打开截图文件夹",null,(s,e)=>{Directory.CreateDirectory(Program.CaptureDir);Process.Start("explorer.exe",Program.CaptureDir);});
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出",null,(s,e)=>ExitThread());
        tray=new NotifyIcon();tray.Icon=Theme.MakeIcon();tray.Text="截图到 Codex | "+hotkey.Shortcut;tray.ContextMenuStrip=menu;tray.Visible=true;
        tray.DoubleClick+=(s,e)=>BeginCapture();
        floating=new FloatingScissors(()=>busy,(target)=>BeginCapture(target),menu);floating.Shortcut=hotkey.Shortcut;
        quitEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\SnipToCodex.Quit");
        showEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\SnipToCodex.Show");
        signals=new System.Windows.Forms.Timer();signals.Interval=250;signals.Tick+=(s,e)=>{if(quitEvent.WaitOne(0)){ExitThread();return;}if(showEvent.WaitOne(0))ShowWelcome();};signals.Start();
        Program.Log("ready; hotkey="+hotkey.Shortcut);
        if(!quiet)ShowWelcome();
    }
    void ShowWelcome(){if(welcome==null || welcome.IsDisposed)welcome=new WelcomeForm(hotkey.Shortcut,()=>BeginCapture(),ConfigureShortcut,()=>floating.RefreshVisibility());welcome.Show();welcome.Activate();}
    void ConfigureShortcut(){if(configuring)return;configuring=true;try{using(var dialog=new ShortcutForm(hotkey.Current,(choice)=>{string error;if(!hotkey.Change(choice,true,out error))return error;captureMenu.Text="截图到 Codex（"+hotkey.Shortcut+"）";tray.Text="截图到 Codex | "+hotkey.Shortcut;floating.Shortcut=hotkey.Shortcut;if(welcome!=null&&!welcome.IsDisposed)welcome.SetShortcut(hotkey.Shortcut);Program.Log("shortcut saved; "+hotkey.Shortcut);return null;}))dialog.ShowDialog(welcome!=null&&welcome.Visible?welcome:null);}finally{configuring=false;}}
    void BeginCapture(IntPtr requestedTarget=default(IntPtr)) {
        if(busy||configuring)return;busy=true;
        try {
            IntPtr target=requestedTarget!=IntPtr.Zero?requestedTarget:Bridge.Target();
            floating.HideForCapture();
            if(welcome!=null && welcome.Visible){welcome.Hide();Thread.Sleep(160);}
            using(Bitmap image=Program.Capture()) {
                if(image==null) {Program.Log("cancelled");return;}
                string path=Path.Combine(Program.CaptureDir,"snip-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".png");
                Program.Save(image,path);
                Clipboard.SetDataObject(new DataObject(DataFormats.Bitmap,image),true,10,80);
                bool pasted=Settings.AutoPaste && Bridge.Paste(target);
                Program.Log((pasted?"paste requested; ":"saved; manual paste needed; ")+path);
                tray.ShowBalloonTip(3000,"截图已保存",pasted?"已尝试粘贴，请确认输入框出现图片后按发送。":"图片已复制；请回到聊天输入框按 Ctrl + V。",ToolTipIcon.Info);
            }
        } catch(Exception e) {Program.Log(e.ToString());tray.ShowBalloonTip(4000,"截图未完成",e.Message,ToolTipIcon.Warning);}
        finally {busy=false;}
    }
    protected override void ExitThreadCore(){signals.Stop();signals.Dispose();quitEvent.Dispose();showEvent.Dispose();floating.Dispose();if(welcome!=null)welcome.Dispose();tray.Visible=false;tray.Icon.Dispose();tray.Dispose();hotkey.Dispose();base.ExitThreadCore();}
}

sealed class SnipForm:Form {
    readonly Bitmap desktop; Point anchor; bool dragging; Panel actions;
    public Rectangle Selection {get;private set;}
    public SnipForm(Bitmap image,Rectangle bounds) {
        desktop=image;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;
        AutoScaleMode=AutoScaleMode.None;Bounds=bounds;TopMost=true;ShowInTaskbar=false;KeyPreview=true;DoubleBuffered=true;Cursor=Cursors.Cross;
        Text="Snip to Codex · 框选截图";
        actions=new RoundPanel();actions.Size=new Size(366,60);actions.BackColor=Theme.Paper;actions.Visible=false;
        var cancel=new ModernButton("取消",false);cancel.SetBounds(10,10,72,40);cancel.Click+=(s,e)=>Cancel();
        var retry=new ModernButton("重新框选",false);retry.SetBounds(88,10,102,40);retry.Click+=(s,e)=>{Selection=Rectangle.Empty;actions.Hide();Focus();Invalidate();};
        var ok=new ModernButton("完成  ↵",true);ok.SetBounds(200,10,156,40);ok.Click+=(s,e)=>Accept();
        actions.Controls.Add(ok);actions.Controls.Add(retry);actions.Controls.Add(cancel);Controls.Add(actions);
        KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){Cancel();e.Handled=true;}if(e.KeyCode==Keys.Enter){Accept();e.Handled=true;}};
        Shown+=(s,e)=>{Activate();Focus();};
    }
    public static Rectangle Normalize(Point a,Point b,Size size) {
        int x1=Math.Max(0,Math.Min(size.Width,Math.Min(a.X,b.X))),y1=Math.Max(0,Math.Min(size.Height,Math.Min(a.Y,b.Y)));
        int x2=Math.Max(0,Math.Min(size.Width,Math.Max(a.X,b.X))),y2=Math.Max(0,Math.Min(size.Height,Math.Max(a.Y,b.Y)));
        return Rectangle.FromLTRB(x1,y1,x2,y2);
    }
    void Accept(){if(Selection.Width>1&&Selection.Height>1){DialogResult=DialogResult.OK;Close();}}
    void Cancel(){DialogResult=DialogResult.Cancel;Close();}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Right){Cancel();return;}if(e.Button!=MouseButtons.Left)return;if(e.Clicks>1 && Selection.Contains(e.Location)){Accept();return;}actions.Visible=false;anchor=e.Location;dragging=true;Capture=true;Selection=Rectangle.Empty;Invalidate();}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging){Selection=Normalize(anchor,e.Location,desktop.Size);Invalidate();}}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!dragging)return;dragging=false;Capture=false;Selection=Normalize(anchor,e.Location,desktop.Size);if(Selection.Width>1&&Selection.Height>1){int x=Math.Max(0,Math.Min(Width-actions.Width,Selection.Right-actions.Width));int y=Selection.Bottom+8;if(y+actions.Height>Height)y=Math.Max(0,Selection.Top-actions.Height-8);actions.Location=new Point(x,y);actions.Visible=true;}Invalidate();}
    public void PreviewSelection(Rectangle r){Selection=r;actions.Location=new Point(Math.Max(12,r.Right-actions.Width),Math.Min(Height-actions.Height-12,r.Bottom+12));actions.Visible=true;}
    public void TestSelectionControls(){
        OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,40,50,0));OnMouseMove(new MouseEventArgs(MouseButtons.Left,0,120,120,0));OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,120,120,0));
        if(Selection!=new Rectangle(40,50,80,70))throw new Exception("Drag selection failed");
        foreach(Control c in actions.Controls)if(c.Text=="重新框选")((Button)c).PerformClick();
        if(Selection!=Rectangle.Empty)throw new Exception("Reselect failed");
        PreviewSelection(new Rectangle(10,20,80,60));foreach(Control c in actions.Controls)if(c.Text=="完成  ↵")((Button)c).PerformClick();
    }
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.DrawImageUnscaled(desktop,0,0);
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(Brush dim=new SolidBrush(Color.FromArgb(140,10,17,30))) e.Graphics.FillRectangle(dim,ClientRectangle);
        if(Selection.Width>0&&Selection.Height>0){
            e.Graphics.DrawImage(desktop,Selection,Selection,GraphicsUnit.Pixel);
            using(Pen pen=new Pen(Theme.Mint,2))e.Graphics.DrawRectangle(pen,Selection);
            foreach(Point p in new Point[]{Selection.Location,new Point(Selection.Right,Selection.Top),new Point(Selection.Left,Selection.Bottom),new Point(Selection.Right,Selection.Bottom)}){e.Graphics.FillRectangle(Brushes.White,p.X-3,p.Y-3,6,6);}
            string dim=Selection.Width+" × "+Selection.Height+" px";
            Rectangle badge=new Rectangle(Selection.X,Math.Max(6,Selection.Y-38),148,30);
            Theme.FillRound(e.Graphics,Color.FromArgb(24,34,49),badge,9);
            TextRenderer.DrawText(e.Graphics,dim,Theme.Small,badge,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        }
        var monitor=Screen.FromPoint(Cursor.Position).Bounds;
        int tx=Math.Max(12,Math.Min(Width-576,monitor.Left-Left+(monitor.Width-552)/2)),ty=Math.Max(12,monitor.Top-Top+24);
        if(Preview.Rendering){tx=(Width-552)/2;ty=24;}
        Rectangle hint=new Rectangle(tx,ty,552,50);
        Theme.FillRound(e.Graphics,Color.FromArgb(24,34,49),hint,16);
        TextRenderer.DrawText(e.Graphics,"拖动框选  /  Enter 完成  /  Esc 取消",Theme.Body,hint,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        base.OnPaint(e);
    }
}
