using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;

struct HotkeyChoice:IEquatable<HotkeyChoice> {
    public readonly uint Modifiers;public readonly Keys Key;
    public HotkeyChoice(uint modifiers,Keys key){Modifiers=modifiers;Key=key;}
    public static HotkeyChoice Default{get{return new HotkeyChoice(3,Keys.S);}}
    public bool Valid{get{return (Modifiers&~7u)==0 && (Modifiers&3)!=0 && ((Key>=Keys.A&&Key<=Keys.Z)||(Key>=Keys.D0&&Key<=Keys.D9)||(Key>=Keys.F1&&Key<=Keys.F24)||Key==Keys.PrintScreen);}}
    public string Label{get{return ((Modifiers&2)!=0?"Ctrl + ":"")+((Modifiers&1)!=0?"Alt + ":"")+((Modifiers&4)!=0?"Shift + ":"")+(Key>=Keys.D0&&Key<=Keys.D9?((int)Key-(int)Keys.D0).ToString():Key.ToString());}}
    public static HotkeyChoice FromKeyData(Keys data){uint m=0;if((data&Keys.Control)!=0)m|=2;if((data&Keys.Alt)!=0)m|=1;if((data&Keys.Shift)!=0)m|=4;return new HotkeyChoice(m,data&Keys.KeyCode);}
    public bool Equals(HotkeyChoice other){return Modifiers==other.Modifiers&&Key==other.Key;}
}

static partial class Settings {
    public static bool ShowScissors {get{return !File.Exists(Path.Combine(Program.DataDir,"scissors-hidden"));}set{Directory.CreateDirectory(Program.DataDir);string p=Path.Combine(Program.DataDir,"scissors-hidden");if(value){if(File.Exists(p))File.Delete(p);}else File.WriteAllText(p,"1");}}
    public static HotkeyChoice Shortcut {
        get {try{string[] parts=File.ReadAllText(Path.Combine(Program.DataDir,"shortcut.txt")).Split(',');uint m;int k;if(parts.Length==2&&uint.TryParse(parts[0],out m)&&int.TryParse(parts[1],out k)){var c=new HotkeyChoice(m,(Keys)k);if(c.Valid)return c;}}catch(IOException){}catch(UnauthorizedAccessException){}return HotkeyChoice.Default;}
        set {if(!value.Valid)throw new ArgumentException("Invalid shortcut");Directory.CreateDirectory(Program.DataDir);string target=Path.Combine(Program.DataDir,"shortcut.txt"),temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,value.Modifiers+","+(int)value.Key);if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);}finally{if(File.Exists(temp))File.Delete(temp);}}
    }
}

sealed class ShortcutForm:Form {
    HotkeyChoice candidate;readonly TextBox input;readonly Label status;readonly Func<HotkeyChoice,string> apply;
    public ShortcutForm(HotkeyChoice current,Func<HotkeyChoice,string> save){
        candidate=current;apply=save;Text="设置截图快捷键";Icon=Theme.MakeIcon();ClientSize=new Size(540,302);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Paper;Font=Theme.Body;
        var title=new Label{Text="按下你想使用的组合键",Font=new Font("Microsoft YaHei UI",16,FontStyle.Bold),ForeColor=Theme.Ink};title.SetBounds(26,22,490,38);Controls.Add(title);
        var description=new Label{Text="Ctrl 或 Alt + 字母、数字、F1–F24，也可加入 Shift。",ForeColor=Theme.Muted};description.SetBounds(28,68,490,28);Controls.Add(description);
        input=new TextBox{ReadOnly=true,Text=candidate.Label,Font=new Font("Segoe UI",17),BackColor=Color.White,ForeColor=Theme.Accent,ShortcutsEnabled=false,AccessibleName="按下新的截图快捷键"};input.SetBounds(28,110,484,44);input.KeyDown+=Record;Controls.Add(input);
        status=new Label{Text="点击上方输入框，再按下组合键。点击保存后立即生效。",ForeColor=Theme.Muted};status.SetBounds(28,166,484,48);Controls.Add(status);
        var reset=new ModernButton("恢复默认",false);reset.SetBounds(28,234,126,42);reset.Click+=(s,e)=>{candidate=HotkeyChoice.Default;input.Text=candidate.Label;status.Text="默认 Ctrl + Alt + S；点击保存应用。";status.ForeColor=Theme.Muted;};Controls.Add(reset);
        var cancel=new ModernButton("取消",false);cancel.SetBounds(282,234,104,42);cancel.Click+=(s,e)=>{DialogResult=DialogResult.Cancel;Close();};Controls.Add(cancel);
        var saveButton=new ModernButton("保存",true);saveButton.SetBounds(398,234,114,42);saveButton.Click+=(s,e)=>Save();Controls.Add(saveButton);CancelButton=cancel;
        Shown+=(s,e)=>input.Focus();
    }
    void Record(object sender,KeyEventArgs e){e.SuppressKeyPress=true;e.Handled=true;var c=HotkeyChoice.FromKeyData(e.KeyData);if(!c.Valid){status.Text="请按住 Ctrl 或 Alt，再按字母、数字或功能键。";status.ForeColor=Theme.Muted;return;}candidate=c;input.Text=c.Label;status.Text="点击保存，将检查占用并应用；原快捷键此时仍有效。";status.ForeColor=Theme.Muted;}
    void Save(){string error=apply(candidate);if(error!=null){status.Text=error;status.ForeColor=Color.FromArgb(170,45,42);return;}DialogResult=DialogResult.OK;Close();}
    protected override void Dispose(bool disposing){if(disposing&&Icon!=null)Icon.Dispose();base.Dispose(disposing);}
}

