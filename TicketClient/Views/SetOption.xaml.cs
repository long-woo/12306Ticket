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
using TicketClient.Properties;

namespace TicketClient.Views
{
    /// <summary>
    /// SetOption.xaml 的交互逻辑
    /// </summary>
    public partial class SetOption : UserControl
    {
        public SetOption()
        {
            InitializeComponent();
        }

        // 开启语言提示
        private void tgsSpeech_Checked(object sender, RoutedEventArgs e)
        {
            SaveSpeechConfig(true);
        }

        // 关闭语言提示
        private void tgsSpeech_Unchecked(object sender, RoutedEventArgs e)
        {
            SaveSpeechConfig(false);
        }

        /// <summary>
        /// 保存语音配置
        /// </summary>
        /// <param name="isSpeech"></param>
        private void SaveSpeechConfig(bool isSpeech)
        {
            Settings.Default.IsSpeech = isSpeech;
            Settings.Default.Save();
        }
    }
}
