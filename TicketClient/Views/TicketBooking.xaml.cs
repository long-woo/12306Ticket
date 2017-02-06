using System;
using System.Collections.Generic;
using System.Data;
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
    /// TicketBooking.xaml 的交互逻辑
    /// </summary>
    public partial class TicketBooking : UserControl
    {
        public TicketBooking()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 获取站名，并绑定
        /// </summary>
        /// <param name="cmbControl">控件的名称</param>
        /// <param name="stationName">站名、简拼、全拼</param>
        /// <param name="key">按下的键</param>
        private async void GetStationNames(ComboBox cmbControl, string stationName, Key key)
        {
            if (key != Key.Up && key != Key.Down)
            {
                dynamic isExistStation = null;

                if (cmbControl.ItemsSource != null)
                {
                    isExistStation = (from s in cmbControl.ItemsSource as List<dynamic>
                                      where s.ZHName.ToString().Contains(stationName) || s.FullPY.ToString().Contains(stationName) || s.FirstPY.ToString().Contains(stationName)
                                      select s).FirstOrDefault();
                }

                if (!string.IsNullOrEmpty(stationName) && isExistStation == null)
                {
                    cmbControl.ItemsSource = null; // 清空项
                    List<dynamic> lstCitys = await TicketHelpers.GetStationNamesAsync(stationName);
                    cmbControl.ItemsSource = lstCitys;
                    cmbControl.DisplayMemberPath = "ZHName";
                    cmbControl.SelectedValuePath = "Code";
                }

                cmbControl.IsDropDownOpen = true;
            }
        }

        #region 事件

        // 出发地
        private void txtFromCity_KeyUp(object sender, KeyEventArgs e)
        {
            GetStationNames(txtFromCity, txtFromCity.Text, e.Key);
        }

        // 出发地展开
        private void txtFromCity_DropDownOpened(object sender, EventArgs e)
        {
            txtFromCity.SelectedIndex = 0;
        }

        // 目的地
        private void txtToCity_KeyUp(object sender, KeyEventArgs e)
        {
            GetStationNames(txtToCity, txtToCity.Text, e.Key);
        }

        // 目的地展开
        private void txtToCity_DropDownOpened(object sender, EventArgs e)
        {
            txtToCity.SelectedIndex = 0;
        }

        // 切换地址
        private void btnChangeCity_Click(object sender, RoutedEventArgs e)
        {
            string startStation = txtFromCity.Text.ToString();

            if (string.IsNullOrEmpty(startStation))
            {
                return;
            }

            string startStationCode = txtFromCity.SelectedValue.ToString();
            var lstFormStations = txtFromCity.ItemsSource as List<dynamic>;
            string endStation = txtToCity.Text.ToString();

            if (string.IsNullOrEmpty(endStation))
            {
                return;
            }

            string endStationCode = txtToCity.SelectedValue.ToString();
            var lstToStations = txtToCity.ItemsSource as List<dynamic>;

            txtFromCity.Text = endStation;
            txtToCity.Text = startStation;
            txtFromCity.ItemsSource = lstToStations;
            txtFromCity.DisplayMemberPath = "ZHName";
            txtFromCity.SelectedValuePath = "Code";
            txtFromCity.SelectedValue = endStationCode;
            txtToCity.ItemsSource = lstFormStations;
            txtToCity.DisplayMemberPath = "ZHName";
            txtToCity.SelectedValuePath = "Code";
            txtToCity.SelectedValue = startStationCode;
        }

        // 日期减
        private void btnPrevDate_Click(object sender, RoutedEventArgs e)
        {
            var date = DateTime.Parse(txtDate.Text).AddDays(-1);
            if (date <= txtDate.DisplayDateStart)
            {
                btnPrevDate.IsEnabled = false;
            }
            txtDate.Text = date.ToString();
            btnNextDate.IsEnabled = true;
        }

        // 日期加
        private void btnNextDate_Click(object sender, RoutedEventArgs e)
        {
            var date = DateTime.Parse(txtDate.Text).AddDays(1);
            if (date >= txtDate.DisplayDateEnd)
            {
                btnNextDate.IsEnabled = false;
                date = date.AddDays(-1);
            }
            txtDate.Text = date.ToString();
            btnPrevDate.IsEnabled = true;
        }

        // 加载行
        private void gridTrainList_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            dynamic item = e.Row.DataContext;

            if (!(bool)item.IsCanBuy)
            {
                e.Row.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        #endregion
    }
}
