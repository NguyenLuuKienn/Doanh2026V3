using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Security;
using Doanh2026.Models;

namespace Doanh2026.Controllers
{
    public class AccountController : Controller
    {
        private Doanh2026Entities db = new Doanh2026Entities();

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
                var user = db.NguoiDungs.FirstOrDefault(u => u.Email.ToLower() == emailLower && u.MatKhau == password);

                if (user == null)
                    return Json(new { success = false, message = "Email hoặc Mật khẩu không chính xác!" });

                if (user.TrangThaiTaiKhoan == "KhoaTaiKhoan")
                    return Json(new { success = false, message = "Tài khoản của bạn đã bị khóa. Liên hệ hỗ trợ!" });

                // Lưu vào Session
                Session["UserId"] = user.MaNguoiDung;
                Session["UserName"] = user.HoTen;
                Session["UserEmail"] = user.Email;
                Session["UserRole"] = user.MaVaiTro;

                // Tích hợp FormsAuthentication để [Authorize] hoạt động
                FormsAuthentication.SetAuthCookie(user.Email, false);

                return Json(new {
                    success = true,
                    message = "Đăng nhập thành công! Chào mừng " + user.HoTen,
                    userName = user.HoTen,
                    userRole = user.MaVaiTro
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

                if (password != confirmPassword)
                    return Json(new { success = false, message = "Mật khẩu xác nhận không khớp!" });

                if (password.Length < 6)
                    return Json(new { success = false, message = "Mật khẩu phải có ít nhất 6 ký tự!" });

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
                    SoDienThoai = soDienThoai,
                    DiaChi = diaChi,
                    MaVaiTro = roleId,
                    TrangThaiTaiKhoan = "HoatDong",
                    NgayDangKy = DateTime.Now
                };

                db.NguoiDungs.Add(newUser);
                db.SaveChanges();

                // Tự động đăng nhập sau khi đăng ký
                Session["UserId"] = newUser.MaNguoiDung;
                Session["UserName"] = newUser.HoTen;
                Session["UserEmail"] = newUser.Email;
                Session["UserRole"] = newUser.MaVaiTro;

                // Tích hợp FormsAuthentication
                FormsAuthentication.SetAuthCookie(newUser.Email, false);

                return Json(new {
                    success = true,
                    message = "Đăng ký thành công! Chào mừng " + newUser.HoTen,
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

        // GET: Account/MyOrders
        public ActionResult MyOrders()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Index", "Home");

            int userId = (int)Session["UserId"];
            var orders = db.DonHangs.Where(d => d.MaNguoiDung == userId)
                           .OrderByDescending(d => d.NgayDat).ToList();
            return View(orders);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
