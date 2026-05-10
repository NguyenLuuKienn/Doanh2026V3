using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using Doanh2026.Models;
using Doanh2026.Filters;
using Doanh2026.Services;
using System.Collections.Generic;
using System.Configuration;

namespace Doanh2026.Controllers
{
    public class PaymentController : Controller
    {
        private Doanh2026Entities db = new Doanh2026Entities();

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
            db.Configuration.ProxyCreationEnabled = false;
            var user = db.NguoiDungs.Find(userId);
            db.Configuration.ProxyCreationEnabled = proxyEnabled;

            ViewBag.GioHang = gioHang;
            ViewBag.User = user;

            decimal tongTien = 0;
            foreach (var ct in gioHang.ChiTietGioHangs)
                tongTien += (ct.SanPham?.GiaUuDai ?? ct.SanPham?.GiaGoc ?? 0) * ct.SoLuong;
            ViewBag.TongTien = tongTien;

            return View();
        }

        [HttpPost, UserAuthFilter]
        public ActionResult PlaceOrder(string tenNguoiNhan, string dienThoaiNhan, string diaChiGiao, string ghiChu, string phuongThucTT)
        {
            int userId = (int)Session["UserId"];
            var gioHang = db.GioHangs
                .Include(g => g.ChiTietGioHangs.Select(c => c.SanPham))
                .FirstOrDefault(g => g.MaNguoiDung == userId);

            if (gioHang == null || !gioHang.ChiTietGioHangs.Any())
                return RedirectToAction("Index", "Cart");

            decimal tongTien = gioHang.ChiTietGioHangs.Sum(ct => (ct.SanPham?.GiaUuDai ?? ct.SanPham?.GiaGoc ?? 0) * ct.SoLuong);

            try
            {
                using (var tx = db.Database.BeginTransaction())
                {
                    foreach (var ct in gioHang.ChiTietGioHangs)
                    {
                        var sp = db.SanPhams.FirstOrDefault(p => p.MaSanPham == ct.MaSanPham);
                        if (sp == null)
                        {
                            tx.Rollback();
                            ModelState.AddModelError("", "Sản phẩm không tồn tại.");
                            ViewBag.User = db.NguoiDungs.Find(userId);
                            ViewBag.GioHang = gioHang;
                            ViewBag.TongTien = tongTien;
                            return View("Checkout");
                        }

                        var tonKhoHienTai = sp.SoLuongTon ?? 0;
                        if (tonKhoHienTai < ct.SoLuong)
                        {
                            tx.Rollback();
                            ModelState.AddModelError("", "Sản phẩm \"" + sp.TenSanPham + "\" không đủ tồn kho.");
                            ViewBag.User = db.NguoiDungs.Find(userId);
                            ViewBag.GioHang = gioHang;
                            ViewBag.TongTien = tongTien;
                            return View("Checkout");
                        }

                        sp.SoLuongTon = tonKhoHienTai - ct.SoLuong;
                    }

                    var donHang = new DonHang
                    {
                        MaNguoiDung = userId,
                        TenNguoiNhan = tenNguoiNhan ?? "",
                        SDTNhan = dienThoaiNhan ?? "",
                        DiaChiNhan = diaChiGiao ?? "",
                        GhiChu = ghiChu,
                        TongTien = tongTien,
                        NgayDat = DateTime.Now,
                        MaTrangThai = 1,
                    };
                    db.DonHangs.Add(donHang);
                    db.SaveChanges();

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

                    db.ChiTietGioHangs.RemoveRange(gioHang.ChiTietGioHangs);
                    db.SaveChanges();
                    tx.Commit();

                    if (phuongThucTT == "VNPAY")
                    {
                        var ip = Request.UserHostAddress ?? Request.ServerVariables["REMOTE_ADDR"] ?? "127.0.0.1";
                        var vnp = new VnPayService();

                        // Prepare extra params (expire, billing, invoice, order type)
                        var extras = new Dictionary<string, string>();
                        extras["vnp_CreateDate"] = DateTime.Now.ToString("yyyyMMddHHmmss");
                        extras["vnp_ExpireDate"] = DateTime.Now.AddMinutes(15).ToString("yyyyMMddHHmmss");
                        extras["vnp_OrderType"] = "other";

                        var user = db.NguoiDungs.Find(userId);
                        // Billing
                        extras["vnp_Bill_Mobile"] = dienThoaiNhan ?? user?.SoDienThoai ?? "";
                        extras["vnp_Bill_Email"] = user?.Email ?? "";
                        if (!string.IsNullOrEmpty(user?.HoTen))
                        {
                            var name = user.HoTen.Trim();
                            var idx = name.IndexOf(' ');
                            if (idx > 0)
                            {
                                extras["vnp_Bill_FirstName"] = name.Substring(0, idx);
                                extras["vnp_Bill_LastName"] = name.Substring(idx + 1);
                            }
                            else
                            {
                                extras["vnp_Bill_FirstName"] = name;
                                extras["vnp_Bill_LastName"] = "";
                            }
                        }
                        extras["vnp_Bill_Address"] = diaChiGiao ?? user?.DiaChi ?? "";
                        extras["vnp_Bill_City"] = "";
                        extras["vnp_Bill_Country"] = "VN";

                        // OrderInfo: normalize
                        extras["vnp_OrderInfo"] = VnPayService.NormalizeOrderInfo("Thanh toan don hang " + donHang.MaDonHang);

                        var paymentUrl = vnp.CreatePaymentUrl(donHang.MaDonHang, tongTien, ip, null, extras);
                        return Redirect(paymentUrl);
                    }

                    TempData["OrderSuccess"] = donHang.MaDonHang;
                    return RedirectToAction("OrderSuccess", new { id = donHang.MaDonHang });
                }
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                        .SelectMany(x => x.ValidationErrors)
                        .Select(x => x.ErrorMessage);
                var fullErrorMessage = string.Join("; ", errorMessages);

                ModelState.AddModelError("", "Lỗi dữ liệu: " + fullErrorMessage);

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

        public ActionResult VNPaySimulate(int orderId, decimal amount)
        {
            ViewBag.OrderId = orderId;
            ViewBag.Amount = amount;
            ViewBag.TransactionId = "VNP" + DateTime.Now.ToString("yyyyMMddHHmmss") + orderId;
            return View();
        }

        // VNPAY return URL (customer redirect)
        public ActionResult VNPayReturn()
        {
            try
            {
                var vnp = new VnPayService();
                var parameters = new Dictionary<string, string>();
                foreach (string key in Request.QueryString.Keys)
                {
                    parameters[key] = Request.QueryString[key];
                }

                bool valid = vnp.VerifyVnpaySignature(parameters);
                int orderId = 0;
                if (parameters.ContainsKey("vnp_TxnRef")) int.TryParse(parameters["vnp_TxnRef"], out orderId);

                var donHang = db.DonHangs.Find(orderId);
                if (!valid)
                {
                    // Debug: log received vs computed secure hash
                    try
                    {
                        var computed = vnp.ComputeSecureHash(parameters);
                        var received = parameters.ContainsKey("vnp_SecureHash") ? parameters["vnp_SecureHash"] : "(none)";
                        var log = new System.Text.StringBuilder();
                        log.AppendLine("VNPAY signature mismatch");
                        log.AppendLine("Time: " + DateTime.Now.ToString("o"));
                        log.AppendLine("OrderRef: " + (parameters.ContainsKey("vnp_TxnRef") ? parameters["vnp_TxnRef"] : "(none)"));
                        log.AppendLine("Received: " + received);
                        log.AppendLine("Computed: " + computed);
                        log.AppendLine("All params:");
                        foreach (var kv in parameters.OrderBy(k => k.Key)) log.AppendLine(kv.Key + "=" + kv.Value);
                        var path = Server.MapPath("~/App_Data/vnpay_debug_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".log");
                        System.IO.File.WriteAllText(path, log.ToString());
                    }
                    catch { }

                    if (donHang != null)
                    {
                        donHang.MaTrangThai = 6; // thanh toan that bai
                        db.SaveChanges();
                    }
                    TempData["OrderError"] = "Không xác thực được dữ liệu thanh toán VNPAY. (Sai chữ ký)";
                    return RedirectToAction("OrderSuccess", new { id = orderId });
                }

                var responseCode = parameters.ContainsKey("vnp_ResponseCode") ? parameters["vnp_ResponseCode"] : "";
                if (responseCode == "00")
                {
                    if (donHang != null)
                    {
                        donHang.MaTrangThai = 2; // Thanh toán thành công
                        db.SaveChanges();

                        // Lưu bản ghi ThanhToan
                        var pttt = db.PhuongThucThanhToans.FirstOrDefault(p => p.TenPTTT.Contains("VNPAY"))?.MaPTTT;
                        db.ThanhToans.Add(new ThanhToan
                        {
                            MaDonHang = donHang.MaDonHang,
                            MaPTTT = pttt,
                            NgayThanhToan = DateTime.Now,
                            SoTienThanhToan = donHang.TongTien,
                            TrangThaiThanhToan = "ThanhCong"
                        });
                        db.SaveChanges();
                    }
                    TempData["OrderSuccess"] = orderId;
                }
                else
                {
                    if (donHang != null)
                    {
                        donHang.MaTrangThai = 6; // failed
                        db.SaveChanges();
                    }
                    TempData["OrderError"] = "Giao dịch bị hủy hoặc thất bại (Mã: " + responseCode + ")";
                }

                return RedirectToAction("OrderSuccess", new { id = orderId });
            }
            catch (Exception ex)
            {
                TempData["OrderError"] = "Lỗi xử lý trả về VNPAY: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        // VNPAY IPN (server-to-server notify)
        [HttpPost]
        public ActionResult VNPayIPN()
        {
            try
            {
                var vnp = new VnPayService();
                var parameters = new Dictionary<string, string>();
                foreach (string key in Request.Form.Keys)
                {
                    parameters[key] = Request.Form[key];
                }

                bool valid = vnp.VerifyVnpaySignature(parameters);
                int orderId = 0;
                if (parameters.ContainsKey("vnp_TxnRef")) int.TryParse(parameters["vnp_TxnRef"], out orderId);

                if (!valid)
                {
                    try
                    {
                        var computed = vnp.ComputeSecureHash(parameters);
                        var received = parameters.ContainsKey("vnp_SecureHash") ? parameters["vnp_SecureHash"] : "(none)";
                        var log = new System.Text.StringBuilder();
                        log.AppendLine("VNPAY IPN signature mismatch");
                        log.AppendLine("Time: " + DateTime.Now.ToString("o"));
                        log.AppendLine("OrderRef: " + (parameters.ContainsKey("vnp_TxnRef") ? parameters["vnp_TxnRef"] : "(none)"));
                        log.AppendLine("Received: " + received);
                        log.AppendLine("Computed: " + computed);
                        log.AppendLine("All params:");
                        foreach (var kv in parameters.OrderBy(k => k.Key)) log.AppendLine(kv.Key + "=" + kv.Value);
                        var path = Server.MapPath("~/App_Data/vnpay_ipn_debug_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".log");
                        System.IO.File.WriteAllText(path, log.ToString());
                    }
                    catch { }

                    return Content("{\"RspCode\":97,\"Message\":\"Checksum invalid\"}", "application/json");
                }

                var responseCode = parameters.ContainsKey("vnp_ResponseCode") ? parameters["vnp_ResponseCode"] : "";
                var donHang = db.DonHangs.Find(orderId);
                if (donHang != null && responseCode == "00")
                {
                    donHang.MaTrangThai = 2;
                    db.SaveChanges();
                    return Content("{\"RspCode\":00,\"Message\":\"Success\"}", "application/json");
                }

                return Content("{\"RspCode\":01,\"Message\":\"Fail\"}", "application/json");
            }
            catch (Exception)
            {
                return Content("{\"RspCode\":99,\"Message\":\"Exception\"}", "application/json");
            }
        }

        [HttpPost]
        public JsonResult VNPayConfirm(int orderId, bool success)
        {
            var donHang = db.DonHangs.Find(orderId);
            if (donHang != null)
            {
                donHang.MaTrangThai = success ? 2 : 6;
                db.SaveChanges();
            }
            return Json(new { success = true, orderId = orderId, paid = success });
        }

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
