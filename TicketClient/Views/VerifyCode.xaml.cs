using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace TicketClient.Views
{
    /// <summary>
    /// VerifyCode.xaml 的交互逻辑
    /// </summary>
    public partial class VerifyCode : UserControl
    {
        public VerifyCode()
        {
            InitializeComponent();
        }

        // 左击选择
        private void canvVerifyCode_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition((IInputElement)sender);
            int px = Convert.ToInt32(p.X), py = Convert.ToInt32(p.Y);
            BitmapSource bitChkImg = Imaging.CreateBitmapSourceFromHBitmap(TicketClient.Properties.Resources.check.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            Image checkImg = new Image();
            checkImg.ToolTip = "右击取消选择";
            checkImg.Source = bitChkImg;
            checkImg.Tag = px + "," + (py - 31);
            checkImg.MouseRightButtonUp += checkImg_MouseRightButtonUp;
            Canvas.SetLeft(checkImg, px - bitChkImg.Width / 2);
            Canvas.SetTop(checkImg, py - bitChkImg.Height / 2);
            canvVerifyCode.Children.Add(checkImg);
            string codeXY = hidVerifyCodes.Text + ',' + px + ',' + (py - 31);
            hidVerifyCodes.Text = codeXY.TrimStart(',');
        }

        // 右击图片撤销选择
        void checkImg_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var imgCheck = sender as Image;
            string strChecks = hidVerifyCodes.Text.ToString().Replace(imgCheck.Tag.ToString(), "");
            hidVerifyCodes.Text = "";
            string strChkCodes = "";
            var arrChecks = strChecks.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in arrChecks)
            {
                strChkCodes += item + ',';
            }
            strChkCodes = strChkCodes.Trim(',');
            hidVerifyCodes.Text = strChkCodes;
            canvVerifyCode.Children.Remove(imgCheck);
        }
    }
}
