using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Automation;
using System.Collections.Generic;

public class CaptureSchedule {
    public bool Active { get; private set; }
    public DateTime NextAt { get; private set; }
    public DateTime EndAt { get; private set; }
    int interval;
    public void Start(DateTime now,int seconds,int durationSeconds) {
        if(seconds<5)throw new ArgumentOutOfRangeException("seconds");
        interval=seconds;NextAt=now;EndAt=durationSeconds==0?DateTime.MaxValue:now.AddSeconds(durationSeconds);Active=true;
    }
    public void Stop() {Active=false;}
    public bool Poll(DateTime now,bool busy) {
        if(!Active)return false;
        if(now>=EndAt) {Stop();return false;}
        if(now<NextAt)return false;
        NextAt=now.AddSeconds(interval);
        return !busy;
    }
    public static string SelfTest() {
        var s=new CaptureSchedule();var t=new DateTime(2026,1,1);
        s.Start(t,5,12);
        if(!s.Poll(t,false)||s.Poll(t.AddSeconds(4),false)||!s.Poll(t.AddSeconds(5),false))throw new Exception("5 second cadence failed");
        if(s.Poll(t.AddSeconds(10),true)||s.Poll(t.AddSeconds(11),false))throw new Exception("Busy capture queued a backlog");
        if(s.Poll(t.AddSeconds(12),false)||s.Active)throw new Exception("Session timeout failed");
        s.Start(t,10,0);s.Stop();if(s.Poll(t.AddSeconds(30),false))throw new Exception("Stop failed");
        s.Start(t,10,0);if(!s.Poll(t,false)||s.Poll(t.AddSeconds(9),false)||!s.Poll(t.AddSeconds(10),false))throw new Exception("10 second cadence failed");
        return "PASS: 5s/10s cadence, busy-skip, timeout, stop, restart";
    }
}

