using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Forms.Integration;
using System.Diagnostics;

namespace ColorStealer
{

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int x;
        public int y;
    }

    public partial class MainWindow : Window
    {

        WindowsFormsHost host = new WindowsFormsHost();
        Timer t = new Timer();


        //Mouse&ColorHook
        #region M&CHook
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(ref Point lpPoint);
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hwnd, IntPtr hDC);
        [DllImport("gdi32.dll")]
        public static extern uint GetPixel(IntPtr hDC, int x, int y);
        IntPtr NeedHandle = IntPtr.Zero;
        #endregion

        //KeyboardHook
        #region KBDHook
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13; //Число(ID) для хука 
        private const int WM_KEYDOWN = 0x100; //Событие нажатия
        private static LowLevelKeyboardProc process = HookCallback; //вызов после срабатывания
        private static IntPtr HookNum = IntPtr.Zero;
        #endregion

        //MagikKeyBinding
        #region KeyHookSettings
        private static IntPtr SetHook(LowLevelKeyboardProc proc) 
        {
            using (Process Proc = Process.GetCurrentProcess()) //Процесс
            using (ProcessModule Module = Proc.MainModule) //Модуль процесса(ProcessModuleCollection)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(Module.ModuleName), 0); //(дескриптор процесса, индекс)
            }
        }
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam); //Ввод тип с клавиатуры в поток

        static int Step = 0;
        static int StepUpDown = 0;

        private static IntPtr HookCallback(int sKey, IntPtr wParam, IntPtr lParam) //то что происходит после срабатывания
        {
            if (sKey >= 0 && wParam == (IntPtr)WM_KEYDOWN)        
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if ((Keys)vkCode == Keys.Add) //Если нужна комбинация использовать это 0x** && GetAsyncKeyState(0x**) != 0
                {
                    CopyToBuffer();
                }
                if ((Keys)vkCode == Keys.Subtract) 
                {
                    Environment.Exit(0); //Закрыть
                }
                if ((Keys)vkCode == Keys.NumPad5)
                {                        
                    LightMode();//Замедлить
                }
                if ((Keys)vkCode == Keys.NumPad4)
                {                           
                    Step--; //Сдвиг влево
                }
                if ((Keys)vkCode == Keys.NumPad6)
                {                             
                    Step++; //Сдвиг вправо
                }
                if ((Keys)vkCode == Keys.NumPad2)
                {                            
                    StepUpDown++; //Сдвиг вниз
                }
                if ((Keys)vkCode == Keys.NumPad8)
                {                            
                    StepUpDown--; //Сдвиг вверх
                }
                if ((Keys)vkCode == Keys.Divide)
                {                       
                    StepUpDown = 0;
                    Step = 0;
                }

            }
            return CallNextHookEx(HookNum, sKey, wParam, lParam); //Далее
        }
        static string buffer = ""; //специально для статичных методов статичный тип 
        #endregion

        //ToGlobalBuffer
        #region Func
        public static void CopyToBuffer()                                           
        {                                                                            
                 System.Windows.Clipboard.Clear();                                  
                 buffer = buffer.Substring(3);                                        
                 System.Windows.Clipboard.SetText(buffer);                             
                 Debug.WriteLine(buffer);                                              
        }                                                                              
        #endregion                                                                   
                                                                                      
                                                                                      
        public MainWindow()                                                          
        {
            InitializeComponent();
            HookNum = SetHook(process); //Запуск глобал хука
            Rect primaryMonitorArea = SystemParameters.WorkArea;
            Left = primaryMonitorArea.Top;
            Top = 0;
            t.Interval = 50 + delay; //Каждые 50 милисисекунд отрабатываю метод нахождения позиции и цвета
            t.Start(); //запуск
            t.Tick += MouseCheck;
        }

        //Positions&Colors
        #region PositionAndColors
        public void MouseCheck(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                t.Interval = 50 + delay;
                Point point = new Point();
                GetCursorPos(ref point);
                XLabel.Content = point.x.ToString() + " + " + Step.ToString();
                YLabel.Content = point.y.ToString() + " + " + StepUpDown.ToString();
                Debug.WriteLine(point.x.ToString());
                Debug.WriteLine(point.y.ToString());
                string colors = Colorize(point.x+Step, point.y+StepUpDown, NeedHandle).ToString();
                Debug.WriteLine(colors);
                ColorLabel.Content = colors;
                buffer = colors;
                ColorViewer.Fill = new SolidColorBrush(Colorize(point.x + Step, point.y+StepUpDown, NeedHandle));
            }));
        }

        static int LightState = 0;
        static int delay = 0;

        public static void LightMode()
        {
            if(LightState == 0)
             {
                 LightState = 1;
                 delay = 500;
             }
             else
             {
                 LightState = 0;
                 delay = 0;
             }
        }

        public Color Colorize(int x, int y, IntPtr wndHandle)
        {
            byte r, g, b;
            IntPtr hDC = GetDC(wndHandle);
            uint pixel = GetPixel(hDC, x, y);
            ReleaseDC(IntPtr.Zero, hDC);
            //==============================
            r = (byte)(pixel & 0x000000FF);
            g = (byte)((pixel & 0x0000FF00) >> 8);
            b = (byte)((pixel & 0x00FF0000) >> 16);
            Color color = Color.FromRgb(r, g, b);
            return color;
        }
        #endregion
    }
}
