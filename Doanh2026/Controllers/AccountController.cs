using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Security;
using Doanh2026.Models;
using Doanh2026.Services;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Data.Entity;

namespace Doanh2026.Controllers
{
    public class AccountController : Controller
    {
        private Doanh2026Entities db = new Doanh2026Entities();
        private readonly TwilioSmsService smsService = new TwilioSmsService();

        private const string SessionOtpPhone = "RegisterOtpPhone";
        private const string SessionOtpExpireAt = "RegisterOtpExpireAt";
        private const string SessionOtpLastSentAt = "RegisterOtpLastSentAt";
        private const string SessionOtpTryCount = "RegisterOtpTryCount";
        private const string SessionPendingUserId = "PendingRegisterUserId";
        private const string SessionPendingUserPhone = "PendingRegisterPhone";

        // GET: Account/Login (Fix 404 khi truy cập URL trực tiếp)
        [HttpGet]
        public ActionResult Login()
        {
            return RedirectToAction("Index", "Home", new { auth = "login" });
        }

        // POST: Account/Login (AJAX JSON)
        [HttpPost]
        public JsonResult Login(string email, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                    return Json(new { success = false, message = "Vui lòng nhập đủ Email và Mật khẩu!" });

                var emailLower = email.Trim().ToLower();
                var user = db.NguoiDungs.Include(u => u.VaiTro).FirstOrDefault(u => u.Email.ToLower() == emailLower && u.MatKhau == password);

                if (user == null)
                    return Json(new { success = false, message = "Email hoặc Mật khẩu không chính xác!" });

                if (user.TrangThaiTaiKhoan == "KhoaTaiKhoan")
                    return Json(new { success = false, message = "Tài khoản của bạn đã bị khóa. Liên hệ hỗ trợ!" });

                if (!string.Equals(user.TrangThaiTaiKhoan, "HoatDong", StringComparison.OrdinalIgnoreCase))
                {
                    // Tạo phiên chờ xác minh giống flow đăng ký
                    var normalizedPhone = TwilioSmsService.NormalizePhoneVN(user.SoDienThoai);
                    Session[SessionPendingUserId] = user.MaNguoiDung;
                    Session[SessionPendingUserPhone] = normalizedPhone;

                    var otpResult = SendPendingRegistrationOtp(user.MaNguoiDung, normalizedPhone);

                    return Json(new {
                        success = false,
                        needOtpVerification = true,
                        otpSent = otpResult.Success,
                        phone = normalizedPhone,
                        message = otpResult.Success ? "Tài khoản chưa kích hoạt. Mã OTP đã được gửi tới số của bạn." : ("Tài khoản chưa kích hoạt. Không gửi được OTP: " + otpResult.Message)
                    });
                }

                // Lưu vào Session
                Session["UserId"] = user.MaNguoiDung;
                Session["UserName"] = user.HoTen;
                Session["UserEmail"] = user.Email;
                Session["UserRole"] = user.MaVaiTro;
                Session["UserRoleName"] = user.VaiTro?.TenVaiTro;

                // Tích hợp FormsAuthentication để [Authorize] hoạt động
                FormsAuthentication.SetAuthCookie(user.Email, false);

                return Json(new {
                    success = true,
                    message = "Đăng nhập thành công! Chào mừng " + user.HoTen,
                    userName = user.HoTen,
                    userRole = user.MaVaiTro,
                    userRoleName = user.VaiTro?.TenVaiTro
                });
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                return Json(new { success = false, message = "Lỗi hệ thống (Login): " + msg });
            }
        }

        // GET: Account/Register (Fix 404)
        [HttpGet]
        public ActionResult Register()
        {
            return RedirectToAction("Index", "Home", new { auth = "register" });
        }