class ScreenshotChat : Form {
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X,Y; }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int n);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h,ref POINT p);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h,ref POINT p);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int k);
    [StructLayout(LayoutKind.Sequential)] struct RECT {public int Left,Top,Right,Bottom;}
    delegate bool EnumWindowProc(IntPtr h,IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowProc callback,IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out RECT r);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
    class CaptureSource {
        public string Name; public Screen Screen; public IntPtr Window;
        public override string ToString() {return Name;}
    }
    AutomationElement chatInput;
    CaptureSource selectedSource;
    Button refreshSources=new Button();
    void RefreshSources() {
        if(busy||schedule.Active)return;
        monitor.Items.Clear();screens=Screen.AllScreens;
        foreach(var s in screens)monitor.Items.Add(new CaptureSource {Name="Monitor: "+s.DeviceName+(s.Primary?" (primary)":" (second monitor)"),Screen=s});
        EnumWindows((h,l)=> {
            if(h==Handle||!IsWindowVisible(h))return true;
            var title=new StringBuilder(512);GetWindowText(h,title,512);
            if(title.Length>0)monitor.Items.Add(new CaptureSource {Name="Window: "+title,Window=h});
            return true;
        },IntPtr.Zero);
        monitor.SelectedIndex=0;for(int i=0;i<screens.Length;i++)if(!screens[i].Primary)monitor.SelectedIndex=i;
    }
    Bitmap CaptureBitmap() {
        if(selectedSource.Window==IntPtr.Zero) {
            bounds=selectedSource.Screen.Bounds;
            var b=new Bitmap(bounds.Width,bounds.Height);
            try {using(var g=Graphics.FromImage(b))g.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size);return b;}
            catch {b.Dispose();throw;}
        }
        IntPtr h=selectedSource.Window;
        if(!IsWindow(h)||IsIconic(h))throw new Exception("Keep the selected capture window open and not minimized.");
        RECT r;if(!GetWindowRect(h,out r)||r.Right<=r.Left||r.Bottom<=r.Top)throw new Exception("Capture window size is unavailable.");
        bounds=new Rectangle(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);
        var bitmap=new Bitmap(bounds.Width,bounds.Height);
        try {
            bool ok;
            using(var g=Graphics.FromImage(bitmap)) {
                IntPtr dc=g.GetHdc();try {ok=PrintWindow(h,dc,2);}finally {g.ReleaseHdc(dc);}
            }
            if(!ok)throw new Exception("This app does not support window capture. Select a monitor instead.");
            return bitmap;
        } catch {bitmap.Dispose();throw;}
    }
    void RememberChatInput(POINT screenPoint) {
        var element=AutomationElement.FromPoint(new System.Windows.Point(screenPoint.X,screenPoint.Y));
        while(element!=null&&!element.Current.IsKeyboardFocusable)element=TreeWalker.ControlViewWalker.GetParent(element);
        if(element==null||element.Current.ControlType==ControlType.Window)throw new Exception("Click directly in the editable chat message box.");
        chatInput=element;
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,IntPtr pid);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a,uint b,bool attach);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
    delegate IntPtr KeyboardProc(int n,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id,KeyboardProc proc,IntPtr module,uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int n,IntPtr w,IntPtr l);
    IntPtr hook; KeyboardProc keyboardProc; int shortcutDown; bool ctrlHeld,shiftHeld;
    void FocusChat() {
        uint current=GetCurrentThreadId(),fg=GetWindowThreadProcessId(GetForegroundWindow(),IntPtr.Zero),dest=GetWindowThreadProcessId(target,IntPtr.Zero);
        bool a=fg!=current&&AttachThreadInput(current,fg,true), b=dest!=current&&dest!=fg&&AttachThreadInput(current,dest,true);
        try { if(IsIconic(target))ShowWindow(target,9); BringWindowToTop(target); SetForegroundWindow(target); }
        finally { if(b)AttachThreadInput(current,dest,false);if(a)AttachThreadInput(current,fg,false); }
        Log("Focus target="+target+" foreground="+GetForegroundWindow());
    }
    IntPtr Keyboard(int n,IntPtr w,IntPtr l) {
        if(n>=0) {
            int vk=Marshal.ReadInt32(l),msg=w.ToInt32();bool down=msg==0x100||msg==0x104;
            if(vk==0x11||vk==0xA2||vk==0xA3)ctrlHeld=down;
            if(vk==0x10||vk==0xA0||vk==0xA1)shiftHeld=down;
        }
        if(n>=0) {
            int key=Marshal.ReadInt32(l);
            int msg=w.ToInt32();bool down=msg==0x100||msg==0x104;
            if((key==0x53||key==StartKey||key==StopKey)&&down&&((ctrlHeld&&shiftHeld)||(Held(0x11)&&Held(0x10)))) {
                if(shortcutDown==0) {shortcutDown=key;BeginInvoke((Action)(()=>Shortcut(key)));}
                return (IntPtr)1;
            }
            if(shortcutDown==key) {if(!down)shortcutDown=0;return (IntPtr)1;}
        }
        return CallNextHookEx(hook,n,w,l);
    }
    ComboBox monitor = new ComboBox();
    NumericUpDown delay = new NumericUpDown();
    NumericUpDown interval = new NumericUpDown(),duration = new NumericUpDown();
    ComboBox startKey = new ComboBox(),stopKey = new ComboBox();
    Button start = new Button(),stop = new Button();
    Label automaticStatus = new Label();
    NotifyIcon tray = new NotifyIcon();
    System.Windows.Forms.Timer automaticTimer = new System.Windows.Forms.Timer();
    CaptureSchedule schedule = new CaptureSchedule();
    int StartKey { get { return 0x70+Math.Max(0,startKey.SelectedIndex); } }
    int StopKey { get { return 0x70+Math.Max(0,stopKey.SelectedIndex); } }
    Button choose = new Button(),capture = new Button();
    CheckBox send = new CheckBox();
    Label targetLabel = new Label(), status = new Label();
    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    System.Windows.Forms.Timer foregroundWatch = new System.Windows.Forms.Timer();
    IntPtr lastForeground;
    Screen[] screens;
    IntPtr target,source;
    string phase="";
    DateTime deadline;
    Rectangle bounds;
    bool busy,hotkey;
    string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenshotToChat");
    void Log(string text) { Directory.CreateDirectory(folder); File.AppendAllText(Path.Combine(folder,"activity.log"),DateTime.Now.ToString("s")+" "+text+Environment.NewLine); }
    void Status(string text) { status.Text=text; Log(text); }
    Label Label(string text,int x,int y,int w,int h) { var l=new Label {Text=text}; l.SetBounds(x,y,w,h); Controls.Add(l); return l; }
    ScreenshotChat() {
        Text="Screenshot to Chat"; ClientSize=new Size(560,620); Font=new Font("Segoe UI",10);
        FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen;
        Label("Capture a monitor or window. The pointer stays stationary.",20,18,520,28);
        Label("Capture source",20,60,150,25); monitor.DropDownStyle=ComboBoxStyle.DropDownList; monitor.SetBounds(180,57,285,30); monitor.DropDownWidth=650;Controls.Add(monitor);
        refreshSources.Text="Refresh";refreshSources.SetBounds(470,56,70,30);Controls.Add(refreshSources);refreshSources.Click+=(s,e)=>RefreshSources();
        RefreshSources();
        Label("Capture delay (seconds)",20,101,160,25); delay.SetBounds(180,98,70,28); delay.Maximum=60; Controls.Add(delay);
        send.Text="Paste and press Enter automatically"; send.Checked=true; send.SetBounds(20,139,520,28); Controls.Add(send);
        choose.Text="Select chat message box"; choose.SetBounds(20,181,210,36); Controls.Add(choose);
        targetLabel.Text="No chat selected"; targetLabel.SetBounds(242,183,298,40); Controls.Add(targetLabel);
        capture.Text="Capture and send (Ctrl + Shift + S)"; capture.SetBounds(20,236,520,44); Controls.Add(capture);
        Label("Automatic capture",20,300,520,25);
        Label("Interval (seconds)",20,336,160,25);interval.SetBounds(180,333,80,28);interval.Minimum=5;interval.Maximum=3600;interval.Value=10;Controls.Add(interval);
        Label("Stop after (minutes)",285,336,160,25);duration.SetBounds(450,333,80,28);duration.Maximum=1440;Controls.Add(duration);
        Label("0 minutes = run until stopped. No overlapping captures.",20,371,520,25);
        Label("Start: Ctrl + Shift +",20,408,150,25);startKey.SetBounds(180,405,80,28);startKey.DropDownStyle=ComboBoxStyle.DropDownList;Controls.Add(startKey);
        Label("Stop: Ctrl + Shift +",285,408,160,25);stopKey.SetBounds(450,405,80,28);stopKey.DropDownStyle=ComboBoxStyle.DropDownList;Controls.Add(stopKey);
        for(int i=1;i<=12;i++) {startKey.Items.Add("F"+i);stopKey.Items.Add("F"+i);}startKey.SelectedIndex=5;stopKey.SelectedIndex=6;
        startKey.SelectedIndexChanged+=(s,e)=>RegisterAutomaticKeys();stopKey.SelectedIndexChanged+=(s,e)=>RegisterAutomaticKeys();
        start.Text="Start automatic";start.SetBounds(20,450,250,36);Controls.Add(start);start.Click+=(s,e)=>StartAutomatic();
        stop.Text="Stop automatic";stop.SetBounds(285,450,250,36);Controls.Add(stop);stop.Click+=(s,e)=>StopAutomatic("Stopped by button");
        automaticStatus.SetBounds(20,497,520,25);automaticStatus.Text="Automatic: stopped";Controls.Add(automaticStatus);
        status.SetBounds(20,536,520,70); Controls.Add(status);
        choose.Click+=(s,e)=> { if(busy)return; busy=true; choose.Enabled=capture.Enabled=false; phase="select"; deadline=DateTime.Now.AddSeconds(30); Status("Click inside this chat's message box within 30 seconds."); WindowState=FormWindowState.Minimized; timer.Start(); };
        capture.Click+=(s,e)=>BeginCapture();
        automaticTimer.Interval=100;automaticTimer.Tick+=(s,e)=> {
            bool wasActive=schedule.Active;
            if(schedule.Poll(DateTime.UtcNow,busy))BeginCapture();
            if(wasActive&&!schedule.Active)StopAutomatic("Session timeout reached");
        };automaticTimer.Start();
        var menu=new ContextMenuStrip();menu.Items.Add("Show controls",null,(s,e)=> {Show();WindowState=FormWindowState.Normal;Activate();});
        menu.Items.Add("Start automatic",null,(s,e)=>StartAutomatic());menu.Items.Add("Stop automatic",null,(s,e)=>StopAutomatic("Stopped from tray"));menu.Items.Add("Exit",null,(s,e)=>Close());
        tray.Icon=SystemIcons.Application;tray.Text="Screenshot to Chat - stopped";tray.ContextMenuStrip=menu;tray.Visible=true;
        tray.DoubleClick+=(s,e)=> {Show();WindowState=FormWindowState.Normal;Activate();};
        timer.Interval=25; timer.Tick+=(s,e)=>Tick();
        Shown+=(s,e)=>Status(hotkey?"Ready. Select the chat message box once, then use Ctrl+Shift+S.":"Ctrl+Shift+S is unavailable. Close conflicting apps or use the button.");
        foregroundWatch.Interval=100;
        foregroundWatch.Tick+=(s,e)=> {
            IntPtr foreground=GetForegroundWindow();
            if(foreground!=lastForeground) {
                lastForeground=foreground;
                if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook);
                hook=SetWindowsHookEx(13,keyboardProc,GetModuleHandle(null),0);
            }
        };
        foregroundWatch.Start();RegisterAutomaticKeys();
        FormClosed+=(s,e)=>{schedule.Stop();timer.Dispose();foregroundWatch.Dispose();automaticTimer.Dispose();tray.Visible=false;tray.Dispose();};
    }
    void RegisterAutomaticKeys() {
        if(!IsHandleCreated)return;
        UnregisterHotKey(Handle,2);UnregisterHotKey(Handle,3);
        if(StartKey==StopKey) {Status("Choose different start and stop keys.");return;}
        bool a=RegisterHotKey(Handle,2,0x4006,(uint)StartKey),b=RegisterHotKey(Handle,3,0x4006,(uint)StopKey);
        if(!a||!b)Status("An automatic shortcut is in use. Choose other keys or use the buttons/tray.");
    }
    void Shortcut(int key) {if(key==StopKey)StopAutomatic("Stopped by shortcut");else if(key==StartKey)StartAutomatic();else if(key==0x53)BeginCapture();}
    void AutomaticControls(bool active) {
        interval.Enabled=duration.Enabled=startKey.Enabled=stopKey.Enabled=!active;
        start.Enabled=!active;choose.Enabled=!active&&!busy;
        automaticStatus.Text=active?"Automatic: running every "+interval.Value+" seconds":"Automatic: stopped";
        tray.Text=active?"Screenshot to Chat - RUNNING":"Screenshot to Chat - stopped";
        monitor.Enabled=refreshSources.Enabled=!active&&!busy;
    }
    void StartAutomatic() {
        if(schedule.Active)return;
        if(busy) {Status("Wait for the current capture to finish before starting automatic mode.");return;}
        if(StartKey==StopKey) {Status("Choose different start and stop keys.");return;}
        if(send.Checked&&!IsWindow(target)) {Status("Select the chat message box before starting automatic capture.");return;}
        schedule.Start(DateTime.UtcNow,(int)interval.Value,(int)duration.Value*60);AutomaticControls(true);
        Status("Automatic capture started. Stop with Ctrl+Shift+"+stopKey.Text+" or the tray menu.");WindowState=FormWindowState.Minimized;
    }
    void StopAutomatic(string reason) {
        schedule.Stop();
        bool pending=busy&&phase!="select";
        if(pending) {Finish();RestoreActivity();}
        AutomaticControls(false);Status(reason+(pending?". Pending capture/send cancelled; any pasted image remains in the composer.":"."));
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); hotkey=RegisterHotKey(Handle,1,0x4006,0x53);RegisterAutomaticKeys(); keyboardProc=Keyboard; hook=SetWindowsHookEx(13,keyboardProc,GetModuleHandle(null),0); hotkey=hotkey||hook!=IntPtr.Zero; }
    protected override void OnHandleDestroyed(EventArgs e) { UnregisterHotKey(Handle,1);UnregisterHotKey(Handle,2);UnregisterHotKey(Handle,3); if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook); base.OnHandleDestroyed(e); }
    protected override void WndProc(ref Message m) { if(m.Msg==0x312) {int id=m.WParam.ToInt32();if(id==1)BeginCapture();else if(id==2)StartAutomatic();else if(id==3)StopAutomatic("Stopped by shortcut");} base.WndProc(ref m); }
    bool Held(int key) { return (GetAsyncKeyState(key)&0x8000)!=0; }
    void Finish() {timer.Stop();phase="";busy=false;choose.Enabled=!schedule.Active;capture.Enabled=true;monitor.Enabled=refreshSources.Enabled=!schedule.Active;}
    void Next(string name,int milliseconds) {phase=name;deadline=DateTime.Now.AddMilliseconds(milliseconds);timer.Start();}
    void BeginCapture() {
        if(busy)return;
        if(send.Checked&&!IsWindow(target)) {if(schedule.Active)StopAutomatic("Chat closed; automatic capture stopped");else Status("Select the chat message box first.");return;}
        if(monitor.SelectedItem==null) {Status("Choose a capture source first.");return;}
        source=GetForegroundWindow();selectedSource=(CaptureSource)monitor.SelectedItem;
        busy=true;choose.Enabled=capture.Enabled=false;
        monitor.Enabled=refreshSources.Enabled=false;
        Log("Capture requested: "+selectedSource.Name);
        WindowState=FormWindowState.Minimized; Next("capture",350+(int)delay.Value*1000);
    }
    void RestoreActivity() {
        if(source!=Handle&&source!=target&&IsWindow(source))SetForegroundWindow(source);
    }
    void Tick() {
        try {
            if(phase=="select") {
                IntPtr h=GetForegroundWindow();
                if(h!=Handle&&h!=IntPtr.Zero&&(GetAsyncKeyState(1)&0x8001)!=0) {
                    target=h;Next("rememberInput",300);return;

                }
            }
            if(DateTime.Now<deadline)return;
            if(Held(0x10)||Held(0x11)||Held(0x12))return;
            timer.Stop();
            if(phase=="select")throw new Exception("No message box clicked. Try Select chat message box again.");
            if(phase=="rememberInput") {
                if(GetForegroundWindow()!=target)throw new Exception("Keep the chat message box selected until selection completes.");
                chatInput=AutomationElement.FocusedElement;
                if(chatInput==null||chatInput.Current.ProcessId!=AutomationElement.FromHandle(target).Current.ProcessId)throw new Exception("Could not identify the chat input. Click directly in the editable message box.");
                Log("Chat input selected: "+chatInput.Current.ControlType.ProgrammaticName);
                var title=new StringBuilder(512);GetWindowText(target,title,512);targetLabel.Text=title.ToString();
                Status("Message box selected. Use Ctrl+Shift+S from your activity.");Finish();WindowState=FormWindowState.Normal;return;
            }
            if(phase=="capture") {
                using(var bitmap=CaptureBitmap()) {
                    using(var png=new MemoryStream()) {
                        bitmap.Save(png,ImageFormat.Png); png.Position=0;
                        var data=new DataObject();data.SetData("PNG",false,png);data.SetImage(bitmap);
                        Clipboard.SetDataObject(data,true,5,100);
                    }
                }
                Log("Clipboard image ready: "+bounds.Width+"x"+bounds.Height);
                if(!send.Checked) {Status("Screenshot copied. Paste with Ctrl+V.");Finish();if(!schedule.Active)WindowState=FormWindowState.Normal;return;}
                if(!IsWindow(target))throw new Exception("Chat closed. Screenshot is in clipboard.");
                if(IsIconic(target))ShowWindow(target,9);
                FocusChat();Next("focus",300);return;
            }
            if(GetForegroundWindow()!=target)throw new Exception("Chat could not be focused. Screenshot is in clipboard; paste manually.");
            if(phase=="focus") {
                if(chatInput==null)throw new Exception("Select the chat message box again.");
                Log("Focusing chat input: "+chatInput.Current.ControlType.ProgrammaticName);chatInput.SetFocus();
                Next("paste",200);return;
            }
            if(phase=="paste") {
                if(!chatInput.Current.HasKeyboardFocus)throw new Exception("Chat input could not be focused without a mouse click. Select the editable message box again.");
                SendKeys.SendWait("^v");Log("Paste sent to chat composer (no pointer movement)");Next("send",2500);return;
            }
            if(phase=="send") {SendKeys.SendWait("{ENTER}");Log("Enter sent to chat");Next("restore",300);return;}
            if(phase=="restore") {RestoreActivity();Status("Capture pasted and Enter pressed. Activity focus restored.");Finish();}
        } catch(Exception ex) {schedule.Stop();AutomaticControls(false);Status(ex.Message+" Automatic capture stopped.");Finish();WindowState=FormWindowState.Normal;}
    }
    [STAThread] static void Main() {
        bool created;
        using(var mutex=new Mutex(true,"Local\\ScreenshotToChat.SingleInstance",out created)) {
            if(!created)return;
            SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ScreenshotChat());
        }
    }
}
