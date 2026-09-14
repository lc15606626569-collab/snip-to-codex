using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;

static partial class Settings {
    static string FilePath {get{return Path.Combine(Program.DataDir,"clipboard-only");}}
    public static bool AutoPaste {get{return !File.Exists(FilePath);}set{Directory.CreateDirectory(Program.DataDir);if(value){if(File.Exists(FilePath))File.Delete(FilePath);}else File.WriteAllText(FilePath,"1");}}
}
static class Theme {
    public static readonly Color Ink=Color.FromArgb(23,38,54),Muted=Color.FromArgb(101,117,131),Paper=Color.FromArgb(248,250,252),Mint=Color.FromArgb(69,231,179),Accent=Color.FromArgb(10,105,85),Line=Color.FromArgb(222,230,235);
    public static readonly Font Body=new Font("Microsoft YaHei UI",10),Small=new Font("Microsoft YaHei UI",9),Heading=new Font("Microsoft YaHei UI",25,FontStyle.Bold);
    public static GraphicsPath Round(Rectangle r,int radius){float d=radius*2;var p=new GraphicsPath();p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    public static void FillRound(Graphics g,Color c,Rectangle r,int radius){using(var p=Round(r,radius))using(var b=new SolidBrush(c))g.FillPath(b,p);}
    public static void StrokeRound(Graphics g,Color c,Rectangle r,int radius){using(var p=Round(r,radius))using(var pen=new Pen(c))g.DrawPath(pen,p);}
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    public static Icon MakeIcon(){using(Bitmap b=new Bitmap(64,64)){using(Graphics g=Graphics.FromImage(b)){g.SmoothingMode=SmoothingMode.AntiAlias;FillRound(g,Ink,new Rectangle(0,0,63,63),16);using(var p=new Pen(Mint,5)){g.DrawLines(p,new Point[]{new Point(27,17),new Point(17,17),new Point(17,27)});g.DrawLines(p,new Point[]{new Point(37,17),new Point(47,17),new Point(47,27)});g.DrawLines(p,new Point[]{new Point(17,37),new Point(17,47),new Point(27,47)});g.DrawLines(p,new Point[]{new Point(37,47),new Point(47,47),new Point(47,37)});}g.FillEllipse(Brushes.White,28,28,8,8);}IntPtr h=b.GetHicon();try{return (Icon)Icon.FromHandle(h).Clone();}finally{DestroyIcon(h);}}}
    public static void OpenGuide(){string path=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","docs","guide.html"));if(File.Exists(path))Process.Start(path);else MessageBox.Show("请保留完整解压后的文件夹，教程位于 docs/guide.html。","使用教程");}
    public static void Text(Graphics g,string value,Font font,Color color,Rectangle r){TextRenderer.DrawText(g,value,font,r,color,TextFormatFlags.WordBreak|TextFormatFlags.NoPadding);}
}
sealed class ModernButton:Button {
    bool hover;readonly bool primary;
    public Color SurfaceColor=Color.Empty;
    public ModernButton(string text,bool prominent){Text=text;primary=prominent;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Font=Theme.Body;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);AccessibleName=text;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(SurfaceColor.IsEmpty?Parent.BackColor:SurfaceColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new Rectangle(1,1,Width-3,Height-3);Theme.FillRound(e.Graphics,primary?(hover?Color.FromArgb(8,83,68):Theme.Accent):(hover?Color.FromArgb(226,235,240):Color.White),r,10);if(!primary)Theme.StrokeRound(e.Graphics,Theme.Line,r,10);TextRenderer.DrawText(e.Graphics,Text,Font,r,primary?Color.White:Theme.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);if(Focused)Theme.StrokeRound(e.Graphics,Theme.Mint,new Rectangle(4,4,Width-9,Height-9),8);}
}
sealed class RoundPanel:Panel {
    public RoundPanel(){DoubleBuffered=true;}
    protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(Width>24&&Height>24){var old=Region;using(var p=Theme.Round(new Rectangle(0,0,Width,Height),12))Region=new Region(p);if(old!=null)old.Dispose();}}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Theme.StrokeRound(e.Graphics,Theme.Line,new Rectangle(0,0,Width-1,Height-1),12);base.OnPaint(e);}
}
sealed class WelcomeForm:Form {
    string shortcut; CheckBox autoPaste;
    public void SetShortcut(string value){shortcut=value;Invalidate();}
    public WelcomeForm(string hotkey,Action capture,Action configure=null,Action floatingChanged=null){
        shortcut=hotkey;Text="截图到 Codex · 使用面板";Icon=Theme.MakeIcon();BackColor=Theme.Paper;Font=Theme.Body;StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(880,640);MinimumSize=Size;MaximumSize=Size;MaximizeBox=false;DoubleBuffered=true;
        var begin=new ModernButton("开始截图",true);begin.SetBounds(36,496,246,48);begin.Click+=(s,e)=>capture();Controls.Add(begin);
        var tutorial=new ModernButton("查看完整图文教程",false);tutorial.SetBounds(298,496,246,48);tutorial.Click+=(s,e)=>Theme.OpenGuide();Controls.Add(tutorial);
        var folder=new ModernButton("打开截图文件夹",false);folder.SetBounds(560,496,284,48);folder.Click+=(s,e)=>{Directory.CreateDirectory(Program.CaptureDir);Process.Start("explorer.exe",Program.CaptureDir);};Controls.Add(folder);
        autoPaste=new CheckBox();autoPaste.Text="截图后尝试自动粘贴到 Codex";autoPaste.Checked=Settings.AutoPaste;autoPaste.SetBounds(38,568,340,28);autoPaste.ForeColor=Theme.Ink;autoPaste.CheckedChanged+=(s,e)=>Settings.AutoPaste=autoPaste.Checked;Controls.Add(autoPaste);
        var floating=new CheckBox();floating.Text="在对话输入栏旁显示小剪刀";floating.Checked=Settings.ShowScissors;floating.SetBounds(410,568,400,28);floating.ForeColor=Theme.Ink;floating.CheckedChanged+=(s,e)=>{Settings.ShowScissors=floating.Checked;if(floatingChanged!=null)floatingChanged();};Controls.Add(floating);
        var change=new ModernButton("更改快捷键",false);change.SurfaceColor=Theme.Ink;change.SetBounds(688,213,136,44);change.Click+=(s,e)=>{if(configure!=null)configure();};Controls.Add(change);
        var tip=new Label();tip.Text="关闭此面板后，快捷键仍可使用。退出请右键托盘图标。";tip.ForeColor=Theme.Muted;tip.Font=Theme.Small;tip.AutoSize=false;tip.SetBounds(38,605,800,22);Controls.Add(tip);
    }
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        Theme.Text(g,"SNIP TO CODEX   /   截图到 Codex",Theme.Small,Theme.Accent,new Rectangle(38,28,640,24));
        Theme.Text(g,"把你看到的，发给 AI。",Theme.Heading,Theme.Ink,new Rectangle(34,67,780,54));
        Theme.Text(g,"框选画面，确认图片，再带着问题发送。第一次用？跟着下面三步走。",Theme.Body,Theme.Muted,new Rectangle(38,136,800,32));
        Theme.FillRound(g,Theme.Ink,new Rectangle(36,191,808,90),18);
        Theme.Text(g,"随时呼出截图",Theme.Body,Color.FromArgb(188,207,215),new Rectangle(60,211,280,26));
        using(var f=new Font("Segoe UI",15,FontStyle.Bold))Theme.Text(g,shortcut,f,Theme.Mint,new Rectangle(330,215,350,46));
        Theme.Text(g,"也可点击对话输入栏旁的小剪刀",Theme.Small,Color.FromArgb(188,207,215),new Rectangle(60,245,340,22));
        Card(g,36,"01","框选你要的画面","按快捷键，拖动鼠标选择区域。\nEsc 或右键可取消。 ");
        Card(g,310,"02","确认并放入输入框","按 Enter 或点击「完成」。\n看到图片缩略图，才算添加成功。");
        Card(g,584,"03","输入问题，再发送","例如：这条报错怎么解决？\n点击聊天软件的发送按钮。");
        Theme.Text(g,"没自动出现图片？点一下聊天输入框，再按 Ctrl + V。网页版也可这样用。",Theme.Small,Theme.Muted,new Rectangle(38,454,800,28));
        base.OnPaint(e);
    }
    void Card(Graphics g,int x,string number,string title,string text){var r=new Rectangle(x,308,260,126);Theme.FillRound(g,Color.White,r,14);Theme.StrokeRound(g,Theme.Line,r,14);Theme.Text(g,number,Theme.Small,Theme.Accent,new Rectangle(x+18,322,40,22));using(var f=new Font("Microsoft YaHei UI",11,FontStyle.Bold))Theme.Text(g,title,f,Theme.Ink,new Rectangle(x+18,349,236,27));Theme.Text(g,text,Theme.Small,Theme.Muted,new Rectangle(x+18,384,226,44));}
    protected override void Dispose(bool disposing){if(disposing && Icon!=null)Icon.Dispose();base.Dispose(disposing);}
}

