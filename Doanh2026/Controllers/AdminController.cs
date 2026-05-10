using System;
using System.Linq;
using System.Web.Mvc;
using System.Collections.Generic;
using Doanh2026.Models;
using System.Data.Entity;
using System.Configuration;

namespace Doanh2026.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private Doanh2026Entities db = new Doanh2026Entities();

        // GET: Admin/Index - Dashboard tổng quan với stats thật
        public ActionResult Index(string period = "month")
        {
            try
            {
                var now = DateTime.Now;
                DateTime startDate;
                DateTime prevStart, prevEnd;

                // Xác định khoảng thời gian theo period
                switch (period.ToLower())
                {
                    case "day":
                        startDate = now.Date;
                        prevStart = now.Date.AddDays(-1);
                        prevEnd = now.Date;
                        break;
                    case "week":
                        int diff = (int)now.DayOfWeek - (int)DayOfWeek.Monday;
                        if (diff < 0) diff += 7;
                        startDate = now.Date.AddDays(-diff);
                        prevStart = startDate.AddDays(-7);
                        prevEnd = startDate;
                        break;
                    default: // month
                        startDate = new DateTime(now.Year, now.Month, 1);
                        prevStart = startDate.AddMonths(-1);
                        prevEnd = startDate;
                        break;
                }

                // ==== THỐNG KÊ KỲ HIỆN TẠI ====
                var ordersThisPeriod = db.DonHangs.Where(d => d.NgayDat >= startDate && d.NgayDat <= now);
                var ordersPrevPeriod = db.DonHangs.Where(d => d.NgayDat >= prevStart && d.NgayDat < prevEnd);

                var roleIds = db.VaiTroes.ToList();
                int customerRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("khách hàng") || r.TenVaiTro.ToLower().Contains("customer"))?.MaVaiTro ?? 2;
                int adminRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("admin") || r.TenVaiTro.ToLower().Contains("quản trị"))?.MaVaiTro ?? 1;
                int staffRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("nhân viên") || r.TenVaiTro.ToLower().Contains("staff"))?.MaVaiTro ?? 3;

                int totalOrders = db.DonHangs.Count();
                decimal totalRevenue = db.DonHangs.Where(d => d.MaTrangThai == 4)
                                       .Select(d => d.TongTien).DefaultIfEmpty(0).Sum() ?? 0;
                int totalCustomers = db.NguoiDungs.Count(n => n.MaVaiTro == customerRoleId);
                int totalProducts = db.SanPhams.Count();
                int totalStaff = db.NguoiDungs.Count(n => n.MaVaiTro == adminRoleId || n.MaVaiTro == staffRoleId);

                // So sánh kỳ trước để tính %
                decimal revThis = ordersThisPeriod.Where(d => d.MaTrangThai == 4).Select(d => d.TongTien).DefaultIfEmpty(0).Sum() ?? 0;
                decimal revPrev = ordersPrevPeriod.Where(d => d.MaTrangThai == 4).Select(d => d.TongTien).DefaultIfEmpty(0).Sum() ?? 0;
                int ordThis = ordersThisPeriod.Count();
                int ordPrev = ordersPrevPeriod.Count();

                double revGrowth = revPrev != 0 ? Math.Round((double)(revThis - revPrev) / (double)revPrev * 100, 1) : 0;
                double ordGrowth = ordPrev != 0 ? Math.Round((double)(ordThis - ordPrev) / (double)ordPrev * 100, 1) : 0;

                ViewBag.TotalOrders = totalOrders;
                ViewBag.TotalRevenue = totalRevenue;
                ViewBag.TotalCustomers = totalCustomers;
                ViewBag.TotalProducts = totalProducts;
                ViewBag.TotalStaff = totalStaff;
                ViewBag.RevGrowth = revGrowth;
                ViewBag.OrdGrowth = ordGrowth;
                ViewBag.Period = period;

                // ==== DỮ LIỆU BIỂU ĐỒ (7 ngày gần nhất) ====
                var chartData = new List<object>();
                for (int i = 6; i >= 0; i--)
                {
                    var day = now.Date.AddDays(-i);
                    var dayRevenue = db.DonHangs
                        .Where(d => d.NgayDat.HasValue &&
                               DbFunctions.TruncateTime(d.NgayDat) == day &&
                               d.MaTrangThai == 4)
                        .Select(d => d.TongTien).DefaultIfEmpty(0).Sum() ?? 0;
                    chartData.Add(new { label = day.ToString("dd/MM"), value = (long)dayRevenue });
                }
                ViewBag.ChartData = Newtonsoft.Json.JsonConvert.SerializeObject(chartData);

                // ==== TOP 5 SẢN PHẨM BÁN CHẠY ====
                var topProducts = db.ChiTietDonHangs
                    .GroupBy(ct => ct.MaSanPham)
                    .Select(g => new {
                        MaSP = g.Key,
                        TotalQty = g.Sum(x => x.SoLuong),
                        TotalRevenue = g.Sum(x => (x.SoLuong) * (x.DonGia))
                    })
                    .OrderByDescending(x => x.TotalQty)
                    .Take(5)
                    .ToList()
                    .Select(x => new {
                        TenSanPham = db.SanPhams.Find(x.MaSP)?.TenSanPham ?? "Không rõ",
                        SoLuong = x.TotalQty,
                        DoanhThu = x.TotalRevenue
                    }).ToList();
                ViewBag.TopProducts = topProducts;
                ViewBag.StatusList = new SelectList(db.TrangThaiDonHangs.OrderBy(t => t.MaTrangThai).ToList(), "MaTrangThai", "TenTrangThai");

                // 10 Đơn hàng mới nhất
                var recentOrders = db.DonHangs
                    .Include(d => d.TrangThaiDonHang)
                    .Include(d => d.NguoiDung)
                    .Include(d => d.ChiTietDonHangs.Select(ct => ct.SanPham))
                    .OrderByDescending(d => d.NgayDat)
                    .Take(10)
                    .ToList();

                return View(recentOrders);
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                ViewBag.TotalOrders = 0;
                ViewBag.TotalRevenue = 0m;
                ViewBag.TotalCustomers = 0;
                ViewBag.TotalProducts = 0;
                ViewBag.TotalStaff = 0;
                ViewBag.RevGrowth = 0.0;
                ViewBag.OrdGrowth = 0.0;
                ViewBag.Period = "month";
                ViewBag.ChartData = "[]";
                ViewBag.TopProducts = new List<object>();
                ViewBag.StatusList = new SelectList(new List<SelectListItem>());
                return View(new List<DonHang>());
            }
        }

        // GET: Admin/Reports - Báo cáo chi tiết theo kỳ
        public ActionResult Reports(string period = "month", DateTime? customStart = null, DateTime? customEnd = null)
        {
            var now = DateTime.Now;
            DateTime startDate;
            DateTime endDate = now;
            string periodLabel;

            switch (period.ToLower())
            {
                case "day":
                    startDate = now.Date;
                    periodLabel = "Hôm nay (" + now.ToString("dd/MM/yyyy") + ")";
                    break;
                case "week":
                    int diff = (int)now.DayOfWeek - (int)DayOfWeek.Monday;
                    if (diff < 0) diff += 7;
                    startDate = now.Date.AddDays(-diff);
                    periodLabel = "Tuần này (" + startDate.ToString("dd/MM") + " - " + now.ToString("dd/MM/yyyy") + ")";
                    break;
                case "custom":
                    startDate = customStart ?? now.Date;
                    endDate = customEnd ?? now;
                    periodLabel = "Tùy chọn: " + startDate.ToString("dd/MM/yyyy") + " - " + endDate.ToString("dd/MM/yyyy");
                    break;
                default: // month
                    startDate = new DateTime(now.Year, now.Month, 1);
                    periodLabel = "Tháng " + now.Month + "/" + now.Year;
                    break;
            }

            var ordersInPeriod = db.DonHangs
                .Include(d => d.TrangThaiDonHang)
                .Include(d => d.ChiTietDonHangs.Select(ct => ct.SanPham))
                .Where(d => d.NgayDat >= startDate && d.NgayDat <= endDate)
                .ToList();

            var roleIds = db.VaiTroes.ToList();
            int customerRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("khách hàng") || r.TenVaiTro.ToLower().Contains("customer"))?.MaVaiTro ?? 2;

            int totalOrders = ordersInPeriod.Count;
            decimal totalRevenue = ordersInPeriod.Where(d => d.MaTrangThai == 4).Sum(d => d.TongTien ?? 0);
            int totalCustomers = db.NguoiDungs.Count(n => n.MaVaiTro == customerRoleId && n.NgayDangKy >= startDate && n.NgayDangKy <= endDate);

            // Sản phẩm bán chạy trong kỳ
            var topProducts = ordersInPeriod
                .Where(d => d.ChiTietDonHangs != null)
                .SelectMany(d => d.ChiTietDonHangs)
                .GroupBy(ct => new { ct.MaSanPham, ct.SanPham?.TenSanPham })
                .Select(g => new {
                    TenSanPham = g.Key.TenSanPham ?? "Không rõ",
                    SoLuongBan = g.Sum(x => x.SoLuong),
                    DoanhThu = g.Sum(x => (x.SoLuong) * (x.DonGia))
                })
                .OrderByDescending(x => x.SoLuongBan)
                .Take(10)
                .ToList();

            // Doanh thu theo ngày trong kỳ
            var revenueByDay = ordersInPeriod
                .Where(d => d.MaTrangThai == 4 && d.NgayDat.HasValue)
                .GroupBy(d => d.NgayDat.Value.Date)
                .Select(g => new { Date = g.Key, Revenue = g.Sum(x => x.TongTien ?? 0) })
                .OrderBy(x => x.Date)
                .ToList();

            ViewBag.Period = period;
            ViewBag.PeriodLabel = periodLabel;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalCustomers = totalCustomers;
            ViewBag.TopProducts = topProducts;
            ViewBag.RevenueByDay = Newtonsoft.Json.JsonConvert.SerializeObject(
                revenueByDay.Select(r => new { date = r.Date.ToString("dd/MM"), revenue = (long)r.Revenue }));
            ViewBag.AllOrders = ordersInPeriod;

            return View();
        }

        // GET: Admin/Settings - Cai dat chung he thong
        [Authorize(Roles = "SuperAdmin")]
        public ActionResult Settings()
        {
            ViewBag.SupportPhone = ConfigurationManager.AppSettings["SupportPhone"] ?? "";
            ViewBag.SupportEmail = ConfigurationManager.AppSettings["SupportEmail"] ?? "";
            ViewBag.StoreAddress = ConfigurationManager.AppSettings["StoreAddress"] ?? "";
            ViewBag.SiteName = ConfigurationManager.AppSettings["SiteName"] ?? "QuocDoanhJr";
            return View();
        }

        // POST: Admin/Settings
        [Authorize(Roles = "SuperAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Settings(string siteName, string supportPhone, string supportEmail, string storeAddress)
        {
            try
            {
                var config = System.Web.Configuration.WebConfigurationManager.OpenWebConfiguration("~");

                SetOrAddAppSetting(config, "SiteName", siteName);
                SetOrAddAppSetting(config, "SupportPhone", supportPhone);
                SetOrAddAppSetting(config, "SupportEmail", supportEmail);
                SetOrAddAppSetting(config, "StoreAddress", storeAddress);

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");

                TempData["Success"] = "Đã lưu cài đặt chung thành công.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Không thể lưu cài đặt: " + ex.Message;
            }

            return RedirectToAction("Settings");
        }

        private static void SetOrAddAppSetting(System.Configuration.Configuration config, string key, string value)
        {
            var setting = config.AppSettings.Settings[key];
            if (setting == null)
            {
                config.AppSettings.Settings.Add(key, value ?? string.Empty);
                return;
            }

            setting.Value = value ?? string.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // POST: Admin/UpdateOrderStatus
        [HttpPost]
        public JsonResult UpdateOrderStatus(int id, int maTrangThai)
        {
            try
            {
                var order = db.DonHangs.FirstOrDefault(d => d.MaDonHang == id);
                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                var statusExists = db.TrangThaiDonHangs.Any(t => t.MaTrangThai == maTrangThai);
                if (!statusExists)
                {
                    return Json(new { success = false, message = "Trạng thái không hợp lệ." });
                }

                order.MaTrangThai = maTrangThai;
                db.SaveChanges();

                var statusName = db.TrangThaiDonHangs.Where(t => t.MaTrangThai == maTrangThai).Select(t => t.TenTrangThai).FirstOrDefault();

                return Json(new { success = true, message = "Đã cập nhật trạng thái đơn hàng.", statusName = statusName });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Không thể cập nhật đơn hàng: " + ex.Message });
            }
        }

    }
}
