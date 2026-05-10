using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Doanh2026.Services
{
    public class VnPayService
    {
        private readonly string vnpUrl;
        private readonly string vnpTmnCode;
        private readonly string vnpHashSecret;
        private readonly string vnpReturnUrl;

        public VnPayService()
        {
            vnpUrl = GetSetting(
                "VNPAY:Url",
                "Vnpay:Url",
                "VNPAY_URL",
                "VNPAY_URL_ENV"
            );

            vnpTmnCode = GetSetting(
                "VNPAY:TmnCode",
                "Vnpay:TmnCode",
                "VNPAY_TMN",
                "VNPAY_TMN_CODE"
            );

            vnpHashSecret = GetSetting(
                "VNPAY:HashSecret",
                "Vnpay:HashSecret",
                "VNPAY_HASH",
                "VNPAY_HASH_SECRET"
            );

            vnpReturnUrl = GetSetting(
                "VNPAY:ReturnUrl",
                "Vnpay:ReturnUrl",
                "VNPAY_RETURNURL",
                "VNPAY_RETURN_URL"
            );
        }

        private string GetSetting(params string[] keys)
        {
            foreach (var key in keys)
            {
                try
                {
                    var value = ConfigurationManager.AppSettings[key];

                    if (!string.IsNullOrWhiteSpace(value))
                        return value.Trim();
                }
                catch { }

                try
                {
                    var env = Environment.GetEnvironmentVariable(key);

                    if (!string.IsNullOrWhiteSpace(env))
                        return env.Trim();
                }
                catch { }
            }

            return string.Empty;
        }

        public string CreatePaymentUrl(
            int orderId,
            decimal amount,
            string ipAddress,
            string bankCode = null,
            IDictionary<string, string> extraParams = null)
        {
            var vnpParams = new SortedDictionary<string, string>(StringComparer.Ordinal);

            vnpParams.Add("vnp_Version", "2.1.0");
            vnpParams.Add("vnp_Command", "pay");
            vnpParams.Add("vnp_TmnCode", vnpTmnCode);

            // amount x100
            vnpParams.Add(
                "vnp_Amount",
                ((long)(amount * 100)).ToString()
            );

            vnpParams.Add("vnp_CurrCode", "VND");
            vnpParams.Add("vnp_TxnRef", orderId.ToString());

            vnpParams.Add(
                "vnp_OrderInfo",
                NormalizeOrderInfo("Thanh toan don hang " + orderId)
            );

            vnpParams.Add("vnp_OrderType", "other");
            vnpParams.Add("vnp_Locale", "vn");

            // Return URL
            string returnUrl = vnpReturnUrl ?? "";

            try
            {
                if (returnUrl.StartsWith("/") &&
                    HttpContext.Current != null)
                {
                    var req = HttpContext.Current.Request;

                    var baseUrl =
                        req.Url.GetLeftPart(UriPartial.Authority);

                    returnUrl =
                        baseUrl.TrimEnd('/') + returnUrl;
                }
            }
            catch { }

            vnpParams.Add("vnp_ReturnUrl", returnUrl);

            // Force IPv4 localhost
            if (ipAddress == "::1")
                ipAddress = "127.0.0.1";

            vnpParams.Add("vnp_IpAddr", ipAddress);

            vnpParams.Add(
                "vnp_CreateDate",
                DateTime.Now.ToString("yyyyMMddHHmmss")
            );

            if (!string.IsNullOrWhiteSpace(bankCode))
            {
                vnpParams.Add("vnp_BankCode", bankCode);
            }

            // Merge extra params
            if (extraParams != null)
            {
                foreach (var kv in extraParams)
                {
                    if (string.IsNullOrWhiteSpace(kv.Value))
                        continue;

                    vnpParams[kv.Key] = kv.Value;
                }
            }

            // Build query + hash data
            var query = new StringBuilder();
            var hashData = new StringBuilder();

            foreach (var kv in vnpParams)
            {
                var encodedKey = WebUtility.UrlEncode(kv.Key);
                var encodedValue = WebUtility.UrlEncode(kv.Value);

                if (query.Length > 0)
                    query.Append("&");

                query.Append(encodedKey + "=" + encodedValue);

                if (hashData.Length > 0)
                    hashData.Append("&");

                hashData.Append(encodedKey + "=" + encodedValue);
            }

            var secureHash =
                HmacSHA512(vnpHashSecret, hashData.ToString());

            var paymentUrl =
                vnpUrl +
                "?" +
                query +
                "&vnp_SecureHash=" +
                secureHash;

            return paymentUrl;
        }

        public bool ValidateResponse(
            IDictionary<string, string> parameters)
        {
            if (!parameters.ContainsKey("vnp_SecureHash"))
                return false;

            var receivedHash =
                parameters["vnp_SecureHash"] ?? "";

            var filtered = parameters
                .Where(kv =>
                    !string.Equals(
                        kv.Key,
                        "vnp_SecureHash",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.Equals(
                        kv.Key,
                        "vnp_SecureHashType",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.IsNullOrWhiteSpace(kv.Value))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToList();

            var signData = string.Join(
                "&",
                filtered.Select(kv =>
                    $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"
                )
            );

            var checksum =
                HmacSHA512(vnpHashSecret, signData);

            return string.Equals(
                checksum,
                receivedHash,
                StringComparison.OrdinalIgnoreCase
            );
        }

        public bool VerifyVnpaySignature(
            IDictionary<string, string> parameters)
        {
            return ValidateResponse(parameters);
        }

        public string ComputeSecureHash(
            IDictionary<string, string> parameters)
        {
            var filtered = parameters
                .Where(kv =>
                    !string.Equals(
                        kv.Key,
                        "vnp_SecureHash",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.Equals(
                        kv.Key,
                        "vnp_SecureHashType",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.IsNullOrWhiteSpace(kv.Value))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToList();

            var signData = string.Join(
                "&",
                filtered.Select(kv =>
                    $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"
                )
            );

            return HmacSHA512(vnpHashSecret, signData);
        }

        public static string NormalizeOrderInfo(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            var normalized =
                input.Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder();

            foreach (var ch in normalized)
            {
                var uc =
                    System.Globalization.CharUnicodeInfo
                    .GetUnicodeCategory(ch);

                if (uc !=
                    System.Globalization.UnicodeCategory
                    .NonSpacingMark)
                {
                    sb.Append(ch);
                }
            }

            var result =
                sb.ToString()
                  .Normalize(NormalizationForm.FormC);

            result =
                System.Text.RegularExpressions.Regex.Replace(
                    result,
                    @"[^A-Za-z0-9\s\-_,\.]+",
                    ""
                );

            result =
                System.Text.RegularExpressions.Regex.Replace(
                    result,
                    @"\s+",
                    " "
                ).Trim();

            return result;
        }

        private static string HmacSHA512(
            string key,
            string data)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var dataBytes = Encoding.UTF8.GetBytes(data);

            using (var hmac = new HMACSHA512(keyBytes))
            {
                var hash = hmac.ComputeHash(dataBytes);

                return BitConverter
                    .ToString(hash)
                    .Replace("-", "")
                    .ToLowerInvariant();
            }
        }
    }
}