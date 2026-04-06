using System;
using System.Linq;
using System.Web.Mvc;
using Doanh2026.Models;
using Doanh2026.Filters;
using System.Data.Entity;

namespace Doanh2026.Controllers
{
    [AdminAuthFilter]
    public class AdminCustomerController : Controller
    {
        private Doanh2026Entities db = new Doanh2026Entities();

        public ActionResult Index(string searchString, string status)
        {
            // Tìm MaVaiTro động (Tránh lỗi hardcode 2 nếu DB thay đổi)
            var customerRole = db.VaiTroes.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("khách hàng") || r.TenVaiTro.ToLower().Contains("customer"));
            int roleId = customerRole?.MaVaiTro ?? 2;

            var customers = db.NguoiDungs.Where(n => n.MaVaiTro == roleId).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
                customers = customers.Where(n => n.HoTen.Contains(searchString) || n.Email.Contains(searchString) || n.SoDienThoai.Contains(searchString));

            if (!string.IsNullOrEmpty(status))
                customers = customers.Where(n => n.TrangThaiTaiKhoan == status);

            ViewBag.SearchString = searchString;
            ViewBag.Status = status;
            ViewBag.TotalCustomers = db.NguoiDungs.Count(n => n.MaVaiTro == roleId);
            ViewBag.ActiveCustomers = db.NguoiDungs.Count(n => n.MaVaiTro == roleId && n.TrangThaiTaiKhoan == "HoatDong");
            ViewBag.LockedCustomers = db.NguoiDungs.Count(n => n.MaVaiTro == roleId && n.TrangThaiTaiKhoan == "KhoaTaiKhoan");

            return View(customers.OrderByDescending(n => n.NgayDangKy).ToList());
        }

        // GET: AdminCustomer/GetCustomer/5
        [HttpGet]
        public JsonResult GetCustomer(int id)
        {
            var customerRole = db.VaiTroes.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("khách hàng") || r.TenVaiTro.ToLower().Contains("customer"));
            int roleId = customerRole?.MaVaiTro ?? 2;

            var customer = db.NguoiDungs.Find(id);
            if (customer == null || customer.MaVaiTro != roleId)
                return Json(new { success = false, message = "Không tìm thấy khách hàng!" }, JsonRequestBehavior.AllowGet);

            return Json(new { success = true, data = new {
                MaNguoiDung = customer.MaNguoiDung,
                HoTen = customer.HoTen,
                Email = customer.Email,
                SoDienThoai = customer.SoDienThoai,
                DiaChi = customer.DiaChi,
                TrangThaiTaiKhoan = customer.TrangThaiTaiKhoan,
                NgayDangKy = customer.NgayDangKy?.ToString("dd/MM/yyyy")
            }}, JsonRequestBehavior.AllowGet);
        }

        // POST: AdminCustomer/Create
        [HttpPost]
        public JsonResult Create(NguoiDung nguoiDung)
        {
            try
            {
                var existing = db.NguoiDungs.FirstOrDefault(n => n.Email == nguoiDung.Email);
                if (existing != null)
                    return Json(new { success = false, message = "Email đã tồn tại trong hệ thống!" });

                var customerRole = db.VaiTroes.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("khách hàng") || r.TenVaiTro.ToLower().Contains("customer"));
                int roleId = customerRole?.MaVaiTro ?? 2;

                nguoiDung.MaVaiTro = roleId;
                nguoiDung.TrangThaiTaiKhoan = "HoatDong";
                nguoiDung.NgayDangKy = DateTime.Now;

                if (string.IsNullOrEmpty(nguoiDung.MatKhau))
                    nguoiDung.MatKhau = "123456"; // Mật khẩu mặc định

                db.NguoiDungs.Add(nguoiDung);
                db.SaveChanges();
                return Json(new { success = true, message = "Thêm khách hàng thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // POST: AdminCustomer/Edit
        [HttpPost]
        public JsonResult Edit(NguoiDung nguoiDung)
        {
            try
            {
                var existing = db.NguoiDungs.Find(nguoiDung.MaNguoiDung);
                if (existing == null) return Json(new { success = false, message = "Không tìm thấy!" });

                existing.HoTen = nguoiDung.HoTen;
                existing.SoDienThoai = nguoiDung.SoDienThoai;
                existing.DiaChi = nguoiDung.DiaChi;
                existing.TrangThaiTaiKhoan = nguoiDung.TrangThaiTaiKhoan;
                if (!string.IsNullOrEmpty(nguoiDung.MatKhau))
                    existing.MatKhau = nguoiDung.MatKhau;

                db.Entry(existing).State = EntityState.Modified;
                db.SaveChanges();
                return Json(new { success = true, message = "Cập nhật thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // POST: AdminCustomer/ToggleStatus/5
        [HttpPost]
        public JsonResult ToggleStatus(int id)
        {
            try
            {
                var customer = db.NguoiDungs.Find(id);
                if (customer == null) return Json(new { success = false, message = "Không tìm thấy!" });

                customer.TrangThaiTaiKhoan = customer.TrangThaiTaiKhoan == "HoatDong" ? "KhoaTaiKhoan" : "HoatDong";
                db.SaveChanges();
                return Json(new { success = true, message = customer.TrangThaiTaiKhoan == "HoatDong" ? "Đã mở khóa tài khoản!" : "Đã khóa tài khoản!", newStatus = customer.TrangThaiTaiKhoan });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: AdminCustomer/Delete/5
        [HttpPost]
        public JsonResult Delete(int id)
        {
            try
            {
                var customerRole = db.VaiTroes.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("khách hàng") || r.TenVaiTro.ToLower().Contains("customer"));
                int roleId = customerRole?.MaVaiTro ?? 2;

                var customer = db.NguoiDungs.Find(id);
                if (customer == null || customer.MaVaiTro != roleId)
                    return Json(new { success = false, message = "Không tìm thấy khách hàng!" });

                // Kiểm tra có đơn hàng không
                if (db.DonHangs.Any(d => d.MaNguoiDung == id))
                    return Json(new { success = false, message = "Không thể xóa: Khách hàng này đang có đơn hàng trong hệ thống!" });

                db.NguoiDungs.Remove(customer);
                db.SaveChanges();
                return Json(new { success = true, message = "Đã xóa khách hàng thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
