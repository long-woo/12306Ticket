using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TicketClient.Properties;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Windows;

namespace TicketClient.Helpers
{
    public static class TicketHelpers
    {
        public static CookieContainer cookieContainer = new CookieContainer();
        private static HttpClient httpClient;

        private static SymmetricAlgorithm symAlgorithm = new RijndaelManaged();
        private static string Key = "YNZlwFi7jBGRfyxc24bmVCUpJqOMThsAQ0a6HkSnXWIvEzeog9tDd8u1PK5L3r";

        static TicketHelpers()
        {
            HttpClientHandler httpHandler = new HttpClientHandler();
            httpHandler.CookieContainer = cookieContainer;

            httpClient = new HttpClient(httpHandler);
            httpClient.MaxResponseContentBufferSize = 1024 * 1024;
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:50.0) Gecko/20100101 Firefox/50.0");
            httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            httpClient.DefaultRequestHeaders.Add("Referer", "https://kyfw.12306.cn/otn/leftTicket/init");
            //httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            httpClient.DefaultRequestHeaders.ExpectContinue = false;
            // 由于12306证书问题，需要设置ServerCertificateValidationCallback
            ServicePointManager.ServerCertificateValidationCallback += (se, cert, chain, sslerror) =>
            {
                return true;
            };
            //预热httpclient
            var message = new HttpRequestMessage
            {
                //Method = new HttpMethod("HEAD"),
                Method = new HttpMethod("GET"),
                RequestUri = new Uri("https://kyfw.12306.cn/otn/login/init"),
            };
            httpClient.SendAsync(message).Result.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// 语音朗读
        /// </summary>
        /// <param name="content">朗读的内容</param>
        public static Task SpeechSpeakAsync(string content)
        {
            return Task.Factory.StartNew(() =>
            {
                Thread.Sleep(100);
                SpeechSynthesizer speech = new SpeechSynthesizer();
                speech.Volume = 100;
                speech.Rate = 0;
                speech.SpeakAsync(content);
            });
        }

        [DllImport("wininet")]
        private static extern bool InternetGetConnectedState(out int connectionDescription, int reservedValue);

        /// <summary>
        /// 程序内存释放
        /// </summary>
        /// <param name="proc"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll")]
        public static extern int SetProcessWorkingSetSize(IntPtr proc, int min, int max);

        /// <summary>
        /// 检查是否连接Internet网络
        /// </summary>
        /// <returns>true：已连接；false：未连接</returns>
        public static Task<bool> CheckInternetConnectedStateAsync()
        {
            return Task.Factory.StartNew(() =>
            {
                Thread.Sleep(100);
                int connDescript = 0;
                return InternetGetConnectedState(out connDescript, 0);
            });
        }

        #region 对称加密算法

        /// <summary>     
        /// 获得密钥     
        /// </summary>     
        /// <returns>密钥</returns>     
        private static byte[] GetLegalKey()
        {
            string sTemp = Key;
            symAlgorithm.GenerateKey();
            byte[] bytTemp = symAlgorithm.Key;
            int KeyLength = bytTemp.Length;
            if (sTemp.Length > KeyLength)
                sTemp = sTemp.Substring(0, KeyLength);
            else if (sTemp.Length < KeyLength)
                sTemp = sTemp.PadRight(KeyLength, ' ');
            return ASCIIEncoding.ASCII.GetBytes(sTemp);
        }
        /// <summary>     
        /// 获得初始向量IV     
        /// </summary>     
        /// <returns>初试向量IV</returns>     
        private static byte[] GetLegalIV()
        {
            string sTemp = "QVhWPqmx61DNEcLbls3ZjFO4iY0I895gJUXnuofkv2zwdeTCraSMp7AHtRBKyG";
            symAlgorithm.GenerateIV();
            byte[] bytTemp = symAlgorithm.IV;
            int IVLength = bytTemp.Length;
            if (sTemp.Length > IVLength)
                sTemp = sTemp.Substring(0, IVLength);
            else if (sTemp.Length < IVLength)
                sTemp = sTemp.PadRight(IVLength, ' ');
            return ASCIIEncoding.ASCII.GetBytes(sTemp);
        }

        /// <summary>
        /// 加密
        /// </summary>
        /// <param name="content">需要加密的字符串</param>
        /// <returns>返回解密后的结果</returns>
        public static Task<string> EncryptAsync(string content)
        {
            return Task.Factory.StartNew(() =>
            {
                Thread.Sleep(100);
                byte[] bytIn = UTF8Encoding.UTF8.GetBytes(content);
                MemoryStream ms = new MemoryStream();
                symAlgorithm.Key = GetLegalKey();
                symAlgorithm.IV = GetLegalIV();
                ICryptoTransform encrypto = symAlgorithm.CreateEncryptor();
                CryptoStream cs = new CryptoStream(ms, encrypto, CryptoStreamMode.Write);
                cs.Write(bytIn, 0, bytIn.Length);
                cs.FlushFinalBlock();
                ms.Close();
                byte[] bytOut = ms.ToArray();
                string encryptedTicket = Convert.ToBase64String(bytOut);

                return encryptedTicket;
            });
        }

