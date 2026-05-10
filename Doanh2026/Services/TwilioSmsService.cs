using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Doanh2026.Services
{
    public sealed class SmsSendResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class TwilioSmsService
    {
        private readonly string accountSid;
        private readonly string authToken;
        private readonly string verifyServiceSid;

        public TwilioSmsService()
        {
            accountSid = ConfigurationManager.AppSettings["Twilio:AccountSid"];
            authToken = ConfigurationManager.AppSettings["Twilio:AuthToken"];
            verifyServiceSid = ConfigurationManager.AppSettings["Twilio:VerifyServiceSid"];
        }

        public SmsSendResult SendOtpSms(string toPhoneNumber)
        {
            if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken) || string.IsNullOrWhiteSpace(verifyServiceSid))
            {
                return new SmsSendResult
                {
                    Success = false,
                    Message = "Twilio Verify chưa được cấu hình. Hãy điền Twilio:AccountSid, Twilio:AuthToken và Twilio:VerifyServiceSid trong Web.config."
                };
            }

            var normalizedTo = NormalizePhoneVN(toPhoneNumber);
            if (string.IsNullOrWhiteSpace(normalizedTo))
            {
                return new SmsSendResult
                {
                    Success = false,
                    Message = "Số điện thoại không hợp lệ. Dùng định dạng 0xxxxxxxxx hoặc +84xxxxxxxxx."
                };
            }

            var endpoint = $"https://verify.twilio.com/v2/Services/{verifyServiceSid}/Verifications";

            using (var httpClient = new HttpClient())
            {
                var credential = Convert.ToBase64String(Encoding.ASCII.GetBytes(accountSid + ":" + authToken));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credential);

                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("To", normalizedTo),
                    new KeyValuePair<string, string>("Channel", "sms")
                });

                var response = httpClient.PostAsync(endpoint, form).Result;
                var responseBody = response.Content.ReadAsStringAsync().Result;

                if (response.IsSuccessStatusCode)
                {
                    return new SmsSendResult { Success = true, Message = "OTP đã được gửi." };
                }

                return new SmsSendResult
                {
                    Success = false,
                    Message = "Twilio trả lỗi: " + responseBody
                };
            }
        }

        public SmsSendResult VerifyOtpSms(string toPhoneNumber, string otpCode)
        {
            if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken) || string.IsNullOrWhiteSpace(verifyServiceSid))
            {
                return new SmsSendResult
                {
                    Success = false,
                    Message = "Twilio Verify chưa được cấu hình. Hãy điền Twilio:AccountSid, Twilio:AuthToken và Twilio:VerifyServiceSid trong Web.config."
                };
            }

            var normalizedTo = NormalizePhoneVN(toPhoneNumber);
            if (string.IsNullOrWhiteSpace(normalizedTo))
            {
                return new SmsSendResult
                {
                    Success = false,
                    Message = "Số điện thoại không hợp lệ. Dùng định dạng 0xxxxxxxxx hoặc +84xxxxxxxxx."
                };
            }

            var endpoint = $"https://verify.twilio.com/v2/Services/{verifyServiceSid}/VerificationCheck";

            using (var httpClient = new HttpClient())
            {
                var credential = Convert.ToBase64String(Encoding.ASCII.GetBytes(accountSid + ":" + authToken));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credential);

                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("To", normalizedTo),
                    new KeyValuePair<string, string>("Code", otpCode)
                });

                var response = httpClient.PostAsync(endpoint, form).Result;
                var responseBody = response.Content.ReadAsStringAsync().Result;

                if (response.IsSuccessStatusCode)
                {
                    if (responseBody.IndexOf("\"status\": \"approved\"", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return new SmsSendResult { Success = true, Message = "OTP hợp lệ." };
                    }

                    return new SmsSendResult
                    {
                        Success = false,
                        Message = "OTP chưa hợp lệ: " + responseBody
                    };
                }

                return new SmsSendResult
                {
                    Success = false,
                    Message = "Twilio trả lỗi: " + responseBody
                };
            }
        }

        public static string NormalizePhoneVN(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return null;
            }

            var digits = new string(phone.Trim().Where(char.IsDigit).ToArray());
            if (digits.StartsWith("84"))
            {
                digits = "+" + digits;
            }
            else if (digits.StartsWith("0"))
            {
                digits = "+84" + digits.Substring(1);
            }
            else
            {
                return null;
            }

            if (!digits.StartsWith("+84") || digits.Length < 11 || digits.Length > 12)
            {
                return null;
            }

            return digits;
        }
    }
}