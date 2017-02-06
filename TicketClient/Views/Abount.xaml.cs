using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TicketClient.Views
{
    /// <summary>
    /// Abount.xaml 的交互逻辑
    /// </summary>
    public partial class Abount : UserControl
    {
        public Abount()
        {
            InitializeComponent();
        }

        // 打开github
        private void github_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://github.com/woo-long/12306-for-pc");
        }
    }
}