        /// <summary>
        /// 解密
        /// </summary>
        /// <param name="content">需要解密的字符串</param>
        /// <returns>返回解密后的结果</returns>
        public static Task<string> DecryptAsync(string content)
        {
            return Task.Factory.StartNew(() =>
            {
                Thread.Sleep(100);
                byte[] bytIn = Convert.FromBase64String(content);
                MemoryStream ms = new MemoryStream(bytIn, 0, bytIn.Length);
                symAlgorithm.Key = GetLegalKey();
                symAlgorithm.IV = GetLegalIV();
                ICryptoTransform encrypto = symAlgorithm.CreateDecryptor();
                CryptoStream cs = new CryptoStream(ms, encrypto, CryptoStreamMode.Read);
                StreamReader sr = new StreamReader(cs);
                return sr.ReadToEnd();
            });
        }

        #endregion

        /// <summary>
        /// Unicode转中文
        /// </summary>
        /// <param name="str">16位unicode</param>
        /// <returns></returns>
        public static Task<string> UnicodeToGBKAsync(string str)
        {
            return Task.Factory.StartNew(() =>
            {
                Thread.Sleep(100);
                MatchCollection match = Regex.Matches(str, "\\\\u([\\w{4}])");
                string text = str.Replace(@"\u", "");
                char[] charStr = new char[match.Count];
                for (int i = 0; i < charStr.Length; i++)
                {
                    charStr[i] = (char)Convert.ToInt32(text.Substring(i * 4, 4), 16);
                }
                return new string(charStr);
            });
        }

        /// <summary>
        /// Escape编码
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public static Task<string> EscapeAsync(string content)
        {
            return Task.Factory.StartNew(() =>
            {
                Thread.Sleep(100);
                StringBuilder str = new StringBuilder();
                byte[] ba = System.Text.Encoding.Unicode.GetBytes(content);
                for (int i = 0; i < ba.Length; i += 2)
                {
                    str.Append("%u");
                    str.Append(ba[i + 1].ToString("X2"));

                    str.Append(ba[i].ToString("X2"));
                }
                return str.ToString();
            });
        }

        // <summary>
        /// 保存文件
        /// </summary>
        /// <param name="fileName">文件名称</param>
        /// <param name="suffix">后缀名</param>
        /// <param name="data">文件内容</param>
        public static Task SaveFileAsync(string fileName, string suffix, string data)
        {
            return Task.Factory.StartNew(() =>
            {
                Thread.Sleep(100);
                string file = string.Format("{0}.{1}", fileName, suffix);

                if (!File.Exists(file))
                {
                    File.CreateText(file).Close();
                }
                using (StreamWriter writer = new StreamWriter(file, false, Encoding.UTF8))
                {
                    writer.BaseStream.Seek(0, SeekOrigin.Begin);
                    writer.Write(data);
                }
            });
        }

        /// <summary>
        /// 获取文件内容
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <param name="suffix">后缀名</param>
        /// <returns></returns>
        public static Task<string> GetFileAsync(string fileName, string suffix)
        {
            return Task.Factory.StartNew(() =>
            {
                Thread.Sleep(100);
                string file = string.Format("{0}.{1}", fileName, suffix);
                string fileContent = "";

                if (File.Exists(file))
                {
                    fileContent = File.ReadAllText(file, Encoding.UTF8);
                }

                return fileContent;
            });
        }

        /// <summary>
        /// 获取json格式文件内容
        /// </summary>
        /// <param name="fileName">文件名称，不包含后缀名</param>
        /// <returns></returns>
        public static Task<JObject> GetJsonFileAsync(string fileName)
        {
            return Task.Factory.StartNew(() =>
            {
                Thread.Sleep(100);
                JObject json = new JObject();
                string file = string.Format("{0}.json", fileName);

                if (File.Exists(file))
                {
                    string fileContent = File.ReadAllText(file, Encoding.UTF8);
                    json = JObject.Parse(fileContent);
                }

                return json;
            });
        }