static class Preview {
    public static bool Rendering;
    public static void Export(string dir){
        Rendering=true;
        Directory.CreateDirectory(dir);
        using(var f=new WelcomeForm("Ctrl + Alt + S",()=>{})) { f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,f.Size));b.Save(Path.Combine(dir,"welcome.png"),ImageFormat.Png);}f.Close(); }
        using(var f=new ShortcutForm(HotkeyChoice.Default,c=>null)){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,f.Size));b.Save(Path.Combine(dir,"shortcut-settings.png"),ImageFormat.Png);}f.Close();}
        using(var b=new Bitmap(1000,270))using(var g=Graphics.FromImage(b)){
            g.Clear(Color.White);g.SmoothingMode=SmoothingMode.AntiAlias;
            Theme.Text(g,"点击输入栏旁的小剪刀，直接框选。",Theme.Heading,Theme.Ink,new Rectangle(36,22,920,52));
            Theme.Text(g,"示例位置 · 随输入栏移动，切到其他应用时隐藏",Theme.Small,Theme.Muted,new Rectangle(40,82,900,25));
            Rectangle field=new Rectangle(42,128,842,96);Theme.FillRound(g,Color.White,field,18);Theme.StrokeRound(g,Theme.Line,field,18);
            Theme.Text(g,"向 Codex 提问…",Theme.Body,Theme.Muted,new Rectangle(64,154,770,28));
            Theme.FillRound(g,Color.White,new Rectangle(896,150,40,40),8);ScissorsButton.DrawScissors(g,Color.FromArgb(91,91,91),896,150);
            b.Save(Path.Combine(dir,"scissors.png"),ImageFormat.Png);
        }
        using(var b=new Bitmap(160,160))using(var g=Graphics.FromImage(b)){g.Clear(Color.White);g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(4,4);ScissorsButton.DrawScissors(g,Color.FromArgb(91,91,91),0,0);b.Save(Path.Combine(dir,"scissors-button.png"),ImageFormat.Png);}
        using(var icon=Theme.MakeIcon())using(var stream=File.Create(Path.Combine(dir,"app.ico")))icon.Save(stream);
        using(var b=new Bitmap(1200,780)){
            using(var g=Graphics.FromImage(b)){
                g.Clear(Color.FromArgb(232,238,241));g.SmoothingMode=SmoothingMode.AntiAlias;
                Theme.FillRound(g,Color.White,new Rectangle(92,102,1016,548),24);
                Theme.Text(g,"一张截图，让问题更清楚",Theme.Heading,Theme.Ink,new Rectangle(140,155,900,70));
                Theme.Text(g,"示例画面 · 不包含真实桌面或聊天内容",Theme.Body,Theme.Muted,new Rectangle(144,229,900,35));
                Theme.FillRound(g,Theme.Ink,new Rectangle(146,304,904,238),18);
                Theme.Text(g,"运行失败",Theme.Heading,Theme.Mint,new Rectangle(178,337,760,62));
                Theme.Text(g,"这里放需要分析的报错、图表、文字或设计稿。",Theme.Body,Color.White,new Rectangle(182,414,810,50));
            }
            using(var f=new SnipForm(b,new Rectangle(-20000,-20000,b.Width,b.Height))){f.TopMost=false;f.Show();f.PreviewSelection(new Rectangle(138,294,922,260));Application.DoEvents();using(var output=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(output,new Rectangle(Point.Empty,f.Size));output.Save(Path.Combine(dir,"selection.png"),ImageFormat.Png);}f.Close();}
        }
    }
}