        // POST: Account/Register (AJAX JSON)
        [HttpPost]
        public JsonResult Register(string hoTen, string email, string password, string confirmPassword, string soDienThoai, string diaChi)
        {
            try
            {
                if (string.IsNullOrEmpty(hoTen) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                    return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin bắt buộc!" });

                var normalizedPhone = TwilioSmsService.NormalizePhoneVN(soDienThoai);
                if (string.IsNullOrEmpty(normalizedPhone))
                    return Json(new { success = false, message = "Số điện thoại không hợp lệ!" });

                if (password != confirmPassword)
                    return Json(new { success = false, message = "Mật khẩu xác nhận không khớp!" });

                if (password.Length < 8)
                    return Json(new { success = false, message = "Mật khẩu phải có ít nhất 8 ký tự!" });

                if (!Regex.IsMatch(password, "[A-Z]") || !Regex.IsMatch(password, "[a-z]") || !Regex.IsMatch(password, "[^A-Za-z0-9]"))
                    return Json(new { success = false, message = "Mật khẩu phải gồm chữ hoa, chữ thường và ký tự đặc biệt!" });

                var emailLower = email.Trim().ToLower();
                var existing = db.NguoiDungs.FirstOrDefault(u => u.Email.ToLower() == emailLower);
                if (existing != null)
                    return Json(new { success = false, message = "Email này đã được đăng ký! Vui lòng dùng Email khác." });

                // Tìm MaVaiTro động theo tên để tránh lỗi FK
                var customerRole = db.VaiTroes.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("khách hàng") || r.TenVaiTro.ToLower().Contains("customer"));
                if (customerRole == null)
                {
                    var allRoles = string.Join(", ", db.VaiTroes.Select(r => $"[{r.MaVaiTro}:{r.TenVaiTro}]"));
                    return Json(new { success = false, message = "Không tìm thấy vai trò 'Khách hàng' trong hệ thống. Các vai trò hiện có: " + allRoles });
                }
                int roleId = customerRole.MaVaiTro;

                var newUser = new NguoiDung
                {
                    HoTen = hoTen.Trim(),
                    Email = email.Trim().ToLower(),
                    MatKhau = password,
                    SoDienThoai = normalizedPhone,
                    DiaChi = diaChi,
                    MaVaiTro = roleId,
                    TrangThaiTaiKhoan = "ChoXacMinh",
                    NgayDangKy = DateTime.Now
                };

                db.NguoiDungs.Add(newUser);
                db.SaveChanges();

                Session[SessionPendingUserId] = newUser.MaNguoiDung;
                Session[SessionPendingUserPhone] = normalizedPhone;

                var otpResult = SendPendingRegistrationOtp(newUser.MaNguoiDung, normalizedPhone);

                return Json(new {
                    success = true,
                    needOtpVerification = true,
                    otpSent = otpResult.Success,
                    phone = normalizedPhone,
                    message = otpResult.Success
                        ? "Đăng ký thành công! Mã OTP đã được gửi tới số điện thoại của bạn."
                        : "Đăng ký thành công nhưng chưa gửi được OTP: " + otpResult.Message + " Vui lòng bấm gửi lại OTP.",
                    userName = newUser.HoTen
                });
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) {
                    msg += " | " + ex.InnerException.Message;
                    if (ex.InnerException.InnerException != null)
                        msg += " | " + ex.InnerException.InnerException.Message;
                }
                return Json(new { success = false, message = "Lỗi hệ thống (Register): " + msg });
            }
        }

        // POST: Account/SendOtp - Gửi OTP SMS qua Twilio cho tài khoản chờ xác minh
        [HttpPost]
        public JsonResult SendOtp()
        {
            try
            {
                var pendingUserId = (int?)(Session[SessionPendingUserId]) ?? 0;
                var normalizedPhone = Session[SessionPendingUserPhone]?.ToString();

                if (pendingUserId <= 0 || string.IsNullOrEmpty(normalizedPhone))
                    return Json(new { success = false, message = "Không có tài khoản chờ xác minh. Vui lòng đăng ký lại." });

                if (string.IsNullOrEmpty(normalizedPhone))
                    return Json(new { success = false, message = "Số điện thoại không hợp lệ!" });

                var lastSentAt = Session[SessionOtpLastSentAt] as DateTime?;
                if (lastSentAt.HasValue && DateTime.Now.Subtract(lastSentAt.Value).TotalSeconds < 60)
                    return Json(new { success = false, message = "Vui lòng chờ 60 giây trước khi gửi lại OTP." });

                var smsResult = smsService.SendOtpSms(normalizedPhone);
                if (!smsResult.Success)
                    return Json(new { success = false, message = smsResult.Message });

                Session[SessionOtpPhone] = normalizedPhone;
                Session[SessionOtpExpireAt] = DateTime.Now.AddMinutes(5);
                Session[SessionOtpLastSentAt] = DateTime.Now;
                Session[SessionOtpTryCount] = 0;

                return Json(new { success = true, message = "OTP đã được gửi qua SMS. Mã có hiệu lực trong 5 phút." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Không thể gửi OTP: " + ex.Message });
            }
        }

        // POST: Account/VerifyOtp - Xác minh OTP SMS
        [HttpPost]
        public JsonResult VerifyOtp(string otp)
        {
            try
            {
                var pendingUserId = (int?)(Session[SessionPendingUserId]) ?? 0;
                var normalizedPhone = Session[SessionPendingUserPhone]?.ToString();
                if (pendingUserId <= 0 || string.IsNullOrEmpty(normalizedPhone) || string.IsNullOrWhiteSpace(otp))
                    return Json(new { success = false, message = "Thiếu số điện thoại hoặc mã OTP." });

                var otpPhone = Session[SessionOtpPhone]?.ToString();
                var expireAt = Session[SessionOtpExpireAt] as DateTime?;
                int tryCount = (int?)(Session[SessionOtpTryCount]) ?? 0;

                if (string.IsNullOrEmpty(otpPhone) || !expireAt.HasValue)
                    return Json(new { success = false, message = "Không tìm thấy phiên OTP. Vui lòng gửi OTP mới." });

                if (!string.Equals(otpPhone, normalizedPhone, StringComparison.Ordinal))
                    return Json(new { success = false, message = "Số điện thoại xác minh không khớp với số đã nhận OTP." });

                if (DateTime.Now > expireAt.Value)
                {
                    ClearRegisterOtpSession();
                    return Json(new { success = false, message = "Mã OTP đã hết hạn. Vui lòng gửi lại OTP." });
                }

                var verifyResult = smsService.VerifyOtpSms(normalizedPhone, otp.Trim());
                if (!verifyResult.Success)
                {
                    tryCount += 1;
                    Session[SessionOtpTryCount] = tryCount;
                    if (tryCount >= 5)
                    {
                        ClearRegisterOtpSession();
                        return Json(new { success = false, message = "Bạn đã nhập sai OTP quá nhiều lần. Vui lòng gửi lại OTP mới." });
                    }

                    return Json(new { success = false, message = verifyResult.Message });
                }

                var user = db.NguoiDungs.Include(u => u.VaiTro).FirstOrDefault(u => u.MaNguoiDung == pendingUserId);
                if (user == null)
                {
                    ClearRegisterOtpSession();
                    return Json(new { success = false, message = "Không tìm thấy tài khoản chờ xác minh." });
                }

                user.TrangThaiTaiKhoan = "HoatDong";
                db.SaveChanges();

                Session["UserId"] = user.MaNguoiDung;
                Session["UserName"] = user.HoTen;
                Session["UserEmail"] = user.Email;
                Session["UserRole"] = user.MaVaiTro;
                Session["UserRoleName"] = user.VaiTro?.TenVaiTro;

                FormsAuthentication.SetAuthCookie(user.Email, false);

                ClearRegisterOtpSession();

                return Json(new { success = true, message = "Xác minh số điện thoại thành công! Tài khoản đã được kích hoạt." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi xác minh OTP: " + ex.Message });
            }
        }

        // GET: Account/Logout
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Index", "Home");
        }

        // GET: Account/Profile
        [HttpGet]
        public ActionResult Profile()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Index", "Home");

            int userId = (int)Session["UserId"];
            var user = db.NguoiDungs.Find(userId);
            if (user == null) { Session.Clear(); return RedirectToAction("Index", "Home"); }

            return View(user);
        }

        // POST: Account/UpdateProfile
        [HttpPost]
        public JsonResult UpdateProfile(string hoTen, string soDienThoai, string diaChi)
        {
            try
            {
                if (Session["UserId"] == null)
                    return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });

                int userId = (int)Session["UserId"];
                var user = db.NguoiDungs.Find(userId);
                if (user == null)
                    return Json(new { success = false, message = "Không tìm thấy tài khoản." });

                hoTen = (hoTen ?? string.Empty).Trim();
                diaChi = (diaChi ?? string.Empty).Trim();
                var normalizedPhone = TwilioSmsService.NormalizePhoneVN(soDienThoai);

                if (string.IsNullOrWhiteSpace(hoTen))
                    return Json(new { success = false, message = "Họ tên không được để trống." });

                if (string.IsNullOrWhiteSpace(normalizedPhone))
                    return Json(new { success = false, message = "Số điện thoại không hợp lệ." });

                if (string.IsNullOrWhiteSpace(diaChi))
                    return Json(new { success = false, message = "Địa chỉ không được để trống." });

                user.HoTen = hoTen;
                user.SoDienThoai = normalizedPhone;
                user.DiaChi = diaChi;

                db.SaveChanges();

                Session["UserName"] = user.HoTen;
                Session["UserEmail"] = user.Email;

                return Json(new
                {
                    success = true,
                    message = "Đã cập nhật thông tin cá nhân thành công.",
                    data = new
                    {
                        HoTen = user.HoTen,
                        SoDienThoai = user.SoDienThoai,
                        DiaChi = user.DiaChi
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Không thể cập nhật thông tin: " + ex.Message });
            }
        }

        // GET: Account/MyOrders
        public ActionResult MyOrders()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Index", "Home");

            int userId = (int)Session["UserId"];
            var orders = db.DonHangs
                .Include(d => d.TrangThaiDonHang)
                .Include(d => d.ChiTietDonHangs.Select(ct => ct.SanPham))
                .Where(d => d.MaNguoiDung == userId)
                .OrderByDescending(d => d.NgayDat)
                .ToList();
            return View(orders);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        private static string GenerateOtpCode()
        {
            byte[] bytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            int value = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;
            return value.ToString("D6");
        }

        private SmsSendResult SendPendingRegistrationOtp(int userId, string normalizedPhone)
        {
            var lastSentAt = Session[SessionOtpLastSentAt] as DateTime?;
            if (lastSentAt.HasValue && DateTime.Now.Subtract(lastSentAt.Value).TotalSeconds < 60)
            {
                return new SmsSendResult
                {
                    Success = false,
                    Message = "Vui lòng chờ 60 giây trước khi gửi lại OTP."
                };
            }

            var smsResult = smsService.SendOtpSms(normalizedPhone);
            if (!smsResult.Success)
            {
                return smsResult;
            }

            Session[SessionPendingUserId] = userId;
            Session[SessionPendingUserPhone] = normalizedPhone;
            Session[SessionOtpPhone] = normalizedPhone;
            Session[SessionOtpExpireAt] = DateTime.Now.AddMinutes(5);
            Session[SessionOtpLastSentAt] = DateTime.Now;
            Session[SessionOtpTryCount] = 0;

            return new SmsSendResult { Success = true, Message = "OTP đã được gửi." };
        }

        private void ClearRegisterOtpSession()
        {
            Session.Remove(SessionPendingUserId);
            Session.Remove(SessionPendingUserPhone);
            Session.Remove(SessionOtpPhone);
            Session.Remove(SessionOtpExpireAt);
            Session.Remove(SessionOtpLastSentAt);
            Session.Remove(SessionOtpTryCount);
        }
    }
}