        /// <summary>
        /// 查找父级元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static T GetParentObject<T>(DependencyObject obj, string name) where T : FrameworkElement
        {
            DependencyObject parent = VisualTreeHelper.GetParent(obj);

            while (parent != null)
            {
                if (parent is T && (((T)parent).Name == name || string.IsNullOrEmpty(name)))
                {
                    return (T)parent;
                }

                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

        /// <summary>
        /// 获取DataGrid行
        /// </summary>
        /// <param name="dataGrid"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private static Task<DataGridRow> GetDataGridRowAsync(DataGrid dataGrid, int index)
        {
            return Task.Factory.StartNew(() =>
            {
                Thread.Sleep(100);
                DataGridRow row = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(index);
                if (row == null)
                {
                    dataGrid.UpdateLayout();
                    dataGrid.ScrollIntoView(dataGrid.Items[index]);
                    row = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(index);
                }
                return row;
            });
        }

        /// <summary>
        /// 获取当前节点的子节点
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parent"></param>
        /// <returns></returns>
        private static T GetObjectChildren<T>(Visual parent) where T : Visual
        {
            T children = default(T);
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                children = v as T;
                if (children == null)
                {
                    children = GetObjectChildren<T>(v);
                }
                if (children != null)
                {
                    break;
                }
            }
            return children;
        }

        /// <summary>
        /// 获取DataGrid列
        /// </summary>
        /// <param name="dataGrid"></param>
        /// <param name="row"></param>
        /// <param name="column"></param>
        /// <returns></returns>
        public static async Task<DataGridCell> GetDataGridCellAsync(DataGrid dataGrid, int row, int column)
        {
            DataGridRow rowContainer = await GetDataGridRowAsync(dataGrid, row);
            if (rowContainer != null)
            {
                DataGridCellsPresenter presenter = GetObjectChildren<DataGridCellsPresenter>(rowContainer);

                DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
                if (cell == null)
                {
                    dataGrid.ScrollIntoView(rowContainer, dataGrid.Columns[column]);
                    cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
                }
                return cell;
            }
            return null;
        }

        /// <summary>
        /// 获取用户登录信息
        /// </summary>
        /// <returns></returns>
        public static async Task<List<dynamic>> GetUserLoginInfoAsync()
        {
            List<dynamic> lstUsers = new List<dynamic>();
            var json = await GetJsonFileAsync("Account");

            if (json.Count > 0)
            {
                var jArray = json["users"] as JArray;
                // 存在用户信息
                if (jArray != null)
                {
                    lstUsers = JsonConvert.DeserializeObject<List<dynamic>>(json["users"].ToString());
                }
            }

            return lstUsers;
        }

        /// <summary>
        /// 保存用户登录信息
        /// </summary>
        /// <param name="loginName">登录名</param>
        /// <param name="userPassword">密码</param>
        /// <param name="chkRemeberMe">是否记住我</param>
        /// <param name="chkAutoLogin">是否自动登录</param>
        /// <returns></returns>
        public static async Task SaveUserLoginInfoAsync(string loginName, string userPassword, bool chkRemeberMe, bool chkAutoLogin)
        {
            List<dynamic> lstUsers = await GetUserLoginInfoAsync();
            // 是否有当前用户的信息
            var user = (from u in lstUsers
                        where u.name == loginName
                        select u).FirstOrDefault<dynamic>();

            if (user == null)
            {
                if (chkRemeberMe)
                {
                    var model = new
                    {
                        name = loginName,
                        password = await EncryptAsync(userPassword),
                        isAutoLogin = chkAutoLogin
                    };
                    lstUsers.Add(model);
                }
            }
            else
            {
                user.password = await EncryptAsync(userPassword);
                user.isAutoLogin = chkAutoLogin;

                if (!chkRemeberMe)
                {
                    lstUsers.Remove(user);
                }
            }

            JObject jsonUser = new JObject(
                new JProperty("users", new JArray(
                    from u in lstUsers
                    select new JObject(
                        new JProperty("name", u.name),
                        new JProperty("password", u.password),
                        new JProperty("isAutoLogin", u.isAutoLogin)
                    )
                ))
            );
            await TicketHelpers.SaveFileAsync("Account", "json", jsonUser.ToString());
        }

        #region HttpClient

        /// <summary>
        /// HTTP GET 请求
        /// </summary>
        /// <param name="url">请求的url</param>
        /// <returns>返回string结果</returns>
        public static async Task<string> GetAsync(string url)
        {
            var uri = new Uri(url);

            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(uri);
                response.EnsureSuccessStatusCode();
                HttpContent content = response.Content;
                string result = await content.ReadAsStringAsync();

                return result;
            }
            catch (Exception)
            {
                return "{\"validateMessagesShowId\":\"_validatorMessage\",\"status\":true,\"httpstatus\":200,\"data\":{},\"messages\":[\"错误：请求发生异常！\"],\"validateMessages\":{}}";
            }
        }

        /// <summary>
        /// HTTP GET 请求
        /// </summary>
        /// <param name="url">请求的url</param>
        /// <returns>返回Stream结果</returns>
        public static async Task<Stream> GetStreamAsync(string url)
        {
            var uri = new Uri(url);

            HttpResponseMessage response = await httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            HttpContent content = response.Content;
            var result = await content.ReadAsStreamAsync();

            return result;
        }

        /// <summary>
        /// HTTP POST 请求
        /// </summary>
        /// <param name="url">请求的url</param>
        /// <param name="formParams">post 参数</param>
        /// <returns>返回string结果</returns>
        public static async Task<string> PostAsync(string url, Dictionary<string, string> formParams)
        {
            var uri = new Uri(url);

            try
            {
                HttpContent content = new FormUrlEncodedContent(formParams);

                content.Headers.ContentType.CharSet = "UTF-8";
                HttpResponseMessage response = await httpClient.PostAsync(uri, content);
                response.EnsureSuccessStatusCode();
                content = response.Content;
                string result = await content.ReadAsStringAsync();

                return result;
            }
            catch (Exception)
            {
                return "{\"validateMessagesShowId\":\"_validatorMessage\",\"status\":true,\"httpstatus\":200,\"data\":{},\"messages\":[\"错误：请求发生异常！\"],\"validateMessages\":{}}";
            }
        }

        #endregion

        #region HTTP请求

