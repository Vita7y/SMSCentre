using System;

namespace SMSCentre
{
    public interface ISmsResult
    {
        string Id { get; }
    }

    public class SmsResult : ISmsResult
    {
        public SmsResult(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public int Status { get; set; }

        public decimal Cost { get; set; }

        public decimal Balance { get; set; }

        public string Phone { get; set; }
        public string Text { get; set; }
    }

    public class SmsStatusResult : ISmsResult
    {
        public SmsStatusResult(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public int Status { get; set; }

        public string StatusDescription { get; set; }

        public DateTime SendTime { get; set; }

        public DateTime DeliveredTime { get; set; }

        public string Text { get; set; }

        public string Sender { get; set; }

        public string Phone { get; set; }
    }


    public class SmsErrorResult : ISmsResult
    {
        public SmsErrorResult(string id, int error, string description)
        {
            Id = id;
            ErrorCode = error;
            Description = description;
        }
        public string Id { get; }

        public int ErrorCode { get; }

        public string Description { get; }

    }
}