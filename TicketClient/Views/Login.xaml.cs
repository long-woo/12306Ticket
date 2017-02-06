using System;
using System.Collections.Generic;
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
using TicketClient.Helpers;

namespace TicketClient.Views
{
    /// <summary>
    /// Login.xaml 的交互逻辑
    /// </summary>
    public partial class Login : UserControl
    {
        public Login()
        {
            InitializeComponent();
        }

        // 记住我
        private void chkRememberMe_Click(object sender, RoutedEventArgs e)
        {
            if (!(bool)chkRememberMe.IsChecked)
            {
                chkAutoLogin.IsChecked = false;
            }
        }

        // 自动登录
        private void chkAutoLogin_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)chkAutoLogin.IsChecked)
            {
                chkRememberMe.IsChecked = true;
            }
        }

    }
}