        /// <summary>
        /// 获取验证码图片
        /// </summary>
        /// <param name="code">验证码类型（1：订单；其他：登录）</param>
        /// <returns>返回bitmapimage结果</returns>
        public static async Task<BitmapImage> GetVerifyCodeImageAsync(int code)
        {
            BitmapImage bmp = new BitmapImage();
            byte[] buffer = new byte[4096];
            string url = code == 1 ? Resources.SubmitOrderVerifyCodeUrl : Resources.LoginVerifyCodeUrl;
            try
            {
                var stream = await TicketHelpers.GetStreamAsync(url);

                if (stream.Length > 0)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        int count = 0;
                        do
                        {
                            count = stream.Read(buffer, 0, buffer.Length);
                            ms.Write(buffer, 0, count);
                        } while (count != 0);
                        ms.Seek(0, SeekOrigin.Begin);
                        bmp.BeginInit();
                        bmp.StreamSource = ms;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                    }
                }
            }
            catch (Exception)
            { }

            return bmp;
        }

        /// <summary>
        /// 校验验证码
        /// </summary>
        /// <param name="formParams">验证所需参数</param>
        /// <returns>true：验证通过；false：验证失败</returns>
        public static async Task<bool> CheckVerifyCodeAsync(Dictionary<string, string> formParams)
        {
            bool resInfo = false;
            try
            {
                string result = await PostAsync(Resources.CheckVerifyCodeUrl, formParams);
                JObject json = JObject.Parse(result);
                Dictionary<string, string> jsonData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json["data"].ToString());

                if (jsonData.Count > 0)
                {
                    if (jsonData["result"].ToString() == "1")
                    {
                        resInfo = true;
                    }
                }
            }
            catch (Exception)
            { }

            return resInfo;
        }

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="formParams">登录所需的参数</param>
        /// <returns>返回登录消息</returns>
        public static async Task<string> LoginAsync(Dictionary<string, string> formParams)
        {
            string resInfo = "登录成功";
            try
            {
                string result = await PostAsync(Resources.LoginUrl, formParams);
                JObject json = JObject.Parse(result);

                if (!Convert.ToBoolean(json["status"]))
                {
                    var strArry = json["messages"] as JArray;
                    resInfo = string.Format("登录失败！{0}", strArry[0].ToString());
                    return resInfo;
                }

                Dictionary<string, string> jsonData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json["data"].ToString());

                if (jsonData.Count > 0)
                {
                    string otherMsg = jsonData["otherMsg"].ToString();
                    if (!string.IsNullOrEmpty(otherMsg))
                    {
                        resInfo = string.Format("登录成功！{0}", otherMsg);
                    }
                }
                else
                {
                    var strArry = json["messages"] as JArray;
                    resInfo = string.Format("登录失败！{0}", strArry[0].ToString());
                }
            }
            catch (Exception)
            {
                resInfo = "登录发生错误";
            }

            return resInfo;
        }

        /// <summary>
        /// 获取登录后的信息（如：用户名，车票查询的url）
        /// </summary>
        /// <returns></returns>
        public static async Task<dynamic> GetLoginedInfoAsync()
        {
            try
            {
                string result = await GetAsync(Resources.InitTicketQueryUrl),
                        userName = "",
                        ticketQueryAction = "",
                        strRegex = @"var\s+sessionInit\s*=\s*'(?<userName>[^']+)';\n\s+var\s+isShowNotice\s+=\s+null;\n\s+var\s+CLeftTicketUrl\s+=\s+'(?<ticketQueryAction>[^']+)';";
                var strInfo = Regex.Match(result, strRegex, RegexOptions.Singleline, TimeSpan.FromSeconds(2));

                if (strInfo.Success)
                {
                    userName = strInfo.Groups["userName"].Value;
                    ticketQueryAction = strInfo.Groups["ticketQueryAction"].Value;
                }

                var resData = new
                {
                    UserName = await UnicodeToGBKAsync(userName),
                    TicketQueryAction = ticketQueryAction
                };
                return resData;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 获取乘客
        /// </summary>
        /// <param name="formParams"></param>
        /// <returns></returns>
        public static async Task<dynamic> GetContactsAsync()
        {
            List<dynamic> lstContacts = new List<dynamic>();
            try
            {
                string result = await GetAsync(Resources.ContactUrl);
                JObject json = JObject.Parse(result);
                Dictionary<string, object> contacts = json["data"].ToObject<Dictionary<string, object>>();

                if (!Convert.ToBoolean(contacts["isExist"]))
                {
                    return contacts["exMsg"].ToString();
                }

                lstContacts = (from c in json["data"]["normal_passengers"]
                               select new
                               {
                                   PassengerName = c["passenger_name"], // 乘客姓名
                                   SexName = c["sex_name"], // 性别
                                   PassengerIdTypeName = c["passenger_id_type_name"], // 身份证类型
                                   PassengerIdNo = c["passenger_id_no"], // 身份证号码
                                   Mobile = c["mobile_no"], // 手机号
                                   PassengerTypeName = c["passenger_type_name"], // 乘客类型
                                   Address = c["address"], // 地址
                                   Code = c["code"],
                                   CountryCode = c["country_code"],
                                   Email = c["email"], // 邮箱
                                   UserNameFirstPY = c["first_letter"], // 乘客姓名首拼
                                   IsUserSelf = c["isUserSelf"], // 是否为登录用户本人
                                   PassengerFlag = c["passenger_flag"],
                                   PassengerIdTypeCode = c["passenger_id_type_code"], // 身份证类型Code
                                   PassengerType = c["passenger_type"], // 乘客类型Code
                                   Tel = c["phone_no"], // 电话
                                   PostalCode = c["postalcode"], // 邮政编码
                                   RecordCount = c["recordCount"],
                                   SexCode = c["sex_code"],
                                   BirthDate = c["born_date"] // 出生日期
                               }).ToList<dynamic>();
            }
            catch (Exception)
            { }

            return lstContacts;
        }

        /// <summary>
        /// 注销登录
        /// </summary>
        public static async Task LogOffAsync()
        {
            await GetAsync(Resources.LogOffUrl);
        }

        /// <summary>
        /// 更新站名文件
        /// </summary>
        /// <returns></returns>
        public static async Task UpdateStationNameAsync()
        {
            try
            {
                var result = await GetAsync(Resources.StationNameUrl);
                await SaveFileAsync("StationName", "txt", result);
            }
            catch (Exception)
            { }
        }

        /// <summary>
        /// 获取站名
        /// </summary>
        /// <param name="stationName">站名、简拼、全拼</param>
        /// <returns>返回匹配的前6条数据</returns>
        public static async Task<List<dynamic>> GetStationNamesAsync(string stationName)
        {
            List<dynamic> lstStationNames = new List<dynamic>();
            try
            {
                var result = await GetFileAsync("StationName", "txt");
                string strRegex = @"var\s+station_names\s+='(?<stationName>[^']+)';",
                    strStationName = "";
                var strInfo = Regex.Match(result, strRegex, RegexOptions.Singleline, TimeSpan.FromSeconds(2));

                if (strInfo.Success)
                {
                    strStationName = strInfo.Groups["stationName"].Value;
                }

                if (!string.IsNullOrEmpty(strStationName))
                {
                    var arrStationName = strStationName.Split(new char[] { '@' }, StringSplitOptions.RemoveEmptyEntries);
                    string[] arrItem = null;
                    foreach (var item in arrStationName)
                    {
                        arrItem = item.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        lstStationNames.Add(new
                        {
                            Code = arrItem[2],
                            FullPY = arrItem[3],
                            FirstPY = arrItem[0],
                            ZHName = arrItem[1],
                            Id = arrItem[5]
                        });
                    }
                }

                // 仅返回匹配的前6条数据
                lstStationNames = (from s in lstStationNames
                                   where s.FullPY.ToString().Contains(stationName) || s.FirstPY.ToString().Contains(stationName) || s.ZHName.ToString().Contains(stationName)
                                   select s).Take(6).ToList<dynamic>();
            }
            catch (Exception)
            { }

            return lstStationNames;
        }

        /// <summary>
        /// 查询车票
        /// </summary>
        /// <param name="queryAction">查询车票的url</param>
        /// <param name="formParams">查询所需参数（date：日期；fromStation：出发地）</param>
        /// <param name="isBuy">是否返回可以预订（Y：是）</param>
        /// <returns></returns>
        public static async Task<List<dynamic>> QueryTrainAsync(string queryAction, Dictionary<string, string> formParams, string isBuy)
        {
            List<dynamic> lstTickets = new List<dynamic>();
            try
            {
                string url = string.Format("https://kyfw.12306.cn/otn/{0}?leftTicketDTO.train_date={1}&leftTicketDTO.from_station={2}&leftTicketDTO.to_station={3}&purpose_codes={4}", queryAction, formParams["date"], formParams["fromStation"], formParams["toStation"], formParams["purposeCode"]);
                var result = await GetAsync(url);
                JObject json = JObject.Parse(result);

                if ((bool)json["status"])
                {
                    var arrTickets = json["data"]["result"];
                    var stationNames = json["data"]["map"];

                    foreach (string item in arrTickets)
                    {
                        var arrTrain = item.Split('|');
                        string trainCode = arrTrain[3];
                        string fromCityName = stationNames[arrTrain[6]].ToString();
                        string toCityName = stationNames[arrTrain[7]].ToString();
                        string departureTime = arrTrain[8];
                        string arrivalTime = arrTrain[9];

                        lstTickets.Add(new
                        {
                            TranType = trainCode.Substring(0, 1), // 车次类别
                            TrainNo = arrTrain[2], // 车次编号
                            TrainCode = trainCode, // 车次
                            FromCityCode = arrTrain[6],
                            FromCityName = fromCityName,
                            ToCityCode = arrTrain[7],
                            ToCityName = toCityName,
                            DepartureTime = departureTime,
                            ArrivalTime = arrivalTime,
                            UseTime = arrTrain[10],
                            Form = string.Format("{0}\r{1}", fromCityName, departureTime),
                            To = string.Format("{0}\r{1}", toCityName, arrivalTime),
                            IsBuy = arrTrain[11] == "Y" ? true : false,
                            CanBuyDescript= arrTrain[11] == "Y" ? "可预订" : "不可预订",
                            YPInfo = arrTrain[12],
                            LocationCode = arrTrain[15],
                            SeatTypeCodes = GetSeatTypeCodes(arrTrain[35]),
                            SeatTypes = GetSeatTypes(arrTrain),
                            Secret = arrTrain[0],
                            Remark = GetRemark(arrTrain)
                        });
                    }
                }
            }
            catch (Exception)
            { }

            return lstTickets;
        }

        /// <summary>
        /// 检查用户是否在线
        /// </summary>
        /// <returns>true：在线；false：离线</returns>
        public static async Task<bool> CheckUserIsOnline()
        {
            Dictionary<string, string> formParams = new Dictionary<string, string>()
            {
                {"_json_att	",""}
            };
            try
            {
                var result = await PostAsync(Resources.UserIsOnlineUrl, formParams);
                JObject json = JObject.Parse(result);
                Dictionary<string, object> dicData = json["data"].ToObject<Dictionary<string, object>>();
                return Convert.ToBoolean(dicData["flag"]);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 提交订单（自动）
        /// </summary>
        /// <param name="formParams"></param>
        /// <returns>错误消息或者是获取排队信息所需参数</returns>
        public static async Task<dynamic> SubmitOrderForAutoAsync(Dictionary<string, string> formParams)
        {
            try
            {
                var result = await PostAsync(Resources.AutoSubmitOrderUrl, formParams);
                JObject json = JObject.Parse(result);
                var errInfo = json["messages"] as JArray;

                if (errInfo.Count > 0)
                {
                    return errInfo[0].ToString();
                }

                Dictionary<string, object> dicData = json["data"].ToObject<Dictionary<string, object>>();

                if (!Convert.ToBoolean(dicData["submitStatus"]))
                {
                    return dicData["errMsg"].ToString();
                }

                var strArr = dicData["result"].ToString().Split('#');
                Dictionary<string, string> dicResult = new Dictionary<string, string>()
                {
                    {"isShowValidCode",dicData["ifShowPassCode"].ToString() }, // 是否需要验证码
                    {"canChooseBeds",dicData["canChooseBeds"].ToString() }, // 是否可以选择床位
                    {"canChooseSeats",dicData["canChooseSeats"].ToString() }, // 是否可以选择位置
                    {"choose_Seats",dicData["choose_Seats"].ToString() }, // 选择的席别
                    {"isCanChooseMid",dicData["isCanChooseMid"].ToString() },
                    {"showValidCodeTime",dicData["ifShowPassCodeTime"].ToString() },
                    {"train_location",strArr[0]},
                    {"key_check_isChange",strArr[1]},
                    {"leftTicketStr",strArr[2]},
                    {"lastValue",strArr[3]}
                };

                return dicResult;
            }
            catch (Exception)
            {
                return "订单提交失败，再试一次";
            }
        }

        /// <summary>
        /// 提交订单
        /// </summary>
        /// <param name="formParams"></param>
        /// <returns></returns>
        public static async Task<string> SubmitOrderAsync(Dictionary<string, string> formParams)
        {
            string resMsg = "";
            try
            {
                var result = await PostAsync(Resources.SubmitOrderUrl, formParams);
                JObject json = JObject.Parse(result);
                var errInfo = json["messages"] as JArray;

                if (errInfo.Count > 0)
                {
                    resMsg = errInfo[0].ToString();
                }
            }
            catch (Exception)
            {
                resMsg = "提交订单发生错误，再试一次";
            }

            return resMsg;
        }

        /// <summary>
        /// 获取提交订单所需的参数值（globalRepeatSubmitToken、key_check_isChange、leftTicketStr）
        /// </summary>
        /// <returns></returns>
        public static async Task<dynamic> GetOrderTokenAsync()
        {
            string strOrderToken = "",
                strKeyCheck = "",
                strLeftTicket = "";
            try
            {
                var result = await GetAsync(Resources.OrderTokenUrl);

                if (string.IsNullOrEmpty(result))
                {
                    return "用户未登录";
                }

                string strTokenRegex = @"var\s+globalRepeatSubmitToken\s+=\s+'(?<orderToken>[^']+)';",
                    strKeyCheckRegex = @"'key_check_isChange':'(?<keyCheck>[^']+)'",
                    strTicketRegex = @"'leftTicketStr':'(?<leftTiket>[^']+)'";
                var tokenInfo = Regex.Match(result, strTokenRegex, RegexOptions.Singleline, TimeSpan.FromSeconds(2));
                var keyCheck = Regex.Match(result, strKeyCheckRegex, RegexOptions.Singleline, TimeSpan.FromSeconds(2));
                var leftTiket = Regex.Match(result, strTicketRegex, RegexOptions.Singleline, TimeSpan.FromSeconds(2));

                if (tokenInfo.Success)
                {
                    strOrderToken = tokenInfo.Groups["orderToken"].Value;
                }

                if (keyCheck.Success)
                {
                    strKeyCheck = keyCheck.Groups["keyCheck"].Value;
                }

                var resInfo = new
                {
                    OrderToken = strOrderToken,
                    KeyCheck = strKeyCheck,
                    LeftTiket = strLeftTicket
                };

                return resInfo;
            }
            catch (Exception)
            {
                return null;
            }

        }

        /// <summary>
        /// 检查订单信息
        /// </summary>
        /// <param name="formParams"></param>
        /// <returns></returns>
        public static async Task<dynamic> CheckOrderInfoAsync(Dictionary<string, string> formParams)
        {
            try
            {
                var result = await PostAsync(Resources.CheckOrderInfoUrl, formParams);
                JObject json = JObject.Parse(result);
                var errInfo = json["messages"] as JArray;

                if (errInfo.Count > 0)
                {
                    return errInfo[0].ToString();
                }

                Dictionary<string, object> dicData = json["data"].ToObject<Dictionary<string, object>>();
                bool resStatus = Convert.ToBoolean(dicData["submitStatus"]);
                return resStatus; // true：检查成功，可以继续提交订单
            }
            catch (Exception)
            {
                return "检查订单信息发生错误，再试一次";
            }
        }

        /// <summary>
        /// 获取排队信息
        /// </summary>
        /// <param name="isAutoSubmitOrder">是否自动提交订单（0：否；1：是）</param>
        /// <param name="formParams"></param>
        /// <returns></returns>
        public static async Task<dynamic> GetQueueCountAsync(int isAutoSubmitOrder, Dictionary<string, string> formParams)
        {
            try
            {
                string url = isAutoSubmitOrder == 1 ? Resources.AutoOrderQueueCountUrl : Resources.OrderQueueCountUrl;
                var result = await PostAsync(url, formParams);
                JObject json = JObject.Parse(result);
                var errInfo = json["messages"] as JArray;

                if (errInfo.Count > 0)
                {
                    return errInfo[0].ToString();
                }

                Dictionary<string, object> dicData = json["data"].ToObject<Dictionary<string, object>>();
                string seatType = string.Format("{0}0", formParams["seatType"]),
                    seatName = GetSeatTypeInfo(formParams["seatType"], null), // 座位名称
                    tiketCount = dicData["ticket"].ToString(); // 剩余票数
                string strResult = string.Format("本次列车，剩余【{0}（{1}）】张", seatName, tiketCount);

                if (Convert.ToBoolean(dicData["op_2"]))
                {
                    return strResult += string.Format("目前排队人数（{0}）已超过余票数，请更换席别或者车次", dicData["countT"]);
                }

                var queueInfo = new
                {
                    IsChangeTicket = Convert.ToBoolean(dicData["op_2"]),
                    TipInfo = strResult,
                    //SeatTypeInfo = tiketCount
                    TicketCount = tiketCount
                };

                return queueInfo;
            }
            catch (Exception)
            {
                return "获取排队信息发生错误，再试一次";
            }
        }

        /// <summary>
        /// 确认订单
        /// </summary>
        /// <param name="isAutoSubmitOrder">是否自动提交订单（0：否；1：是）</param>
        /// <param name="formParams"></param>
        /// <returns>true:订单提交成功，等待出票；false：订单提交失败</returns>
        public static async Task<dynamic> ConfirmOrderAsync(int isAutoSubmitOrder, Dictionary<string, string> formParams)
        {
            try
            {
                string url = isAutoSubmitOrder == 1 ? Resources.AutoConfirmOrderUrl : Resources.ConfirmOrderUrl;
                var result = await PostAsync(url, formParams);
                JObject json = JObject.Parse(result);
                var errInfo = json["messages"] as JArray;

                if (errInfo.Count > 0)
                {
                    return errInfo[0].ToString();
                }

                Dictionary<string, object> dicData = json["data"].ToObject<Dictionary<string, object>>();

                if (!Convert.ToBoolean(dicData["submitStatus"])) // 订单提交失败
                {
                    return string.Format("{0}，再试一次", dicData["errMsg"].ToString());
                }

                return true;
            }
            catch (Exception)
            {
                return "确认订单发生错误，再试一次";
            }
        }

        /// <summary>
        /// 获取出票等待时间
        /// </summary>
        /// <param name="orderToken">订单token</param>
        /// <returns></returns>
        public static async Task<dynamic> GetOrderWaitTimeAsync(string orderToken = "")
        {
            try
            {
                string random = DateTime.Now.GetHashCode().ToString();
                string url = string.Format("{0}?random={1}&tourFlag=dc&REPEAT_SUBMIT_TOKEN={2}&_json_att=", Resources.OrderWaitTimeUrl, random, orderToken);
                var result = await GetAsync(url);
                JObject json = JObject.Parse(result);
                Dictionary<string, object> dicData = json["data"].ToObject<Dictionary<string, object>>();

                var orderWaitInfo = new
                {
                    Status = dicData["queryOrderWaitTimeStatus"],
                    Count = dicData["count"],
                    WaitTime = dicData["waitTime"],
                    WaitCount = dicData["waitCount"],
                    OrderId = dicData["orderId"]
                };

                return orderWaitInfo;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 获取未完成的订单
        /// </summary>
        /// <returns></returns>
        public static async Task<dynamic> GetNoCompleteOrder()
        {
            Dictionary<string, string> formParams = new Dictionary<string, string>()
            {
                {"_json_att	",""}
            };
            try
            {
                var result = await PostAsync(Resources.NoCompleteOrderUrl, formParams);
                JObject json = JObject.Parse(result);
                var errInfo = json["messages"] as JArray;

                if (errInfo.Count > 0)
                {
                    return errInfo[0].ToString();
                }

                if (!result.Contains("orderDBList"))
                {
                    return "没有待支付的订单";
                }

                Dictionary<string, object> dicData = json["data"].ToObject<Dictionary<string, object>>();
                List<dynamic> lstOrders = new List<dynamic>();

                switch (dicData["to_page"].ToString())
                {
                    case "cache":
                        return "订单还处于排队状态";
                    case "db":
                        var orderList = json["data"]["orderDBList"].Children();
                        lstOrders = (from o in orderList["tickets"].Children()
                                     select new
                                     {
                                         OrderCode = o["sequence_no"], // 订单号
                                         TrainCode = o["stationTrainDTO"]["station_train_code"], // 车次号
                                         FromStationName = o["stationTrainDTO"]["from_station_name"], // 出发地
                                         StartTime = ((DateTime)o["stationTrainDTO"]["start_time"]).ToString("HH:mm"), // 发车时间
                                         ToStationName = o["stationTrainDTO"]["to_station_name"], // 目的地
                                         ArriveTime = ((DateTime)o["stationTrainDTO"]["arrive_time"]).ToString("HH:mm"), // 到达时间
                                         PassengerName = o["passengerDTO"]["passenger_name"], // 乘客姓名
                                         IdTypeName = o["passengerDTO"]["passenger_id_type_name"], // 身份证
                                         TrainDate = ((DateTime)o["start_train_date_page"]).ToString("yyyy-MM-dd"),  // 乘车日期
                                         CoachName = o["coach_name"], // 车厢
                                         SeatName = o["seat_name"], // 座位号
                                         SeatTypeName = o["seat_type_name"], // 席别类型
                                         TicketTypeName = o["ticket_type_name"], // 车票类型
                                         ReserveTime = o["reserve_time"], // 预订时间
                                         LoseTime = o["lose_time"], // 失效时间
                                         OrderStatus = o["ticket_status_name"], // 订单状态
                                         TicketPrice = string.Format("{0:C}", double.Parse(o["str_ticket_price_page"].ToString())), // 票价
                                     }).ToList<dynamic>();
                        return lstOrders;
                }

                return lstOrders;
            }
            catch (Exception)
            {
                return "查询待支付的订单发生错误，再试一下";
            }
        }
        #endregion

        /// <summary>
        /// 处理座位代码
        /// </summary>
        /// <param name="seatCodes"></param>
        /// <returns></returns>
        private static List<string> GetSeatTypeCodes(string seatCodes)
        {
            var seats = seatCodes.Replace(@"/(1)/", "w").ToCharArray().Select(s => s.ToString()).ToList();

            return seats;
        }

        /// <summary>
        /// 座位信息
        /// </summary>
        /// <param name="trains"></param>
        /// <returns></returns>
        private static List<dynamic> GetSeatTypes(string[] trains)
        {
            var seats = GetSeatTypeCodes(trains[35]);
            List<dynamic> lstSeats = new List<dynamic>();

            foreach (var code in seats)
            {
                var info = GetSeatTypeInfo(code, trains);

                lstSeats.Add(new
                {
                    seatCode = code,
                    seatInfo = info
                });
            }

            return lstSeats;
        }

        /// <summary>
        /// 获取座位信息
        /// </summary>
        /// <param name="seatCode"></param>
        /// <param name="trains"></param>
        /// <returns></returns>
        public static string GetSeatTypeInfo(string seatCode, string[] trains)
        {
            switch (seatCode)
            {
                case "Q":
                    return trains != null ? $"观光座（{trains[20]}）" : "观光座";
                case "9":
                    return trains != null ? $"商务座（{ trains[32]}）" : "商务座";
                case "P":
                    return trains != null ? $"特等座（{ trains[25]}）" : "特等座";
                case "S":
                    return trains != null ? $"一等包座（{ trains[27]}）" : "一等包座";
                case "M":
                    return trains != null ? $"一等座（{ trains[31]}）" : "一等座";
                case "O":
                    return trains != null ? $"二等座（{ trains[30]}）" : "二等座";
                case "6":
                    return trains != null ? $"高级软卧（{ trains[21]}）" : "高级软卧";
                case "4":
                    return trains != null ? $"软卧（{ trains[23]}）" : "软卧";
                case "3":
                    return trains != null ? $"硬卧（{ trains[28]}）" : "硬卧";
                case "2":
                    return trains != null ? $"软座（{ trains[24]}）" : "软座";
                case "1":
                    return trains != null ? $"硬座（{ trains[29]}）" : "硬座";
                case "W":
                    return trains != null ? $"无座（{ trains[26]}）" : "无座";
                default:
                    return trains != null ? $"其他（{ trains[22]}）" : "其他";
            }
        }

        /// <summary>
        /// 备注信息
        /// </summary>
        /// <param name="trains"></param>
        /// <returns></returns>
        private static string GetRemark(string[] trains)
        {
            var lstSeats = GetSeatTypes(trains);

            var seatInfos = from s in lstSeats
                       select s.seatInfo;

            string result = string.Join("\t", seatInfos.ToArray());

            return result;
        }
    }
}
