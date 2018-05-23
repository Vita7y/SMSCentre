using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Web;

namespace SMSCentre
{
    /// <summary>
    /// <example>
    /// SMSC smsc = new SMSC(login, password);
    /// ISmsResult r = smsc.SendSms("79999999999", "Ваш пароль: 123");
    /// string[] r = smsc.SendSms("79999999999", "http://smsc.ru\nSMSC.RU", 0, "", 0, 0, "", "maxsms=3");
    /// string[] r = smsc.SendSms("79999999999", "0605040B8423F0DC0601AE02056A0045C60C036D79736974652E72750001036D7973697465000101", 0, "", 0, 5);
    /// string[] r = smsc.SendSms("79999999999", "", 0, "", 0, 3);
    /// string[] r = smsc.SendSms("dest@mysite.com", "Ваш пароль: 123", 0, 0, 0, 8, "source@mysite.com", "subj=Confirmation");
    /// string[] r = smsc.GetSmsCost("79999999999", "Вы успешно зарегистрированы!");
    /// smsc.SendSmsMail("79999999999", "Ваш пароль: 123", 0, "0101121000");
    /// string[] r = smsc.GetStatus(12345, "79999999999");
    /// ISmsResult r = smsc.GetStatus(12345, "79999999999");
    /// string balance = smsc.GetBalance();
    /// </example>
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class SMSC
    {
        /// <summary>
        /// Логин клиента
        /// </summary>
        private readonly string _smscLogin;

        /// <summary>
        /// Пароль или MD5-хеш пароля в нижнем регистре
        /// </summary>
        private readonly string _smscPassword;

        /// <summary>
        /// Использовать метод POST
        /// </summary>
        private bool _smscPost;

        /// <summary>
        /// Использовать HTTPS протокол
        /// </summary>
        public bool SmscHttps;

        /// <summary>
        /// Кодировка сообщения (windows-1251 или koi8-r), по умолчанию используется UTF-8
        /// </summary>
        public string SmscCharset = "UTF-8";

        public event TraceInformation Trace;

        /// <summary>
        /// Константы для отправки SMS по SMTP
        /// </summary>
        public class SmtpServerParam
        {
            /// <summary>
            /// e-mail адрес отправителя
            /// <example>api@smsc.ru</example>
            /// </summary>
            public string SmtpFrom { get; }

            /// <summary>
            /// Адрес SMTP сервера
            /// <example>send.smsc.ru</example>
            /// </summary>
            public string SmtpServer { get; }

            /// <summary>
            /// Логин для SMTP сервера
            /// </summary>
            public string SmtpLogin { get; }

            /// <summary>
            /// Пароль для SMTP сервера
            /// </summary>
            public string SmtpPassword { get; }

            public SmtpServerParam(string smtpFrom, string smtpServer, string smtpLogin, string smtpPassword)
            {
                SmtpFrom = smtpFrom;
                SmtpServer = smtpServer;
                SmtpLogin = smtpLogin;
                SmtpPassword = smtpPassword;
            }
        }

        /// <summary>
        /// .ctor
        /// </summary>
        /// <param name="login">логин клиента</param>
        /// <param name="password">пароль или MD5-хеш пароля в нижнем регистре</param>
        /// <param name="smtp">Константы для отправки SMS по SMTP</param>
        public SMSC(string login, string password, SmtpServerParam smtp)
        {
            _smscLogin = login;
            _smscPassword = password;
            SMTP = smtp;
        }

        /// <summary>
        /// .ctor
        /// </summary>
        /// <param name="login">логин клиента</param>
        /// <param name="password">пароль или MD5-хеш пароля в нижнем регистре</param>
        public SMSC(string login, string password) : this(login, password, null)
        {
        }

        // ReSharper disable once InconsistentNaming
        public SmtpServerParam SMTP { get; }

        /// <summary>
        /// Метод отправки SMS
        /// </summary>
        /// <param name="phone">номер телефона</param>
        /// <param name="message">отправляемое сообщение</param>
        public ISmsResult SendSms(string phone, string message)
        {
            var res = SendSms(phone, message, 0);
            if (Convert.ToInt32(res[1]) >= 0)
            {
                return new SmsResult(res[0])
                {
                    Status = Convert.ToInt32(res[1]),
                    Phone = phone,
                    Text = message,
                    Cost = Convert.ToDecimal(res[2]),
                    Balance = Convert.ToDecimal(res[3])
                };
            }

            return new SmsErrorResult(res[0], Convert.ToInt32(res[1]), res[1]);
        }
        /// <summary>
        /// Метод отправки SMS
        /// </summary>
        /// <param name="phones">список телефонов через запятую или точку с запятой</param>
        /// <param name="message">отправляемое сообщение</param>
        /// <param name="translit">переводить или нет в транслит</param>
        /// <param name="time">необходимое время доставки в виде строки (DDMMYYhhmm, h1-h2, 0ts, +m)</param>
        /// <param name="id">идентификатор сообщения. Представляет собой 32-битное число в диапазоне от 1 до 2147483647.</param>
        /// <param name="format">формат сообщения (0 - обычное sms, 1 - flash-sms, 2 - wap-push, 3 - hlr, 4 - bin, 5 - bin-hex, 6 - ping-sms, 7 - mms, 8 - mail, 9 - call)</param>
        /// <param name="sender">имя отправителя (Sender ID). Для отключения Sender ID по умолчанию необходимо в качестве имени передать пустую строку или точку. </param>
        /// <param name="query">строка дополнительных параметров, добавляемая в URL-запрос ("valid=01:00;maxsms=3") </param>
        /// <param name="files"></param>
        /// <returns>
        /// возвращает массив строк (id, количество sms, стоимость, баланс) в случае успешной отправки
        /// либо массив строк (id, код ошибки) в случае ошибки
        /// </returns>
        public string[] SendSms(string phones, string message, int translit = 0, string time = "", int id = 0, int format = 0, string sender = "", string query = "",
            string[] files = null)
        {
            _smscPost |= files != null;

            string[] formats = {"flash=1", "push=1", "hlr=1", "bin=1", "bin=2", "ping=1", "mms=1", "mail=1", "call=1"};

            var m = SmscSendCmd("send", "cost=3&phones=" + UrlEncode(phones)
                                                              + "&mes=" + UrlEncode(message) + "&id=" + id + "&translit=" + translit
                                                              + (format > 0 ? "&" + formats[format - 1] : "") + (sender != "" ? "&sender=" + UrlEncode(sender) : "")
                                                              + (time != "" ? "&time=" + UrlEncode(time) : "") + (query != "" ? "&" + query : ""), files);

            // (id, cnt, cost, balance) или (id, -error)
            if (Convert.ToInt32(m[1]) > 0)
                OnTrace("Сообщение отправлено успешно. ID: " + m[0] + ", всего SMS: " + m[1] + ", стоимость: " + m[2] + ", баланс: " + m[3]);
            else
                OnTrace("Ошибка №" + m[1].Substring(1, 1) + (m[0] != "0" ? ", ID: " + m[0] : ""));

            return m;
        }

        /// <summary>
        ///  SMTP версия метода отправки SMS
        /// </summary>
        /// <param name="phones"></param>
        /// <param name="message"></param>
        /// <param name="translit"></param>
        /// <param name="time"></param>
        /// <param name="id"></param>
        /// <param name="format"></param>
        /// <param name="sender"></param>
        public void SendSmsMail(string phones, string message, int translit = 0, string time = "", int id = 0, int format = 0, string sender = "")
        {
            if (SMTP == null)
                throw new NullReferenceException(nameof(SMTP));

            var mail = new MailMessage();
            mail.To.Add("send@send.smsc.ru");
            mail.From = new MailAddress(SMTP.SmtpFrom, "");
            mail.Body = _smscLogin + ":" + _smscPassword + ":" + id + ":" + time + ":" + translit + "," + format + "," + sender + ":" + phones + ":" + message;
            mail.BodyEncoding = Encoding.GetEncoding(SmscCharset);
            mail.IsBodyHtml = false;

            using (var client = new SmtpClient(SMTP.SmtpServer, 25)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = false,
                UseDefaultCredentials = false
            })
            {

                if (SMTP.SmtpLogin != "")
                    client.Credentials = new NetworkCredential(SMTP.SmtpLogin, SMTP.SmtpPassword);

                client.Send(mail);
            }
        }

