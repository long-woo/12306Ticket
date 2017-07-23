using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TicketClient.Helpers;
using System.Windows.Markup;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Net;
using MahApps.Metro.Controls.Dialogs;
using System.Diagnostics;
using TicketClient.Properties;
using System.Web;

namespace TicketClient
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        System.Windows.Forms.NotifyIcon notifyIcon = new System.Windows.Forms.NotifyIcon();

        //public static RoutedCommand VerifyCmd = new RoutedCommand("VerifyCmd", typeof(MainWindow)); // 验证命令
        private DispatcherTimer disTimer = new DispatcherTimer();
        double queryTime = 1.0; // 查询等待时间，以毫秒为单位
        int queryCount = 1; // 查询计数器

        public MainWindow()
        {
            InitializeComponent();

            disTimer.Tick += disTimer_Tick;
            disTimer.Interval = TimeSpan.FromMilliseconds(100);

            // 对话框基本设置
            MetroDialogOptions.AffirmativeButtonText = "确定";
            MetroDialogOptions.NegativeButtonText = "取消";
            MetroDialogOptions.ColorScheme = MetroDialogColorScheme.Accented;

            // 用户控件 Login
            Button btnLogin = viewLogin.FindName("btnLogin") as Button;
            btnLogin.Click += btnLogin_Click;
            viewLogin.txtUserName.SelectionChanged += txtUserName_SelectionChanged;

            // 用户控件 VerifyCode
            viewVerifyCode.btnCodeValidate.Click += btnCodeValidate_Click;
            viewVerifyCode.linkChange.Click += linkChange_Click;
            viewVerifyCode.btnClose.Click += btnClose_Click;

            // 用户控件 TicketBooking
            viewTicketBooking.btnQueryTicket.Click += btnQueryTicket_Click;
            viewTicketBooking.linkRefresh.Click += linkRefresh_Click;
            viewTicketBooking.gridTrainList.SelectionChanged += gridTrainList_SelectionChanged;
            viewTicketBooking.btnConfirmTask.Click += btnConfirmTask_Click;

            // 用户控件 Abount
            viewAbout.btnAbountClose.Click += btnAbountClose_Click;
        }

        #region 用户控件 事件

        // 更换用户
        async void txtUserName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cmbUserName = viewLogin.txtUserName;
            PasswordBox txtPassword = viewLogin.txtPassword;
            CheckBox chkRemeberMe = viewLogin.chkRememberMe,
                chkAutoLogin = viewLogin.chkAutoLogin;

            if (cmbUserName.SelectedIndex > -1)
            {
                progressRingAnima.IsActive = true;
                List<dynamic> lstUsers = cmbUserName.ItemsSource as List<dynamic>;
                var user = (from u in lstUsers
                            where u.name == cmbUserName.SelectedValue.ToString()
                            select u).FirstOrDefault();

                if (user != null)
                {
                    string userPassword = await TicketHelpers.DecryptAsync(user.password.ToString());
                    txtPassword.Password = userPassword;
                    chkRemeberMe.IsChecked = true;
                    chkAutoLogin.IsChecked = (bool)user.isAutoLogin;

                    if ((bool)viewLogin.chkAutoLogin.IsChecked)
                    {
                        // 自动登录
                        btnLogin_Click(sender, e);
                    }
                }

                progressRingAnima.IsActive = false;
            }
            else
            {
                txtPassword.Password = "";
                chkRemeberMe.IsChecked = false;
                chkAutoLogin.IsChecked = false;
            }
        }

        // 登录
        async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(viewLogin.txtUserName.Text))
            {
                staInfo.Content = "用户名不能为空";
                return;
            }

            if (string.IsNullOrEmpty(viewLogin.txtPassword.Password))
            {
                staInfo.Content = "密码不能为空";
                return;
            }

            progressRingAnima.IsActive = true;
            staInfo.Content = "正在登录...";
            OpenLoginPopup(false);
            viewVerifyCode.lblCodeHead.Content = viewLogin.txtUserName.Text;
            OpenVerifyCodePopup(true, true);
            await RefreshVerifyCode(0);
            ShowSystemNotice("12306助手", "选择验证码", 5000);
            staInfo.Content = "选择验证码";
            progressRingAnima.IsActive = false;
        }

        // 刷新验证码图片
        async void linkChange_Click(object sender, RoutedEventArgs e)
        {
            progressRingAnima.IsActive = true;
            string codeType = viewVerifyCode.linkChange.Tag.ToString();
            int code = codeType == "O-C" ? 1 : 0;
            await RefreshVerifyCode(code);
            progressRingAnima.IsActive = false;
        }

        // 校验验证码
        async void btnCodeValidate_Click(object sender, RoutedEventArgs e)
        {
            progressRingAnima.IsActive = true;
            viewVerifyCode.IsEnabled = false;
            staInfo.Content = "正在校验验证码...";
            string verifyCodes = viewVerifyCode.hidVerifyCodes.Text;
            string chkeckType = viewVerifyCode.btnCodeValidate.Tag.ToString();
            string randType = chkeckType == "O-V" ? "randp" : "sjrand";
            Dictionary<string, string> formParams = new Dictionary<string, string>()
            {
                {"randCode",verifyCodes},
                {"rand",randType}
            };
            bool checkResult = await TicketHelpers.CheckVerifyCodeAsync(formParams);
            int code = chkeckType == "O-V" ? 1 : 0;

            if (checkResult)
            {
                // 登录
                if (chkeckType == "L-V")
                {
                    staInfo.Content = "验证通过，正在登录...";
                    await VerificationLogin(verifyCodes, code);
                }

                // 提交订单
                if (chkeckType == "O-V")
                {
                    staInfo.Content = "验证通过，正在提交订单...";
                    await SubmitOrder(verifyCodes);
                }

                if (!"系统繁忙".Contains(staInfo.Content.ToString()))
                {
                    OpenVerifyCodePopup(false); // 关闭验证码选择框
                }
                progressRingAnima.IsActive = false;
            }
            else
            {
                progressRingAnima.IsActive = true;
                staInfo.Content = "验证码不正确";
                await RefreshVerifyCode(code);
            }

            viewVerifyCode.IsEnabled = true;
            progressRingAnima.IsActive = false;
        }

        // 关闭验证码选择框
        void btnClose_Click(object sender, RoutedEventArgs e)
        {
            OpenVerifyCodePopup(false);
            string chkeckType = viewVerifyCode.btnCodeValidate.Tag.ToString();

            // 显示登录框
            if (chkeckType == "L-V")
            {
                OpenLoginPopup(true, true);
            }
        }

        // 查询车次
        async void btnQueryTicket_Click(object sender, RoutedEventArgs e)
        {
            ComboBox txtFromCity = viewTicketBooking.txtFromCity,
                txtToCity = viewTicketBooking.txtToCity;
            DatePicker txtDate = viewTicketBooking.txtDate;

            string fromName = txtFromCity.Text,
                   toName = txtToCity.Text,
                   trainDate = txtDate.Text;


            if (string.IsNullOrEmpty(fromName))
            {
                staInfo.Content = "出发地不能为空";
                return;
            }

            if (string.IsNullOrEmpty(toName))
            {
                staInfo.Content = "目的地不能为空";
                return;
            }

            if (string.IsNullOrEmpty(trainDate))
            {
                staInfo.Content = "乘车日期不能为空";
                return;
            }

            string fromCode = txtFromCity.SelectedValue.ToString(),
                   toCode = txtToCity.SelectedValue.ToString();

            SaveSearchConfig(fromName, fromCode, toName, toCode, trainDate);
            viewTicketBooking.IsEnabled = false;

            progressRingAnima.IsActive = true;
            var lstTrains = await GetTrain(txtFromCity, txtToCity, txtDate);

            ListCollectionView collectionView = new ListCollectionView(lstTrains);
            collectionView.GroupDescriptions.Add(new PropertyGroupDescription("CanBuyDescript"));
            viewTicketBooking.gridTrainList.ItemsSource = collectionView;
            staInfo.Content = string.Format("查询完成！{0}→{1}共【{2}】趟列车", txtFromCity.Text, txtToCity.Text, lstTrains.Count());
            viewTicketBooking.IsEnabled = true;
            progressRingAnima.IsActive = false;
        }

        // 刷新乘客
        async void linkRefresh_Click(object sender, RoutedEventArgs e)
        {
            progressRingAnima.IsActive = true;
            await GetContacts();
            progressRingAnima.IsActive = false;
        }

        // 更改选中车次
        void gridTrainList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            dynamic lstSelTrainItems = viewTicketBooking.gridTrainList.SelectedItems;

            if (lstSelTrainItems.Count > 5)
            {
                staInfo.Content = "选中的车次不能超过5趟";
                var dgCurrentRow = viewTicketBooking.gridTrainList.ItemContainerGenerator.ContainerFromItem(viewTicketBooking.gridTrainList.CurrentItem) as DataGridRow;
                dgCurrentRow.IsSelected = false;
                //dgCurrentRow.Foreground = new SolidColorBrush(Colors.Black);
                return;
            }

            // 绑定席别
            Dictionary<string, string> dicSeatTypes = new Dictionary<string, string>();
            string bookingTrain = "";
            // seatTypeText = "";
            foreach (var item in lstSelTrainItems)
            {
                bookingTrain += string.Format("{0},", item.TrainCode);
                foreach (var seat in item.SeatInfo)
                {
                    if (!dicSeatTypes.ContainsKey(seat.SeatTypeCode))
                    {
                        //seatTypeText = string.Format("{0}（{1}）", seat.SeatTypeName, seat.SeatCount);
                        dicSeatTypes.Add(seat.SeatTypeCode, seat.SeatTypeName);
                    }
                }
            }
            lblBookingTrain.Tag = bookingTrain.TrimEnd(','); // 预订的车次
            int sRow = (int)Math.Ceiling((double)dicSeatTypes.Count() / 9), sCell = 9;
            while (sRow-- > 0)
            {
                viewTicketBooking.gridSeatTypes.RowDefinitions.Add(new RowDefinition()
                {
                    Height = new GridLength(20)
                });
            }
            while (sCell-- > 0)
            {
                viewTicketBooking.gridSeatTypes.ColumnDefinitions.Add(new ColumnDefinition()
                {
                    Width = new GridLength()
                });
            }
            viewTicketBooking.gridSeatTypes.Children.Clear();
            int sR = 0, sC = 0, sT = 0;
            foreach (var d in dicSeatTypes)
            {
                CheckBox chkSeatType = new CheckBox()
                {
                    Height = 18,
                    Name = "chk" + d.Key,
                    Content = d.Value,
                    Tag = string.Format("{0}_", d.Key)
                };
                chkSeatType.Click += chkSeatType_Click;
                viewTicketBooking.gridSeatTypes.Children.Add(chkSeatType);
                if (sT > 0)
                {
                    if (sT % 9 == 0)
                    {
                        sR += 1;
                        sC = 0;
                    }
                    else
                    {
                        sC++;
                    }
                }
                chkSeatType.SetValue(Grid.RowProperty, sR);
                chkSeatType.SetValue(Grid.ColumnProperty, sC);
                sT++;
            }
        }

        // 确认任务
        async void btnConfirmTask_Click(object sender, RoutedEventArgs e)
        {
            int chkContactCount = GetCheckedCount(viewTicketBooking.gridContacts); // 选中乘客个数

            if (chkContactCount < 1)
            {
                staInfo.Content = "还没有选择乘客";
                return;
            }

            int chkSeatCount = GetCheckedCount(viewTicketBooking.gridSeatTypes); // 选中席别个数

            if (chkSeatCount < 1)
            {
                staInfo.Content = "还没有选择席别";
                return;
            }

            lblChkSeatType.Tag = GetCheckedItems(viewTicketBooking.gridSeatTypes, true); // 选中的席别

            // 检查用户是否在线
            bool isOnline = await TicketHelpers.CheckUserIsOnline();

            if (!isOnline)
            {
                OpenLoginPopup(true);
                staInfo.Content = "用户未登录";
                ShowSystemNotice("12306助手", staInfo.Content.ToString(), 5000);
                return;
            }

            disTimer.Start(); // 启动计时器
        }

        // 关闭关于窗口
        private void btnAbountClose_Click(object sender, RoutedEventArgs e)
        {
            abountPopup.Visibility = Visibility.Hidden;
        }

        #endregion

        #region 通知区域图标

        // 初始化通知区域图标和右键菜单
        private void InitNotfyIconMenu()
        {
            System.Windows.Forms.ContextMenuStrip contextMenu = new System.Windows.Forms.ContextMenuStrip();

            var menuArrs = new string[] { "打开主界面", "-", "关于", "-", "退出" };

            for (int i = 0; i < menuArrs.Length; i++)
            {
                string item = menuArrs[i];
                if (item != "-")
                {
                    System.Windows.Forms.ToolStripMenuItem menuItem = new System.Windows.Forms.ToolStripMenuItem();
                    menuItem.Text = menuArrs[i];
                    menuItem.Click += menuItem_Click;

                    contextMenu.Items.Add(menuItem);
                }
                else
                {
                    contextMenu.Items.Add("-");
                }
            }
            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.Text = "12306助手，体验不一样的购票";
            notifyIcon.Icon = Properties.Resources.ic_train;
            notifyIcon.MouseClick += notifyIcon_MouseClick;
            notifyIcon.Visible = true;
        }

        private void menuItem_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.ToolStripMenuItem menuItem = sender as System.Windows.Forms.ToolStripMenuItem;

            switch (menuItem.Text)
            {
                case "打开主界面":
                    ShowMain();
                    break;
                case "关于":
                    ShowMain();
                    abountPopup.Visibility = Visibility.Visible;
                    break;
                case "退出":
                    ShowMain();
                    this.Close();
                    break;
            }
        }

        private void notifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                ShowMain();
            }
        }

        #endregion

        #region Command 事件

        // 命令是否可以执行
        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            //if (progressRingAnima.IsActive)
            //{
            //    e.CanExecute = false;
            //}
            //else
            //{
            //    e.CanExecute = true;
            //}
        }

        // 执行命令
        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {

        }

        #endregion

        /// <summary>
        /// 保存搜索记录
        /// </summary>
        /// <param name="fromName">出发地</param>
        /// <param name="fromCode">出发地Code</param>
        /// <param name="toName">目的地</param>
        /// <param name="toCode">目的地Code</param>
        /// <param name="trainDate">乘车日期</param>
        private void SaveSearchConfig(string fromName, string fromCode, string toName, string toCode, string trainDate)
        {
            Settings.Default.FromStationCode = fromCode;
            Settings.Default.FromStationName = fromName;
            Settings.Default.ToStationCode = toCode;
            Settings.Default.ToStationName = toName;
            Settings.Default.TrainDate = trainDate;
        }

        /// <summary>
        /// 显示窗口并置顶
        /// </summary>
        private void ShowMain()
        {
            this.Show();

            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }

            this.Topmost = true;
        }

        // 计时器执行事件
        async void disTimer_Tick(object sender, EventArgs e)
        {
            staInfo.Content = string.Format("{0}秒后，开始第【{1}】次查询...", queryTime, (queryCount));

            if (queryTime - 0.1 < 0.1)
            {
                disTimer.Stop(); // 查询到车次，停止计时器
                queryCount += 1; // 查询计数器，开始计数
                queryTime = 1.0;
                progressRingAnima.IsActive = true;
                viewTicketBooking.IsEnabled = false;

                List<dynamic> lstTrains = await GetTrain(viewTicketBooking.txtFromCity, viewTicketBooking.txtToCity, viewTicketBooking.txtDate, "Y");
                if (lstTrains.Count() > 0)
                {
                    string strChkSeats = GetCheckedItems(viewTicketBooking.gridSeatTypes, true); // 选中的席别
                    List<dynamic> lstChkTrains = (from t in lstTrains
                                                  where lblBookingTrain.Tag.ToString().Contains(t.TrainCode.ToString()) && !"无".Contains(GetSeatCountByCode(strChkSeats, t.SeatInfo))
                                                  select t).ToList(); // 选中的车次

                    if (lstChkTrains.Count() > 0)
                    {
                        staInfo.Content = string.Format("查询完成！共【{0}】趟可预订列车，准备开始任务", lstChkTrains.Count());
                        ShowSystemNotice("12306助手", staInfo.Content.ToString(), 5000);
                        await ConfirmStartTask(lstChkTrains, strChkSeats);
                    }
                    else
                    {
                        disTimer.Start(); // 若没查到车次，启动计时器
                    }
                }
                else
                {
                    disTimer.Start(); // 若没查到车次，启动计时器
                }

                viewTicketBooking.IsEnabled = true;
                progressRingAnima.IsActive = false;
            }
            else
            {
                queryTime -= 0.1;
            }
        }

        /// <summary>
        /// 获取座位票数
        /// </summary>
        /// <param name="seatCodes">座位代码</param>
        /// <param name="seatInfos">座位信息</param>
        /// <returns></returns>
        private string GetSeatCountByCode(string seatCodes, List<dynamic> seatInfos)
        {
            string count = "无";
            var strSeatArr = seatCodes.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < strSeatArr.Length; i++)
            {
                var seat = (from s in seatInfos
                            where s.SeatTypeCode.ToString() == strSeatArr[i]
                            select s).FirstOrDefault();

                count = "无*-".Contains(seat.SeatCount.ToString()) ? "无" : "有";
            }
            return count;
        }

        /// <summary>
        /// 显示系统通知
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="content">内容</param>
        /// <param name="timeOut">显示多久（单位为毫妙）</param>
        private void ShowSystemNotice(string title, string content, int timeOut)
        {
            notifyIcon.BalloonTipTitle = title;
            notifyIcon.BalloonTipText = content;
            notifyIcon.ShowBalloonTip(timeOut);
        }

        /// <summary>
        /// 是否打开登录框
        /// </summary>
        /// <param name="isOpen">true：打开，false：关闭</param>
        /// <param name="isVerifyCodeClose">是否由验证码关闭打开的</param>
        private async void OpenLoginPopup(bool isOpen, bool isVerifyCodeClose = false)
        {
            if (isOpen)
            {
                progressRingAnima.IsActive = true;
                await TicketHelpers.LogOffAsync();
                IsCMDLogin(true, "");
                loginPopup.Visibility = Visibility.Visible;
                gridPopup.Visibility = Visibility.Visible;
                // 绑定用户
                List<dynamic> lstUsers = await TicketHelpers.GetUserLoginInfoAsync();
                viewLogin.txtUserName.ItemsSource = lstUsers;
                viewLogin.txtUserName.DisplayMemberPath = "name";
                viewLogin.txtUserName.SelectedValuePath = "name";

                if (!isVerifyCodeClose)
                {
                    viewLogin.txtUserName.SelectedIndex = 0; // 选择第一个
                }

                progressRingAnima.IsActive = false;

                return;
            }

            loginPopup.Visibility = Visibility.Hidden;
            gridPopup.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// 是否打开验证码选择框
        /// </summary>
        /// <param name="isOpen">true：打开，false：关闭</param>
        /// <param name="isLogin">isOpen为false，该参数忽略；否则，true：登录；false：订单</param>
        private void OpenVerifyCodePopup(bool isOpen, bool isLogin = true)
        {
            if (isOpen)
            {
                verifyCodePopup.Visibility = Visibility.Visible;
                gridPopup.Visibility = Visibility.Visible;

                if (isLogin)
                {
                    viewVerifyCode.linkChange.Tag = "L-C";
                    viewVerifyCode.btnCodeValidate.Tag = "L-V";
                    return;
                }

                viewVerifyCode.linkChange.Tag = "O-C";
                viewVerifyCode.btnCodeValidate.Tag = "O-V";
                return;
            }

            verifyCodePopup.Visibility = Visibility.Hidden;
            gridPopup.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// 刷新验证码图片
        /// </summary>
        /// <param name="code">验证码类型（1：订单；其他：登录）</param>
        /// <returns></returns>
        private async Task RefreshVerifyCode(int code)
        {
            viewVerifyCode.canvVerifyCode.Children.Clear();
            viewVerifyCode.hidVerifyCodes.Text = "";
            var bmp = await TicketHelpers.GetVerifyCodeImageAsync(code);
            viewVerifyCode.imgVerifyCode.ImageSource = bmp;
        }

        /// <summary>
        /// 验证通过后登录
        /// </summary>
        /// <param name="verifyCodes"></param>
        /// <param name="code"></param>
        private async Task VerificationLogin(string verifyCodes, int code)
        {
            string loginName = viewLogin.txtUserName.Text,
                userPassword = viewLogin.txtPassword.Password;
            bool chkRemeberMe = (bool)viewLogin.chkRememberMe.IsChecked,
                chkAutoLogin = (bool)viewLogin.chkAutoLogin.IsChecked;
            Dictionary<string, string> loginParams = new Dictionary<string, string>()
            {
                {"loginUserDTO.user_name",loginName},
                {"userDTO.password",userPassword},
                {"randCode",verifyCodes}
            };
            string result = await TicketHelpers.LoginAsync(loginParams);
            staInfo.Content = result;

            if (result.Contains("登录成功"))
            {
                ShowSystemNotice("12306助手", "登录成功", 5000);
                var loginedInfo = await TicketHelpers.GetLoginedInfoAsync();

                if (loginedInfo == null)
                {
                    staInfo.Content = "未获取到登录和查询地址信息，请重新登录";
                    ShowSystemNotice("12306助手", staInfo.Content.ToString(), 5000);
                    return;
                }

                viewTicketBooking.btnQueryTicket.Tag = loginedInfo.TicketQueryAction; // 查询地址保存到查询按钮上

                IsCMDLogin(false, loginedInfo.UserName);
                // 保存用户登录信息
                await TicketHelpers.SaveUserLoginInfoAsync(loginName, userPassword, chkRemeberMe, chkAutoLogin);
                // 获取乘客
                await GetContacts();
            }
            else
            {
                OpenLoginPopup(true); // 登录失败，重新打开登录框
            }
        }

        /// <summary>
        /// 是否显示顶部的登录按钮
        /// </summary>
        /// <param name="isShow">true：显示；false：移除</param>
        /// <param name="userName">用户名</param>
        private void IsCMDLogin(bool isShow, string userName)
        {
            Button cmdOpenLogin, cmdUserName, cmdLogOff;
            if (isShow)
            {
                cmdUserName = ctlCommand.FindChild<Button>("cmdUserName");
                ctlCommand.Items.Remove(cmdUserName);

                cmdLogOff = ctlCommand.FindChild<Button>("cmdLogOff");
                ctlCommand.Items.Remove(cmdLogOff);

                if (ctlCommand.FindChild<Button>("cmdOpenLogin") == null)
                {
                    cmdOpenLogin = new Button();
                    cmdOpenLogin.Content = "登录";
                    cmdOpenLogin.Name = "cmdOpenLogin";
                    cmdOpenLogin.Click += cmdOpenLogin_Click;
                    ctlCommand.Items.Add(cmdOpenLogin);
                }
                return;
            }

            cmdOpenLogin = ctlCommand.FindChild<Button>("cmdOpenLogin");
            ctlCommand.Items.Remove(cmdOpenLogin); // 移除窗体顶部的登录按钮

            cmdUserName = new Button();
            cmdUserName.Name = "cmdUserName";
            cmdUserName.Content = userName;
            ctlCommand.Items.Add(cmdUserName);

            cmdLogOff = new Button();
            cmdLogOff.Name = "cmdLogOff";
            cmdLogOff.Content = "注销";
            cmdLogOff.Click += cmdLogOff_Click;
            ctlCommand.Items.Add(cmdLogOff);
        }

        /// <summary>
        /// 获取乘客
        /// </summary>
        private async Task GetContacts()
        {
            Grid gridContacts = viewTicketBooking.gridContacts;
            gridContacts.Children.Clear();
            staInfo.Content = "加载乘客中...";
            var result = await TicketHelpers.GetContactsAsync();

            if (result.GetType().Name == "String") // 返回值类型为string
            {
                staInfo.Content = result;
                ShowSystemNotice("12306助手", staInfo.Content.ToString(), 5000);
                return;
            }

            int row = (int)Math.Ceiling((double)result.Count / 9),
                    cell = 9;
            while (row-- > 0)
            {
                gridContacts.RowDefinitions.Add(new RowDefinition()
                {
                    Height = new GridLength(20)
                });
            }
            while (cell-- > 0)
            {
                gridContacts.ColumnDefinitions.Add(new ColumnDefinition()
                {
                    Width = new GridLength()
                });
            }

            if (result.Count > 0)
            {
                int r = 0, c = 0;

                for (int i = 0; i < result.Count; i++)
                {
                    string strPassenger = string.Format("{0},{1},{2},{3}_", result[i].PassengerName, result[i].PassengerIdTypeCode, result[i].PassengerIdNo, result[i].PassengerType), // oldPassengerStr
                        strPassengerTicket = string.Format("0,{0},{1},{2},{3},{4},N_", result[i].PassengerType, result[i].PassengerName, result[i].PassengerIdTypeCode, result[i].PassengerIdNo, result[i].Mobile); // passengerTicketStr（还需要在最前面添加席别code）
                    CheckBox chkContact = new CheckBox()
                    {
                        Content = result[i].PassengerName,
                        Name = "chk" + result[i].Code,
                        Height = 18,
                        Tag = strPassenger,
                        Uid = strPassengerTicket
                    };
                    chkContact.Click += chkContact_Click;
                    gridContacts.Children.Add(chkContact);

                    if (i > 0)
                    {
                        if ((i % 9) == 0)
                        {
                            r += 1;
                            c = 0;
                        }
                        else
                        {
                            c++;
                        }
                    }

                    chkContact.SetValue(Grid.RowProperty, r);
                    chkContact.SetValue(Grid.ColumnProperty, c);
                }
            }

            staInfo.Content = "乘客加载完成";
        }

        // 选择乘客
        private void chkContact_Click(object sender, RoutedEventArgs e)
        {
            Grid gridContacts = viewTicketBooking.gridContacts;
            int chkCount = GetCheckedCount(viewTicketBooking.gridContacts);

            if (chkCount > 5)
            {
                staInfo.Content = "选择乘客的数量不能超过5个";
                CheckBox chkObj = e.Source as CheckBox;
                chkObj.IsChecked = false;
            }
        }

        // 选择席别
        private void chkSeatType_Click(object sender, RoutedEventArgs e)
        {
            int chkCount = GetCheckedCount(viewTicketBooking.gridSeatTypes);

            if (chkCount > 5)
            {
                staInfo.Content = "选择席别的数量不能超过5个";
                CheckBox chkObj = e.Source as CheckBox;
                chkObj.IsChecked = false;
            }
        }

        /// <summary>
        /// 获取选中的（乘客、席别）数
        /// </summary>
        /// <param name="gridControl"></param>
        /// <returns></returns>
        private int GetCheckedCount(Grid gridControl)
        {
            int chkCount = 0;

            foreach (var chkItem in gridControl.Children)
            {
                if (chkItem is CheckBox)
                {
                    CheckBox chkType = chkItem as CheckBox;
                    if ((bool)chkType.IsChecked)
                    {
                        chkCount++;
                    }
                }
            }

            return chkCount;
        }

        /// <summary>
        /// 获取选中的（乘客、席别）
        /// </summary>
        /// <param name="gridControl"></param>
        /// <param name="isTag">是否获取CheckBox的Tag属性值（true：获取Tag的值；false：获取Uid的值）</param>
        /// <returns></returns>
        private string GetCheckedItems(Grid gridControl, bool isTag)
        {
            string chkItems = "";

            foreach (var chk in gridControl.Children)
            {
                if (chk is CheckBox)
                {
                    CheckBox chkType = chk as CheckBox;
                    if ((bool)chkType.IsChecked)
                    {
                        chkItems += isTag ? chkType.Tag : chkType.Uid;
                    }
                }
            }

            return chkItems;
        }

        /// <summary>
        /// 获取选中的内容
        /// </summary>
        /// <param name="gridControl"></param>
        /// <returns></returns>
        private string GetChkeckedContent(Grid gridControl)
        {
            string chkItems = "";

            foreach (var chk in gridControl.Children)
            {
                if (chk is CheckBox)
                {
                    CheckBox chkType = chk as CheckBox;
                    if ((bool)chkType.IsChecked)
                    {
                        chkItems += string.Format("{0}，", chkType.Content);
                    }
                }
            }

            return chkItems.TrimEnd('，');
        }

        /// <summary>
        /// 是否打开预订任务框
        /// </summary>
        /// <param name="isOpen">true：打开；false：关闭</param>
        private void OpenBookingTaskPopup(bool isOpen)
        {
            if (isOpen)
            {
                ticketTaskPopup.Visibility = Visibility.Visible;
                gridPopup.Visibility = Visibility.Visible;

                return;
            }

            ticketTaskPopup.Visibility = Visibility.Hidden;
            gridPopup.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// 查询车次
        /// </summary>
        /// <param name="txtFromCity">出发地</param>
        /// <param name="txtToCity">目的地</param>
        /// <param name="txtDate">乘车日期</param>
        /// <param name="isBuy">是否返回可以预订（Y：是）</param>
        /// <returns></returns>
        private async Task<List<dynamic>> GetTrain(ComboBox txtFromCity, ComboBox txtToCity, DatePicker txtDate, string isBuy = "")
        {
            staInfo.Content = "查询中...";
            string queryAction = viewTicketBooking.btnQueryTicket.Tag.ToString(),
                date = DateTime.Parse(txtDate.Text).ToString("yyyy-MM-dd");
            Dictionary<string, string> formParams = new Dictionary<string, string>()
            {
                {"fromStation",txtFromCity.SelectedValue.ToString()},
                {"toStation",txtToCity.SelectedValue.ToString()},
                {"date",date},
                {"purposeCode","ADULT"}
            };
            var lstTrains = await TicketHelpers.QueryTrainAsync(queryAction, formParams, isBuy);
            return lstTrains;
        }

        /// <summary>
        /// 确认任务并开始
        /// <param name="lstTrains">查询到的车次</param>
        /// <param name="strChkSeats">选中的席别</param>
        /// </summary>
        private async Task ConfirmStartTask(List<dynamic> lstChkTrains, string strChkSeats)
        {
            progressRingAnima.IsActive = true;

            string trainDate = DateTime.Parse(viewTicketBooking.txtDate.Text).ToString("yyyy-MM-dd"),
                oldPassengerStr = GetCheckedItems(viewTicketBooking.gridContacts, true), // 选中的乘客
                strPassengerTicket = GetCheckedItems(viewTicketBooking.gridContacts, false), // 选中的乘客
                passengerTicketStr = "",
                trainNo = "",
                trainCode = "",
                fromStationCode = "",
                fromStationName = "",
                toStationCode = "",
                toStationName = "",
                seatTypeCode = "",
                leftTicket = "",
                trainLocation = "",
                keyCheckChang = "",
                secretStr = "";
            var strSeatArr = strChkSeats.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            var strPassengerArr = strPassengerTicket.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            lblFromStation.Tag = string.Format("{0}%2C{1}", await TicketHelpers.EscapeAsync(viewTicketBooking.txtFromCity.Text), viewTicketBooking.txtFromCity.SelectedValue);
            lblToStation.Tag = string.Format("{0}%2C{1}", await TicketHelpers.EscapeAsync(viewTicketBooking.txtToCity.Text), viewTicketBooking.txtToCity.SelectedValue);

            for (int i = 0; i < lstChkTrains.Count(); i++)
            {
                trainNo = lstChkTrains[i].TrainNo; // 车次编号
                trainCode = lstChkTrains[i].TrainCode; // 车次
                fromStationCode = lstChkTrains[i].FromStationCode; // 出发地Code
                fromStationName = lstChkTrains[i].FromStationName;
                toStationCode = lstChkTrains[i].ToStationCode; // 目的地Code
                toStationName = lstChkTrains[i].ToStationName;
                secretStr = lstChkTrains[i].SecretStr; // 车次预订凭证
                leftTicket = lstChkTrains[i].YPInfo; // 席别相关信息
                trainLocation = lstChkTrains[i].LocationCode;

                for (int s = 0; s < strSeatArr.Length; s++)
                {
                    seatTypeCode = strSeatArr[s];
                    passengerTicketStr = "";

                    for (int p = 0; p < strPassengerArr.Length; p++)
                    {
                        passengerTicketStr += string.Format("{0},{1}_", seatTypeCode, strPassengerArr[p]);
                    }

                    passengerTicketStr = passengerTicketStr.TrimEnd('_');
                    staInfo.Content = string.Format("正在预订【{0}】车次...", trainCode);
                    ShowSystemNotice("12306助手", staInfo.Content.ToString(), 5000);
                    // 打开预订任务框
                    viewTicketTask.lblTaskDate.Content = string.Format("正在预订（{0}）", trainDate);
                    viewTicketTask.lblTaskFrom.Text = fromStationName;
                    viewTicketTask.lblTaskTo.Text = toStationName;
                    viewTicketTask.lblTaskTrain.Content = trainCode;
                    viewTicketTask.lblTaskPassenger.Content = GetChkeckedContent(viewTicketBooking.gridContacts);
                    viewTicketTask.lblTaskSeat.Content = TicketHelpers.GetSeatTypeInfo(seatTypeCode, null);
                    OpenBookingTaskPopup(true);
                    // 提交订单
                    Dictionary<string, string> formParams = new Dictionary<string, string>()
                    {
                        {"secretStr",HttpUtility.UrlDecode(secretStr)},
                        {"train_date",trainDate},
                        {"tour_flag","dc"},
                        {"purpose_codes","ADULT"},
                        {"query_from_station_name",viewTicketBooking.txtFromCity.Text},
                        {"query_to_station_name",viewTicketBooking.txtToCity.Text},
                        {"cancel_flag","2"},
                        {"bed_level_order_num","000000000000000000000000000000"},
                        {"passengerTicketStr",passengerTicketStr},
                        {"oldPassengerStr",oldPassengerStr}
                    };
                    //CookieContainer cookieContainer = TicketHelpers.cookieContainer;
                    //Uri uri = new Uri("https://kyfw.12306.cn");
                    //cookieContainer.Add(uri, new Cookie("_jc_save_detail", "true"));
                    //cookieContainer.Add(uri, new Cookie("_jc_save_fromDate", DateTime.Parse(viewTicketBooking.txtDate.Text).ToString("yyyy-MM-dd"))); // 乘车日期
                    //cookieContainer.Add(uri, new Cookie("_jc_save_fromStation", lblFromStation.Tag.ToString())); // url编码的fromStation
                    //cookieContainer.Add(uri, new Cookie("_jc_save_showIns", "true"));
                    //cookieContainer.Add(uri, new Cookie("_jc_save_toDate", DateTime.Now.ToString("yyyy-MM-dd"))); // 返程日期，默认为当天
                    //cookieContainer.Add(uri, new Cookie("_jc_save_toStation", lblToStation.Tag.ToString())); // url编码的toStation
                    //cookieContainer.Add(uri, new Cookie("_jc_save_wfdc_flag", "dc"));

                    var result = await TicketHelpers.SubmitOrderForAutoAsync(formParams);

                    if (result.GetType().Name == "String")
                    {
                        staInfo.Content = result;

                        OpenBookingTaskPopup(false); // 关闭预订任务框

                        if ("用户未登录".Contains(result))
                        {
                            OpenLoginPopup(true);
                        }
                        else
                        {
                            continue;
                        }

                        ShowSystemNotice("12306助手", staInfo.Content.ToString(), 5000);
                        progressRingAnima.IsActive = false;
                        return;
                    }

                    staInfo.Content = "正在加入排队...";
                    // 获取排队信息
                    leftTicket = result["leftTicketStr"];
                    trainLocation = result["train_location"];
                    keyCheckChang = result["key_check_isChange"];
                    trainDate = DateTime.Parse(trainDate).ToString("ddd MMM dd yyyy HH:mm:ss 'GMT'zzz", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
                    trainDate = trainDate.Replace("08:00", "0800");
                    Dictionary<string, string> queueParams = new Dictionary<string, string>()
                    {
                        {"train_date",trainDate},
                        {"train_no",trainNo},
                        {"stationTrainCode",trainCode},
                        {"seatType",seatTypeCode},
                        {"fromStationTelecode",fromStationCode},
                        {"toStationTelecode",toStationCode},
                        {"leftTicket",leftTicket},
                        {"purpose_codes","ADULT"},
                        {"_json_att",""}
                    };
                    var queueResult = await TicketHelpers.GetQueueCountAsync(1, queueParams);

                    if (queueResult.GetType().Name == "String")
                    {
                        staInfo.Content = queueResult;
                        OpenBookingTaskPopup(false); // 关闭预订任务框

                        if ("用户未登录".Contains(queueResult))
                        {
                            OpenLoginPopup(true);
                        }
                        else
                        {
                            continue;
                        }

                        ShowSystemNotice("12306助手", staInfo.Content.ToString(), 5000);
                        progressRingAnima.IsActive = false;
                        return;
                    }

                    if ((bool)queueResult.IsChangeTicket)
                    {
                        continue; // 如果当前（车次）席别不能加入排队，则继续下一个（车次）席别
                    }

                    //leftTicket = queueResult.SeatTypeInfo;
                    staInfo.Content = queueResult.TipInfo;
                    lblKeyCheckChange.Tag = keyCheckChang;
                    lblLeftTicket.Tag = leftTicket;
                    lblOldPassenger.Tag = oldPassengerStr;
                    lblPassengerTicket.Tag = passengerTicketStr;
                    lblTrainLocation.Tag = trainLocation;
                    viewVerifyCode.lblCodeHead.Content = string.Format("{0}（{1}）→{2}（{3}）", viewTicketBooking.txtFromCity.Text, lstChkTrains[i].StartTime, viewTicketBooking.txtToCity.Text, lstChkTrains[i].ArriveTime);

                    if (result["isShowValidCode"] == "Y")
                    { // 判断是否需要选择验证码
                        OpenBookingTaskPopup(false); // 关闭预订任务框
                        OpenVerifyCodePopup(true, false); // 打开验证码选择框
                        await RefreshVerifyCode(1);
                        ShowSystemNotice("12306助手", "选择验证码", 5000);
                        staInfo.Content = "选择验证码";
                    }
                    else
                    {
                        // 不需要验证码
                        await SubmitOrder("");
                    }

                    progressRingAnima.IsActive = false;
                    return; // 如果当前席别可以加入排队，则弹出验证码选择框，并继续后面的操作
                }
            }
        }

        /// <summary>
        /// 提交订单
        /// </summary>
        /// <param name="verifyCodes">验证码</param>
        private async Task SubmitOrder(string verifyCodes)
        {
            if (string.IsNullOrEmpty(verifyCodes))
            {
                progressRingAnima.IsActive = false;
            }

            // 确认订单信息 
            Dictionary<string, string> orderParams = new Dictionary<string, string>()
            {
                {"passengerTicketStr",lblPassengerTicket.Tag.ToString()},
                {"oldPassengerStr",lblOldPassenger.Tag.ToString()},
                {"randCode",verifyCodes},
                {"purpose_codes","ADULT"},
                {"key_check_isChange",lblKeyCheckChange.Tag.ToString()},
                {"leftTicketStr",lblLeftTicket.Tag.ToString()},
                {"train_location",lblTrainLocation.Tag.ToString()},
                {"choose_seats","" },
                {"seatDetailType" ,"000"},
                {"_json_att",""}
            };

            var confirmResult = await TicketHelpers.ConfirmOrderAsync(1, orderParams);

            if (confirmResult.GetType().Name == "String")
            {
                staInfo.Content = confirmResult;

                if (string.IsNullOrEmpty(verifyCodes) || "再试一次".Contains(confirmResult))
                {
                    OpenBookingTaskPopup(false); // 关闭预订任务框
                }

                if ("用户未登录".Contains(confirmResult))
                {
                    OpenLoginPopup(true);
                }

                ShowSystemNotice("12306助手", staInfo.Content.ToString(), 5000);
                progressRingAnima.IsActive = false;
                return;
            }

            // 订单确认成功，等待出票
            staInfo.Content = "订单确认成功，等待出票...";
            while (true)
            {
                var waitResult = await TicketHelpers.GetOrderWaitTimeAsync();

                if (!(bool)waitResult.Status)
                {
                    staInfo.Content = "用户未登录";

                    if (string.IsNullOrEmpty(verifyCodes))
                    {
                        OpenBookingTaskPopup(false); // 关闭预订任务框
                    }

                    OpenLoginPopup(true);
                    ShowSystemNotice("12306助手", staInfo.Content.ToString(), 5000);
                    break;
                }

                if (waitResult != null)
                {
                    if (waitResult.WaitTime <= 0 && !string.IsNullOrEmpty(waitResult.OrderId))
                    {
                        string orderResult = string.Format("出票成功！订单号：【{0}】", waitResult.OrderId);
                        ShowSystemNotice("12306助手", orderResult, 5000);
                        if ((bool)viewSet.tgsSpeech.IsChecked)
                        {
                            await TicketHelpers.SpeechSpeakAsync(orderResult);
                        }
                        staInfo.Content = orderResult;

                        if (string.IsNullOrEmpty(verifyCodes))
                        {
                            OpenBookingTaskPopup(false); // 关闭预订任务框
                        }

                        tabMenu.SelectedIndex = 1; // 选择“待支付订单”选项卡
                        await GetAwaitPayOrderAsync();
                        break;
                    }
                    else
                    {
                        staInfo.Content = waitResult.Count > 0 ? string.Format("前面还有【{0}】订单等待处理，大约需等待【{1}】秒", waitResult.WaitCount, waitResult.WaitTime) : string.Format("出票中，大约需要【{0}】秒", waitResult.WaitTime);
                    }
                }
            }
        }


        /// <summary>
        /// 查询待支付的订单
        /// </summary>
        /// <returns></returns>
        private async Task GetAwaitPayOrderAsync()
        {
            progressRingAnima.IsActive = true;
            viewMyOrder.orderList.IsEnabled = false;
            staInfo.Content = "正在查询待支付订单...";
            StringBuilder orderTemp = new StringBuilder();
            orderTemp.Append("<StackPanel");
            orderTemp.Append(" xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'");

            var orderResult = await TicketHelpers.GetNoCompleteOrder();

            if (orderResult.GetType().Name == "String")
            {
                orderTemp.Append(" VerticalAlignment='Center'>");

                if ("用户未登录".Contains(orderResult))
                {
                    OpenLoginPopup(true);
                }
                else
                {
                    orderTemp.AppendFormat("<Label Content='{0}' HorizontalContentAlignment='Center' Foreground='#6B15A1'  FontSize='35' />", orderResult);
                }

                staInfo.Content = orderResult;
            }
            else
            {
                List<dynamic> lstTickets = orderResult as List<dynamic>;
                List<string> lstOrders = new List<string>();
                orderTemp.Append(">");

                for (int i = 0; i < lstTickets.Count(); i++)
                {
                    string strOrderCode = lstTickets[i].OrderCode,
                        strForm = string.Format("{0} {1}", lstTickets[i].FromStationName, lstTickets[i].StartTime),
                        strTo = string.Format("{0} {1}", lstTickets[i].ToStationName, lstTickets[i].ArriveTime),
                        strSeatInfo = string.Format(@"{0}{1}车厢{2} 票价（{3}）：{4}", lstTickets[i].SeatTypeName, lstTickets[i].CoachName, lstTickets[i].SeatName, lstTickets[i].TicketTypeName, lstTickets[i].TicketPrice),
                        orderFontColor = "#3FB2DF"; // 订单字体颜色

                    lstOrders.Add(strOrderCode);
                    orderTemp.Append("<Grid>");

                    if (i % 2 == 0)
                    {
                        orderFontColor = "#fff";
                        orderTemp.Append("<Grid.Background>");
                        orderTemp.Append("<SolidColorBrush Color='#3FB2DF' />");
                        orderTemp.Append("</Grid.Background>");
                    }

                    orderTemp.Append("<Grid.RowDefinitions>");
                    orderTemp.Append("<RowDefinition Height='60'/>");
                    orderTemp.Append("<RowDefinition Height='*'/>");
                    orderTemp.Append("<RowDefinition Height='40'/>");
                    orderTemp.Append("<RowDefinition Height='40'/>");
                    orderTemp.Append("</Grid.RowDefinitions>");
                    orderTemp.Append("<Grid.ColumnDefinitions>");
                    orderTemp.Append("<ColumnDefinition Width='1.*'/>");
                    orderTemp.Append("<ColumnDefinition Width='100'/>");
                    orderTemp.Append("<ColumnDefinition Width='1.*'/>");
                    orderTemp.Append("</Grid.ColumnDefinitions>");
                    orderTemp.AppendFormat("<Label Grid.ColumnSpan='3' Content='{0}' HorizontalContentAlignment='Center' VerticalContentAlignment='Center' Foreground='{1}' FontSize='30'/>", lstTickets[i].TrainDate, orderFontColor);
                    //orderTemp.Append("<Border Margin='0,58,0,0' Height='2' Background='#FF41B1E1' Grid.ColumnSpan='3' />");
                    orderTemp.AppendFormat("<TextBlock Grid.Row='1' Text='{0}' HorizontalAlignment='Center' VerticalAlignment='Center' Foreground='{1}' FontSize='30' />", strForm, orderFontColor);
                    orderTemp.AppendFormat("<Label Grid.Row='1' Grid.Column='1' Content='→' Foreground='{0}' FontSize='60' HorizontalContentAlignment='Center' VerticalAlignment='Center' Margin='0,30,0,10'/>", orderFontColor);
                    orderTemp.AppendFormat("<Label Grid.Row='1' Grid.Column='1' Content='{0}' HorizontalContentAlignment='Center' Foreground='{1}' VerticalAlignment='Center' FontSize='23' Margin='0,-25,0,0' />", lstTickets[i].TrainCode, orderFontColor);
                    orderTemp.AppendFormat("<TextBlock Grid.Row='1' Grid.Column='2' Text='{0}' HorizontalAlignment='Center' VerticalAlignment='Center' Foreground='{1}' FontSize='30' />", strTo, orderFontColor);
                    //orderTemp.Append("<Border Margin='0,-38,0,0' Height='2' Background='#FF41B1E1' Grid.ColumnSpan='3' Grid.Row='2' />");
                    orderTemp.AppendFormat("<Label Grid.Row='2' Content='{0}' VerticalContentAlignment='Center' Foreground='{1}'  FontSize='18'/>", lstTickets[i].PassengerName, orderFontColor);
                    orderTemp.AppendFormat("<Label Grid.Row='2' Grid.Column='1' Grid.ColumnSpan='2' Content='{0}' HorizontalContentAlignment='Right' VerticalContentAlignment='Center' Foreground='{1}'  FontSize='18'/>", strSeatInfo, orderFontColor);
                    //orderTemp.Append("<Border Margin='0,38,0,0' Height='2' Background='#FF41B1E1' Grid.ColumnSpan='3' Grid.Row='2' />");
                    orderTemp.AppendFormat("<Label Grid.Row='3' Content='订单号：{0}' VerticalContentAlignment='Center' Foreground='{1}'  FontSize='18'/>", strOrderCode, orderFontColor);
                    orderTemp.AppendFormat("<Label Grid.Row='3' Grid.Column='1' Grid.ColumnSpan='2' Content='预订时间：{0}' HorizontalContentAlignment='Right' VerticalContentAlignment='Center' Foreground='{1}'  FontSize='18'/>", lstTickets[i].ReserveTime, orderFontColor);
                    orderTemp.AppendFormat("<Label Grid.ColumnSpan='3' Grid.RowSpan='4' Content='{0}' HorizontalContentAlignment='Center' VerticalContentAlignment='Center' Foreground='#6B15A1'  FontSize='100' Opacity='0.3' Panel.ZIndex='-1' />", lstTickets[i].OrderStatus);
                    orderTemp.Append("</Grid>");
                }

                staInfo.Content = string.Format("您有【{0}】张订单（共有【{1}】张车票），等待支付", lstOrders.Count(), lstTickets.Count());
            }

            orderTemp.Append("</StackPanel>");
            viewMyOrder.orderList.Content = XamlReader.Parse(orderTemp.ToString());
            ShowSystemNotice("12306助手", staInfo.Content.ToString(), 5000);
            viewMyOrder.orderList.IsEnabled = true;
            progressRingAnima.IsActive = false;
        }

        #region 事件

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            TicketHelpers.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
        }

        // 窗体加载完成后
        private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitNotfyIconMenu();

            ShowSystemNotice("12306助手", "程序信息初始化中，请稍后", 2000);

            // 初始化设置项
            viewSet.tgsSpeech.IsChecked = Settings.Default.IsSpeech;

            // 初始化搜索
            viewTicketBooking.txtFromCity.Text = Settings.Default.FromStationName;
            viewTicketBooking.txtFromCity.SelectedValue = Settings.Default.FromStationCode;
            viewTicketBooking.txtToCity.Text = Settings.Default.ToStationName;
            viewTicketBooking.txtToCity.SelectedValue = Settings.Default.ToStationCode;
            viewTicketBooking.txtDate.DisplayDateStart = DateTime.Now;
            viewTicketBooking.txtDate.DisplayDateEnd = DateTime.Now.AddDays(29);
            viewTicketBooking.txtDate.Text = string.IsNullOrEmpty(Settings.Default.TrainDate) ? viewTicketBooking.txtDate.DisplayDateEnd.Value.ToString("yyyy-MM-dd") : Settings.Default.TrainDate;

            bool internetState = await TicketHelpers.CheckInternetConnectedStateAsync(); // 检查网络连接状态


            // 更新站名文件
            await TicketHelpers.UpdateStationNameAsync();

            if (!internetState)
            {
                await this.ShowMessageAsync("12306助手", "请确保您的网络连接正常！");
                Application.Current.Shutdown();
            }

            //if (MessageDialogResult.Affirmative == await this.ShowMessageAsync("12306助手", "本助手仅为辅助软件，不要太依赖于本助手!\r是否打开浏览器？", MessageDialogStyle.AffirmativeAndNegative))
            //{
            //    Process.Start("https://kyfw.12306.cn/otn/login/init");
            //}

            ShowSystemNotice("12306助手", "程序信息初始化完成", 2000);

            OpenLoginPopup(true);
        }

        // 窗体状态发生改变后
        private void MetroWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.Hide();
            }
        }

        // 关闭窗体确认
        private async void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            if (MessageDialogResult.Negative == await this.ShowMessageAsync("12306助手", "确定退出吗？", MessageDialogStyle.AffirmativeAndNegative))
            {
                return;
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        // 窗体关闭后
        private void MetroWindow_Closed(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
        }

        // 打开设置
        private void cmdSet_Click(object sender, RoutedEventArgs e)
        {
            var flyout = this.Flyouts.Items[0] as Flyout;
            if (flyout == null)
            {
                return;
            }

            flyout.IsOpen = !flyout.IsOpen;
        }

        // 打开登录
        private void cmdOpenLogin_Click(object sender, RoutedEventArgs e)
        {
            OpenLoginPopup(true);
        }

        // 注销
        void cmdLogOff_Click(object sender, RoutedEventArgs e)
        {
            progressRingAnima.IsActive = true;
            OpenLoginPopup(true);
            progressRingAnima.IsActive = false;
        }

        // 待支付订单
        private async void tabAwaitPay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            await GetAwaitPayOrderAsync();
        }

        #endregion

    }
}
