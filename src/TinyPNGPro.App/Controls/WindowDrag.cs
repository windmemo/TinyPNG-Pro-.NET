using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TinyPNGPro.App.Controls
{
    /// <summary>
    /// 无边框窗口拖动：左键拖动非交互区域，中键可在任意位置拖动。
    /// 交互控件（按钮、输入框、列表等）保留自身行为，中键也不影响左键点击。
    /// </summary>
    public static class WindowDrag
    {
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        private const int DragThreshold = 4;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static Form _form;
        private static Control _source;
        private static Point _downScreen;
        private static Point _origin;
        private static bool _leftDown;
        private static bool _leftDrag;
        private static bool _middleDown;
        private static bool _middleDrag;

        public static void Attach(Form form)
        {
            if (form == null)
            {
                return;
            }
            _form = form;
            Walk(form, true);
        }

        private static void Walk(Control control, bool allowLeft)
        {
            if (allowLeft)
            {
                control.MouseDown += OnLeftDown;
                control.MouseMove += OnLeftMove;
                control.MouseUp += OnLeftUp;
            }
            control.MouseDown += OnMiddleDown;
            control.MouseMove += OnMiddleMove;
            control.MouseUp += OnMiddleUp;

            for (int i = 0; i < control.Controls.Count; i++)
            {
                Walk(control.Controls[i], allowLeft && !IsInteractive(control.Controls[i]));
            }
        }

        private static bool IsInteractive(Control control)
        {
            return control is TextBoxBase || control is ComboBox || control is ListBox ||
                   control is ButtonBase || control is NumericUpDown ||
                   control is RoundedButton || control is PillToggle || control is ScrollBar;
        }

        private static void OnLeftDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _form == null)
            {
                return;
            }
            _source = (Control)sender;
            _leftDown = true;
            _leftDrag = false;
            _downScreen = _source.PointToScreen(e.Location);
            _source.Capture = true;
        }

        private static void OnLeftMove(object sender, MouseEventArgs e)
        {
            if (!_leftDown || _leftDrag || _form == null)
            {
                return;
            }
            Control control = (Control)sender;
            Point now = control.PointToScreen(e.Location);
            if (Math.Abs(now.X - _downScreen.X) < DragThreshold &&
                Math.Abs(now.Y - _downScreen.Y) < DragThreshold)
            {
                return;
            }
            _leftDrag = true;
            _leftDown = false;
            ReleaseCapture();
            SendMessage(_form.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        private static void OnLeftUp(object sender, MouseEventArgs e)
        {
            _leftDown = false;
            _leftDrag = false;
            Control control = (Control)sender;
            if (control.Capture)
            {
                control.Capture = false;
            }
        }

        private static void OnMiddleDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Middle || _form == null)
            {
                return;
            }
            _source = (Control)sender;
            _middleDown = true;
            _middleDrag = false;
            _downScreen = _source.PointToScreen(e.Location);
            _origin = _form.Location;
            _source.Capture = true;
        }

        private static void OnMiddleMove(object sender, MouseEventArgs e)
        {
            if (!_middleDown || _form == null)
            {
                return;
            }
            Control control = (Control)sender;
            Point now = control.PointToScreen(e.Location);
            int dx = now.X - _downScreen.X;
            int dy = now.Y - _downScreen.Y;
            if (!_middleDrag)
            {
                if (Math.Abs(dx) < DragThreshold && Math.Abs(dy) < DragThreshold)
                {
                    return;
                }
                _middleDrag = true;
            }
            _form.Location = new Point(_origin.X + dx, _origin.Y + dy);
        }

        private static void OnMiddleUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Middle)
            {
                return;
            }
            _middleDown = false;
            _middleDrag = false;
            Control control = (Control)sender;
            if (control.Capture)
            {
                control.Capture = false;
            }
        }
    }
}