        /// <summary>
        /// Метод получения стоимости SMS
        /// </summary>
        /// <param name="phones">список телефонов через запятую или точку с запятой</param>
        /// <param name="message">отправляемое сообщение</param>
        /// <param name="translit">переводить или нет в транслит</param>
        /// <param name="format">формат сообщения (0 - обычное sms, 1 - flash-sms, 2 - wap-push, 3 - hlr, 4 - bin, 5 - bin-hex, 6 - ping-sms, 7 - mms, 8 - mail, 9 - call)</param>
        /// <param name="sender">имя отправителя (Sender ID)</param>
        /// <param name="query">строка дополнительных параметров, добавляемая в URL-запрос ("list=79999999999:Ваш пароль: 123\n78888888888:Ваш пароль: 456")</param>
        /// <returns>массив (стоимость, количество sms) либо массив (0, - код ошибки) в случае ошибки</returns>
        public string[] GetSmsCost(string phones, string message, int translit = 0, int format = 0, string sender = "", string query = "")
        {
            string[] formats = {"flash=1", "push=1", "hlr=1", "bin=1", "bin=2", "ping=1", "mms=1", "mail=1", "call=1"};

            var m = SmscSendCmd("send", "cost=1&phones=" + UrlEncode(phones)
                                                              + "&mes=" + UrlEncode(message) + translit + (format > 0 ? "&" + formats[format - 1] : "")
                                                              + (sender != "" ? "&sender=" + UrlEncode(sender) : "") + (query != "" ? "&query" : ""));

            // (cost, cnt) или (0, -error)
            if (Convert.ToInt32(m[1]) > 0)
                OnTrace("Стоимость рассылки: " + m[0] + ". Всего SMS: " + m[1]);
            else
                OnTrace("Ошибка №" + m[1].Substring(1, 1));

            return m;
        }

