using System.IO;
using System.Linq;
using SMSCentre;
using Xunit;

namespace Test_SMSCentre
{
    [Trait("Category", "Integration")]
    public class TestSmsCentre
    {
        private readonly string _login;
        private readonly string _password;
        private readonly string _phone;

        public TestSmsCentre()
        {
            var list = File.ReadLines("c:\\src\\SmsCredentials.txt").ToList();
            _login = list[0];
            _password = list[1];
            _phone = list[2];
        }

        [Fact]
        public void TestSendSmsValidNumber()
        {
            var message = "Test";
            var sms = new SMSCentre.SMSC(_login, _password);
            var res = sms.SendSms(_phone, message);
            Assert.True(res is ISmsResult);
            Assert.True(res is SmsResult);
            var smsRes = res as SmsResult;
            Assert.Equal(_phone, smsRes.Phone);
            Assert.Equal(message, smsRes.Text);
            Assert.True(smsRes.Balance > 0);
            Assert.True(smsRes.Cost > 0);
        }

        [Fact]
        public void TestSendSmsInValidNumber()
        {
            var sms = new SMSCentre.SMSC(_login, _password);
            var res = sms.SendSms("111", "Test");
            Assert.True(res is ISmsResult);
            Assert.True(res is SmsErrorResult);
            var smsRes = res as SmsErrorResult;
            Assert.Equal(-7, smsRes.ErrorCode);
            Assert.NotEmpty(smsRes.Id);
        }

        [Fact]
        public void TestSmsStatus()
        {
            var sms = new SMSCentre.SMSC(_login, _password);
            var res = sms.GetStatus("1", _phone);
            Assert.True(res is ISmsResult);
            Assert.True(res is SmsStatusResult);
            var smsRes = res as SmsStatusResult;
            Assert.Equal(1, smsRes.Status);
            Assert.Equal("Доставлено", smsRes.StatusDescription);
            Assert.Equal("Filuet", smsRes.Sender);
            Assert.Equal("Test", smsRes.Text);
        }

        [Fact]
        public void TestSmsStatusInCorrectId()
        {
            var sms = new SMSCentre.SMSC(_login, _password);
            var res = sms.GetStatus("11111111111", _phone);
            Assert.True(res is ISmsResult);
            Assert.True(res is SmsErrorResult);
            var smsRes = res as SmsErrorResult;
            Assert.Equal(-3, smsRes.ErrorCode);
            Assert.Equal("11111111111", smsRes.Id);
        }


        [Fact]
        public void TestBalance()
        {
            var sms = new SMSCentre.SMSC(_login, _password);
            var res = sms.GetBalance();
            Assert.NotEmpty(res);
            Assert.True(decimal.Parse(res) > 0);

        }

    }
}