sealed class Anchor {
    public IntPtr Window;public Rectangle Editor,WindowBounds;public DateTime Observed;
}
sealed class FloatingScissors:IDisposable {
    readonly ScissorsButton button;readonly Func<bool> isBusy;readonly System.Windows.Forms.Timer timer;readonly EventWaitHandle capture;
    Task<Anchor> query;Anchor current;DateTime nextQuery;bool disposed;
    public string Shortcut {set{button.SetHint(value);}}
    public FloatingScissors(Func<bool> busy,Action<IntPtr> clicked,ContextMenuStrip menu){
        isBusy=busy;button=new ScissorsButton(clicked);button.ContextMenuStrip=menu;
        capture=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\SnipToCodex.Capture");
        timer=new System.Windows.Forms.Timer{Interval=200};timer.Tick+=(s,e)=>Tick();timer.Start();
    }
    public void HideForCapture(){current=null;button.Hide();}
    public void RefreshVisibility(){Tick();}
    void Tick(){
        if(disposed)return;
        IntPtr active=Native.GetForegroundWindow();
        if(isBusy()||capture.WaitOne(0)||!Settings.ShowScissors||!Bridge.IsTarget(active)||Native.IsIconic(active)){button.Hide();current=null;return;}
        DateTime now=DateTime.UtcNow;
        if(query!=null&&query.IsCompleted){var answer=query.Result;query=null;if(answer!=null&&answer.Window==active)current=answer;}
        Native.WindowRect window;
        if(current!=null&&current.Window==active&&(now-current.Observed).TotalSeconds<2&&Native.GetWindowRect(active,out window)&&window.Rectangle==current.WindowBounds){
            Rectangle screen=Screen.FromRectangle(current.Editor).WorkingArea;
            Rectangle area=Rectangle.Intersect(screen,current.WindowBounds);
            Rectangle place=Position(current.Editor,area);
            if(place.IsEmpty){button.Hide();}else{button.Target=active;button.Bounds=place;if(!button.Visible)button.Show();}
        }else button.Hide();
        if(query==null&&now>=nextQuery){nextQuery=now.AddMilliseconds(650);IntPtr target=active;query=Task.Factory.StartNew(()=>ReadAnchor(target));}
    }
    static Anchor ReadAnchor(IntPtr target){
        try{Native.WindowRect window;if(!Native.GetWindowRect(target,out window))return null;var editor=Bridge.Editor(target);if(editor==null)return null;var bounds=editor.Current.BoundingRectangle;if(bounds.IsEmpty||bounds.Width<80||bounds.Height<16)return null;return new Anchor{Window=target,WindowBounds=window.Rectangle,Editor=Rectangle.FromLTRB((int)bounds.Left,(int)bounds.Top,(int)Math.Ceiling(bounds.Right),(int)Math.Ceiling(bounds.Bottom)),Observed=DateTime.UtcNow};}catch{return null;}
    }
    public static Rectangle Position(Rectangle editor,Rectangle available){
        const int size=40,gap=10;
        if(available.Width<size||available.Height<size)return Rectangle.Empty;
        int y=Math.Max(available.Top,Math.Min(available.Bottom-size,editor.Bottom-size));
        Rectangle right=new Rectangle(editor.Right+gap,y,size,size);if(available.Contains(right))return right;
        Rectangle left=new Rectangle(editor.Left-gap-size,y,size,size);if(available.Contains(left))return left;
        Rectangle above=new Rectangle(Math.Max(available.Left,Math.Min(available.Right-size,editor.Right-size)),editor.Top-size-gap,size,size);return available.Contains(above)?above:Rectangle.Empty;
    }
    public void Dispose(){disposed=true;timer.Stop();timer.Dispose();button.Dispose();capture.Dispose();}
}