        public ISmsResult GetStatus(string messageId, string phoneNumber)
        {
            var res = GetStatus(messageId, phoneNumber, 1);
            if (Convert.ToInt32(res[0]) >= 0)
            {
                return new SmsStatusResult(messageId)
                {
                    Status = Convert.ToInt32(res[0]),
                    SendTime = DateTime.FromFileTimeUtc(Convert.ToInt32(res[3])),
                    DeliveredTime = DateTime.FromFileTimeUtc(Convert.ToInt32(res[1])),
                    Phone = res[4],
                    Sender = res[6],
                    StatusDescription = res[7],
                    Text = res[8]
                };
            }

            return new SmsErrorResult(messageId, Convert.ToInt32(res[0]), res[1]);
        }

        /// <summary>
        /// Метод проверки статуса отправленного SMS или HLR-запроса
        /// </summary>
        /// <param name="id">ID сообщения или список ID через запятую</param>
        /// <param name="phone">номер телефона или список номеров через запятую</param>
        /// <param name="all">вернуть все данные отправленного SMS, включая текст сообщения (0,1 или 2)</param>
        /// <returns>
        /// возвращает массив (для множественного запроса возвращается массив с единственным элементом, равным 1.
        ///   для одиночного SMS-сообщения: (статус, время изменения, код ошибки доставки)
        ///   для HLR-запроса:  (статус, время изменения, код ошибки SMS, код IMSI SIM-карты, номер сервис-центра, код страны регистрации, код оператора, название страны регистрации, название оператора, название роуминговой страны, название роумингового оператора)
        ///     при all = 1 дополнительно возвращаются элементы в конце массива: (время отправки, номер телефона, стоимость, sender id, название статуса, текст сообщения)
        ///     при all = 2 дополнительно возвращаются элементы: (страна, оператор, регион)
        /// при множественном запросе(данные по статусам сохраняются в двумерном массиве D2Res):
        ///   если all = 0, то для каждого сообщения или HLR-запроса дополнительно возвращается: ID сообщения, номер телефона
        ///   если all = 1 или all = 2, то в ответ добавляется: ID сообщения
        /// либо массив (0, -код ошибки) в случае ошибки
        /// </returns>
        public string[] GetStatus(string id, string phone, int all = 0)
        {
            var m = SmscSendCmd("status", $"phone={UrlEncode(phone)}&id={UrlEncode(id)}&all={all}");

            // (status, time, err, ...) или (0, -error)
            if (id.IndexOf(',') == -1)
            {
                if (m[1] != "" && Convert.ToInt32(m[1]) >= 0)
                {
                    var timestamp = Convert.ToInt32(m[1]);
                    var offset = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                    var date = offset.AddSeconds(timestamp);

                    OnTrace($"Статус SMS = {m[0]}{(timestamp > 0 ? ", время изменения статуса - " + date.ToLocalTime() : "")}");
                }
                else
                    OnTrace($"Ошибка №{m[1].Substring(1, 1)}");

                // var idx = all == 1 ? 9 : 12;
                // if (all > 0 && m.Length > idx && (m.Length < idx + 5 || m[idx + 5] != "HLR"))
                //    m = String.Join(",", m).Split(",".ToCharArray(), idx);
            }
            else
            {
                if (m.Length == 1 && m[0].IndexOf('-') == 2)
                    return m[0].Split(',');

                Array.Resize(ref m, 1);
                m[0] = "1";
            }

            return m;
        }

