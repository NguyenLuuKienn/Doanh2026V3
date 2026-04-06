using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using System.Data.Entity;
using Doanh2026.Models;
using Doanh2026.Filters;

namespace Doanh2026.Controllers
{
    public class PaymentController : Controller
    {
        private Doanh2026Entities db = new Doanh2026Entities();

        // --- VNPAY Cấu hình giả lập ---
        private const string VnpayTmnCode = "DEMOSHOP";
        private const string VnpayHashSecret = "DOANH2026SECRETKEY123456789ABCDE";
        private const string VnpayUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        private const string VnpayReturnUrl = "/Payment/VNPayReturn";

        // GET: Payment/Checkout - Trang thanh toán từ giỏ hàng
        [UserAuthFilter]
        public ActionResult Checkout()
        {
            int userId = (int)Session["UserId"];
            var gioHang = db.GioHangs
                .Include(g => g.ChiTietGioHangs.Select(c => c.SanPham))
                .FirstOrDefault(g => g.MaNguoiDung == userId);

            if (gioHang == null || !gioHang.ChiTietGioHangs.Any())
                return RedirectToAction("Index", "Cart");

            bool proxyEnabled = db.Configuration.ProxyCreationEnabled;
            db.Configuration.ProxyCreationEnabled = false; // Tắt tạm thời để lấy data sạch cho View
            var user = db.NguoiDungs.Find(userId);
            db.Configuration.ProxyCreationEnabled = proxyEnabled; // Bật lại ngay

            ViewBag.GioHang = gioHang;
            ViewBag.User = user;

            decimal tongTien = 0;
            foreach (var ct in gioHang.ChiTietGioHangs)
                tongTien += (ct.SanPham?.GiaUuDai ?? ct.SanPham?.GiaGoc ?? 0) * (ct.SoLuong);
            ViewBag.TongTien = tongTien;

            return View();
        }

        // POST: Payment/PlaceOrder - Đặt hàng COD hoặc chuyển sang VNPAY
        [HttpPost, UserAuthFilter]
        public ActionResult PlaceOrder(string tenNguoiNhan, string dienThoaiNhan, string diaChiGiao, string ghiChu, string phuongThucTT)
        {
            int userId = (int)Session["UserId"];
            var gioHang = db.GioHangs
                .Include(g => g.ChiTietGioHangs.Select(c => c.SanPham))
                .FirstOrDefault(g => g.MaNguoiDung == userId);

            if (gioHang == null || !gioHang.ChiTietGioHangs.Any())
                return RedirectToAction("Index", "Cart");

            decimal tongTien = gioHang.ChiTietGioHangs.Sum(ct => (ct.SanPham?.GiaUuDai ?? ct.SanPham?.GiaGoc ?? 0) * (ct.SoLuong));

            try
            {
                // Tạo đơn hàng mới
                var donHang = new DonHang
                {
                    MaNguoiDung = userId,
                    TenNguoiNhan = tenNguoiNhan ?? "",
                    SDTNhan = dienThoaiNhan ?? "",
                    DiaChiNhan = diaChiGiao ?? "",
                    GhiChu = ghiChu,
                    TongTien = tongTien,
                    NgayDat = DateTime.Now,
                    MaTrangThai = 1, // Chờ xác nhận
                };
                db.DonHangs.Add(donHang);
                db.SaveChanges(); // Lấy MaDonHang

                // Thêm chi tiết đơn hàng
                foreach (var ct in gioHang.ChiTietGioHangs)
                {
                    db.ChiTietDonHangs.Add(new ChiTietDonHang
                    {
                        MaDonHang = donHang.MaDonHang,
                        MaSanPham = ct.MaSanPham,
                        SoLuong = ct.SoLuong,
                        DonGia = ct.SanPham?.GiaUuDai ?? ct.SanPham?.GiaGoc ?? 0
                    });
                }

                // Xóa giỏ hàng sau khi đặt hàng
                db.ChiTietGioHangs.RemoveRange(gioHang.ChiTietGioHangs);
                db.SaveChanges();

                if (phuongThucTT == "VNPAY")
                {
                    return RedirectToAction("VNPaySimulate", new { orderId = donHang.MaDonHang, amount = tongTien });
                }

                TempData["OrderSuccess"] = donHang.MaDonHang;
                return RedirectToAction("OrderSuccess", new { id = donHang.MaDonHang });
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                        .SelectMany(x => x.ValidationErrors)
                        .Select(x => x.ErrorMessage);
                var fullErrorMessage = string.Join("; ", errorMessages);
                var exceptionMessage = string.Concat(ex.Message, " The validation errors are: ", fullErrorMessage);
                
                // Trả về view với thông báo lỗi rõ ràng thay vì crash
                ModelState.AddModelError("", "Lỗi dữ liệu: " + fullErrorMessage);
                
                // Re-bind dữ liệu cho View Checkout nếu lỗi
                db.Configuration.ProxyCreationEnabled = false;
                ViewBag.User = db.NguoiDungs.Find(userId);
                db.Configuration.ProxyCreationEnabled = true;
                ViewBag.GioHang = gioHang;
                ViewBag.TongTien = tongTien;
                
                return View("Checkout");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                
                db.Configuration.ProxyCreationEnabled = false;
                ViewBag.User = db.NguoiDungs.Find(userId);
                db.Configuration.ProxyCreationEnabled = true;
                ViewBag.GioHang = gioHang;
                ViewBag.TongTien = tongTien;

                return View("Checkout");
            }
        }

        // GET: Payment/VNPaySimulate - Trang giả lập VNPAY
        public ActionResult VNPaySimulate(int orderId, decimal amount)
        {
            ViewBag.OrderId = orderId;
            ViewBag.Amount = amount;
            ViewBag.TransactionId = "VNP" + DateTime.Now.ToString("yyyyMMddHHmmss") + orderId;
            return View();
        }

        // GET: Payment/VNPayConfirm - Xác nhận thanh toán VNPAY (giả lập)
        [HttpPost]
        public JsonResult VNPayConfirm(int orderId, bool success)
        {
            var donHang = db.DonHangs.Find(orderId);
            if (donHang != null)
            {
                donHang.MaTrangThai = success ? 2 : 6; // 2: Đã thanh toán, 6: Thanh toán thất bại
                db.SaveChanges();
            }
            return Json(new { success = true, orderId = orderId, paid = success });
        }

        // GET: Payment/OrderSuccess/5
        public ActionResult OrderSuccess(int id)
        {
            var donHang = db.DonHangs.Include(d => d.ChiTietDonHangs.Select(ct => ct.SanPham))
                                     .Include(d => d.TrangThaiDonHang)
                                     .FirstOrDefault(d => d.MaDonHang == id);
            if (donHang == null) return RedirectToAction("Index", "Home");
            return View(donHang);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