sealed class ScissorsButton:Form {
    readonly Action<IntPtr> capture;readonly ToolTip hint;bool hover;
    public IntPtr Target;
    public ScissorsButton(Action<IntPtr> action){
        capture=action;Text="截图到 Codex · 小剪刀";AccessibleName="点击小剪刀，框选截图";AccessibleRole=AccessibleRole.PushButton;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;ShowInTaskbar=false;TopMost=true;AutoScaleMode=AutoScaleMode.None;Size=new Size(40,40);BackColor=Theme.Paper;Cursor=Cursors.Hand;DoubleBuffered=true;
        using(var path=Theme.Round(new Rectangle(0,0,40,40),13))Region=new Region(path);
        hint=new ToolTip();hint.SetToolTip(this,"框选截图");
    }
    public void SetHint(string shortcut){hint.SetToolTip(this,"框选截图 · "+shortcut);}
    protected override bool ShowWithoutActivation{get{return true;}}
    protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
    protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button==MouseButtons.Left&&ClientRectangle.Contains(e.Location)&&Target==Native.GetForegroundWindow()&&Bridge.IsTarget(Target))capture(Target);}
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(hover?Theme.Accent:Theme.Ink);DrawScissors(g,hover?Color.White:Theme.Mint,0,0);base.OnPaint(e);}
    public static void DrawScissors(Graphics g,Color color,int x,int y){using(var p=new Pen(color,1.8f)){p.StartCap=LineCap.Round;p.EndCap=LineCap.Round;g.DrawEllipse(p,x+9,y+9,8,8);g.DrawEllipse(p,x+9,y+23,8,8);g.DrawLine(p,x+16,y+15,x+30,y+29);g.DrawLine(p,x+16,y+25,x+30,y+11);}}
    protected override void Dispose(bool disposing){if(disposing)hint.Dispose();base.Dispose(disposing);}
}

static class ShortcutTests {
    public static void Run(){
        if(HotkeyChoice.FromKeyData(Keys.S).Valid||HotkeyChoice.FromKeyData(Keys.Shift|Keys.S).Valid)throw new Exception("Unmodified key accepted");
        var custom=HotkeyChoice.FromKeyData(Keys.Control|Keys.Shift|Keys.F19);if(!custom.Valid||custom.Label!="Ctrl + Shift + F19")throw new Exception("Shortcut parsing failed");
        Settings.Shortcut=custom;if(!Settings.Shortcut.Equals(custom))throw new Exception("Shortcut persistence failed");
        string p=Path.Combine(Program.DataDir,"shortcut.txt");File.WriteAllText(p,"invalid");if(!Settings.Shortcut.Equals(HotkeyChoice.Default))throw new Exception("Invalid shortcut file fallback failed");
        Settings.ShowScissors=false;if(Settings.ShowScissors)throw new Exception("Scissors preference failed");Settings.ShowScissors=true;
        Rectangle edit=new Rectangle(-400,700,300,44),area=new Rectangle(-800,0,800,900);
        Rectangle placed=FloatingScissors.Position(edit,area);if(!area.Contains(placed)||placed.IntersectsWith(edit))throw new Exception("Negative monitor positioning failed");
        edit=new Rectangle(0,300,400,50);area=new Rectangle(0,0,400,500);placed=FloatingScissors.Position(edit,area);if(placed.IsEmpty||placed.IntersectsWith(edit)||!area.Contains(placed))throw new Exception("Narrow window positioning failed");
        using(var a=new HotKeyWindow(false))using(var b=new HotKeyWindow(false)){
            string error;var first=new HotkeyChoice(7,Keys.F23);var occupied=new HotkeyChoice(7,Keys.F24);
            if(!a.Change(first,true,out error)||!b.Change(occupied,false,out error))throw new Exception("Test shortcut unavailable: "+error);
            if(a.Change(occupied,true,out error))throw new Exception("Shortcut collision was not detected");
            if(!a.Current.Equals(first)||!Settings.Shortcut.Equals(first))throw new Exception("Shortcut lost after collision");
            if(!a.Change(custom,true,out error))throw new Exception("Changing registered shortcut failed: "+error);
            if(!b.Change(first,false,out error))throw new Exception("Old shortcut was not released");
        }
    }
}
