using System;
using System.Linq;
using System.Web.Mvc;
using Doanh2026.Models;
using Doanh2026.Filters;
using System.Data.Entity;

namespace Doanh2026.Controllers
{
    [AdminAuthFilter]
    public class AdminStaffController : Controller
    {
        private Doanh2026Entities db = new Doanh2026Entities();

        private bool IsSuperAdmin()
        {
            var roleName = Session["UserRoleName"]?.ToString();
            var roleId = Session["UserRole"]?.ToString();
            return string.Equals(roleName, "SuperAdmin", StringComparison.OrdinalIgnoreCase) || roleId == "4";
        }

        // GET: AdminStaff
        public ActionResult Index(string searchString)
        {
            var roleIds = db.VaiTroes.ToList();
            int superAdminRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("superadmin"))?.MaVaiTro ?? 4;
            int adminRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("admin") || r.TenVaiTro.ToLower().Contains("quản trị"))?.MaVaiTro ?? 1;
            int staffRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("nhân viên") || r.TenVaiTro.ToLower().Contains("staff"))?.MaVaiTro ?? 3;
            ViewBag.CanAssignAdmin = IsSuperAdmin();

            // Lọc nhân viên có vai trò Admin hoặc Staff
            var staff = db.NguoiDungs.Include(n => n.VaiTro).Where(n => n.MaVaiTro == adminRoleId || n.MaVaiTro == staffRoleId || n.MaVaiTro == superAdminRoleId).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
                staff = staff.Where(n => n.HoTen.Contains(searchString) || n.Email.Contains(searchString));

            ViewBag.SearchString = searchString;
            return View(staff.OrderBy(n => n.MaVaiTro).ThenBy(n => n.HoTen).ToList());
        }

        // GET: AdminStaff/GetStaff/5
        [HttpGet]
        public JsonResult GetStaff(int id)
        {
            var roleIds = db.VaiTroes.ToList();
            int superAdminRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("superadmin"))?.MaVaiTro ?? 4;
            int adminRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("admin") || r.TenVaiTro.ToLower().Contains("quản trị"))?.MaVaiTro ?? 1;
            int staffRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("nhân viên") || r.TenVaiTro.ToLower().Contains("staff"))?.MaVaiTro ?? 3;

            var staff = db.NguoiDungs.Include(n => n.VaiTro).FirstOrDefault(n => n.MaNguoiDung == id);
            if (staff == null || (staff.MaVaiTro != adminRoleId && staff.MaVaiTro != staffRoleId && staff.MaVaiTro != superAdminRoleId))
                return Json(new { success = false, message = "Không tìm thấy nhân viên!" }, JsonRequestBehavior.AllowGet);

            if ((staff.MaVaiTro == adminRoleId || staff.MaVaiTro == superAdminRoleId) && !IsSuperAdmin())
                return Json(new { success = false, message = "Chỉ SuperAdmin mới được chỉnh sửa tài khoản Admin!" }, JsonRequestBehavior.AllowGet);

            return Json(new { success = true, data = new {
                MaNguoiDung = staff.MaNguoiDung,
                HoTen = staff.HoTen,
                Email = staff.Email,
                SoDienThoai = staff.SoDienThoai,
                DiaChi = staff.DiaChi,
                MaVaiTro = staff.MaVaiTro,
                TrangThaiTaiKhoan = staff.TrangThaiTaiKhoan
            }}, JsonRequestBehavior.AllowGet);
        }

        // POST: AdminStaff/Create
        [HttpPost]
        public JsonResult Create(NguoiDung nguoiDung)
        {
            try
            {
                var roleIds = db.VaiTroes.ToList();
                int adminRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("admin") || r.TenVaiTro.ToLower().Contains("quản trị"))?.MaVaiTro ?? 1;
                int staffRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("nhân viên") || r.TenVaiTro.ToLower().Contains("staff"))?.MaVaiTro ?? 3;

                var existing = db.NguoiDungs.FirstOrDefault(n => n.Email == nguoiDung.Email);
                if (existing != null)
                    return Json(new { success = false, message = "Email đã tồn tại!" });

                if (!IsSuperAdmin() || (nguoiDung.MaVaiTro != adminRoleId && nguoiDung.MaVaiTro != staffRoleId))
                    nguoiDung.MaVaiTro = staffRoleId;

                nguoiDung.TrangThaiTaiKhoan = "HoatDong";
                nguoiDung.NgayDangKy = DateTime.Now;
                if (string.IsNullOrEmpty(nguoiDung.MatKhau))
                    nguoiDung.MatKhau = "Staff@123"; // Mật khẩu mặc định cho nhân viên mới

                db.NguoiDungs.Add(nguoiDung);
                db.SaveChanges();
                return Json(new { success = true, message = $"Thêm nhân viên thành công! Mật khẩu mặc định: {nguoiDung.MatKhau}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // POST: AdminStaff/Edit
        [HttpPost]
        public JsonResult Edit(NguoiDung nguoiDung)
        {
            try
            {
                var roleIds = db.VaiTroes.ToList();
                int superAdminRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("superadmin"))?.MaVaiTro ?? 4;
                int adminRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("admin") || r.TenVaiTro.ToLower().Contains("quản trị"))?.MaVaiTro ?? 1;
                int staffRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("nhân viên") || r.TenVaiTro.ToLower().Contains("staff"))?.MaVaiTro ?? 3;

                var existing = db.NguoiDungs.Find(nguoiDung.MaNguoiDung);
                if (existing == null) return Json(new { success = false, message = "Không tìm thấy!" });

                existing.HoTen = nguoiDung.HoTen;
                existing.SoDienThoai = nguoiDung.SoDienThoai;
                existing.DiaChi = nguoiDung.DiaChi;
                existing.TrangThaiTaiKhoan = nguoiDung.TrangThaiTaiKhoan;

                if (existing.MaVaiTro != superAdminRoleId)
                {
                    if (IsSuperAdmin() && (nguoiDung.MaVaiTro == adminRoleId || nguoiDung.MaVaiTro == staffRoleId))
                        existing.MaVaiTro = nguoiDung.MaVaiTro;
                    else if (!IsSuperAdmin())
                        existing.MaVaiTro = staffRoleId;
                }
                else
                {
                    existing.MaVaiTro = superAdminRoleId;
                }

                if (!string.IsNullOrEmpty(nguoiDung.MatKhau))
                    existing.MatKhau = nguoiDung.MatKhau;

                db.Entry(existing).State = EntityState.Modified;
                db.SaveChanges();
                return Json(new { success = true, message = "Cập nhật nhân viên thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // POST: AdminStaff/ToggleStatus/5
        [HttpPost]
        public JsonResult ToggleStatus(int id)
        {
            try
            {
                var roleIds = db.VaiTroes.ToList();
                int superAdminRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("superadmin"))?.MaVaiTro ?? 4;
                int adminRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("admin") || r.TenVaiTro.ToLower().Contains("quản trị"))?.MaVaiTro ?? 1;

                var staff = db.NguoiDungs.Find(id);
                if (staff == null) return Json(new { success = false });

                if (!IsSuperAdmin() && (staff.MaVaiTro == adminRoleId || staff.MaVaiTro == superAdminRoleId))
                    return Json(new { success = false, message = "Chỉ SuperAdmin mới được thay đổi trạng thái tài khoản Admin/SuperAdmin." });

                staff.TrangThaiTaiKhoan = staff.TrangThaiTaiKhoan == "HoatDong" ? "KhoaTaiKhoan" : "HoatDong";
                db.SaveChanges();
                return Json(new { success = true, newStatus = staff.TrangThaiTaiKhoan });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: AdminStaff/Delete/5
        [HttpPost]
        public JsonResult Delete(int id)
        {
            try
            {
                var roleIds = db.VaiTroes.ToList();
                int superAdminRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("superadmin"))?.MaVaiTro ?? 4;
                int adminRoleId = roleIds.FirstOrDefault(r => r.TenVaiTro.ToLower().Contains("admin") || r.TenVaiTro.ToLower().Contains("quản trị"))?.MaVaiTro ?? 1;

                // Không cho phép xóa chính mình
                if ((int?)Session["UserId"] == id)
                    return Json(new { success = false, message = "Không thể xóa tài khoản đang đăng nhập!" });

                var staff = db.NguoiDungs.Find(id);
                if (staff == null) return Json(new { success = false, message = "Không tìm thấy!" });

                if (!IsSuperAdmin() && (staff.MaVaiTro == adminRoleId || staff.MaVaiTro == superAdminRoleId))
                    return Json(new { success = false, message = "Chỉ SuperAdmin mới được xóa tài khoản Admin/SuperAdmin." });

                db.NguoiDungs.Remove(staff);
                db.SaveChanges();
                return Json(new { success = true, message = "Đã xóa nhân viên!" });
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