        /// <summary>
        /// Метод получения баланса
        /// </summary>
        /// <returns>возвращает баланс в виде строки или пустую строку в случае ошибки</returns>
        public string GetBalance()
        {
            var m = SmscSendCmd("balance", "");

            //(balance) или (0, -error)
            if (m.Length == 1)
                OnTrace("Сумма на счете: " + m[0]);
            else
                OnTrace("Ошибка №" + m[1].Substring(1, 1));

            return m.Length == 1 ? m[0] : "";
        }

        /// <summary>
        /// Метод вызова запроса. Формирует URL и делает 3 попытки чтения
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="arg"></param>
        /// <param name="files"></param>
        /// <returns></returns>
        private string[] SmscSendCmd(string cmd, string arg, string[] files = null)
        {
            arg = "login=" + UrlEncode(_smscLogin) + "&psw=" + UrlEncode(_smscPassword) + "&fmt=1&charset=" + SmscCharset + "&" + arg;

            var url = (SmscHttps ? "https" : "http") + "://smsc.ru/sys/" + cmd + ".php" + (_smscPost ? "" : "?" + arg);

            var ret = "";
            var i = 0;

            do
            {
                if (i++ > 0)
                    url = url.Replace("smsc.ru/", "www" + i + ".smsc.ru/");

                var request = (HttpWebRequest) WebRequest.Create(url);

                if (_smscPost)
                {
                    request.Method = "POST";

                    var boundary = "----------" + DateTime.Now.Ticks.ToString("x");
                    var boundaryBytes = Encoding.ASCII.GetBytes("--" + boundary + "--\r\n");
                    var sb = new StringBuilder();

                    var output = new byte[0];

                    if (files == null)
                    {
                        request.ContentType = "application/x-www-form-urlencoded";
                        output = Encoding.UTF8.GetBytes(arg);
                        request.ContentLength = output.Length;
                    }
                    else
                    {
                        request.ContentType = "multipart/form-data; boundary=" + boundary;

                        var par = arg.Split('&');
                        var fl = files.Length;

                        for (int pcnt = 0; pcnt < par.Length + fl; pcnt++)
                        {
                            sb.Clear();

                            sb.Append("--");
                            sb.Append(boundary);
                            sb.Append("\r\n");
                            sb.Append("Content-Disposition: form-data; name=\"\"");

                            var pof = pcnt < fl;
                            var nv = new String[0];

                            if (pof)
                            {
                                sb.Append("File" + (pcnt + 1));
                                sb.Append("\"; filename = \"");
                                sb.Append(Path.GetFileName(files[pcnt]));
                            }
                            else
                            {
                                nv = par[pcnt - fl].Split('=');
                                sb.Append(nv[0]);
                            }

                            sb.Append("\"");

                            sb.Append("\r\n");
                            sb.Append("Content-Type: ");
                            sb.Append(pof ? "application/octet-stream" : "text/plain; charset=\"" + SmscCharset + "\"");
                            sb.Append("\r\n");
                            sb.Append("Content-Transfer-Encoding: binary");
                            sb.Append("\r\n");
                            sb.Append("\r\n");

                            var postHeader = sb.ToString();
                            var postHeaderBytes = Encoding.UTF8.GetBytes(postHeader);

                            output = ConcatBytes(output, postHeaderBytes);

                            if (pof)
                            {
                                using (var fileStream = new FileStream(files[pcnt], FileMode.Open, FileAccess.Read))
                                {
                                    var buffer = new Byte[checked((uint) Math.Min(4096, (int) fileStream.Length))];
                                    int bytesRead;
                                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                                    {
                                        var tbuf = buffer;
                                        Array.Resize(ref tbuf, bytesRead);
                                        output = ConcatBytes(output, tbuf);
                                    }
                                }
                            }
                            else
                            {
                                var vl = Encoding.UTF8.GetBytes(nv[1]);
                                output = ConcatBytes(output, vl);
                            }

                            output = ConcatBytes(output, Encoding.UTF8.GetBytes("\r\n"));
                        }

                        output = ConcatBytes(output, boundaryBytes);

                        request.ContentLength = output.Length;
                    }

                    var requestStream = request.GetRequestStream();
                    requestStream.Write(output, 0, output.Length);
                }

                try
                {
                    var response = (HttpWebResponse) request.GetResponse();
                    var res = response.GetResponseStream();
                    if (res != null)
                    {
                        using (var sr = new StreamReader(res))
                        {
                            ret = sr.ReadToEnd();
                        }
                    }
                }
                catch (WebException)
                {
                    ret = "";
                }
            } while (ret == "" && i < 5);

            if (ret == "")
            {
                OnTrace("Ошибка чтения адреса: " + url);

                ret = ","; // фиктивный ответ
            }

            var delim = ',';

            if (cmd == "status")
            {
                var par = arg.Split('&');

                for (i = 0; i < par.Length; i++)
                {
                    var lr = par[i].Split("=".ToCharArray(), 2);

                    if (lr[0] == "id" && lr[1].IndexOf("%2c", StringComparison.Ordinal) > 0) // запятая в id - множественный запрос
                        delim = '\n';
                }
            }

            return ret.Split(delim);
        }

        /// <summary>
        /// Кодирование параметра в HTTP-запросе
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string UrlEncode(string str)
        {
            if (_smscPost)
                return str;

            return HttpUtility.UrlEncode(str);
        }

        /// <summary>
        /// Объединение байтовых массивов
        /// </summary>
        /// <param name="farr"></param>
        /// <param name="sarr"></param>
        /// <returns></returns>
        private static byte[] ConcatBytes(byte[] farr, byte[] sarr)
        {
            var opl = farr.Length;

            Array.Resize(ref farr, farr.Length + sarr.Length);
            Array.Copy(sarr, 0, farr, opl, sarr.Length);

            return farr;
        }

        protected virtual void OnTrace(string information)
        {
            Trace?.Invoke(this, new TraceEventArgs(information));
        }
    }

    public delegate void TraceInformation(object sender, TraceEventArgs e);

    public class TraceEventArgs
    {
        public TraceEventArgs(string information)
        {
            Information = information;
        }

        public string Information { get; }
    }
}

