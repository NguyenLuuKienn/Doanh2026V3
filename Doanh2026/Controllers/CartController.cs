using System;
using System.Linq;
using System.Web.Mvc;
using Doanh2026.Models;
using Doanh2026.Filters;
using System.Data.Entity;

namespace Doanh2026.Controllers
{
    public class CartController : Controller
    {
        private Doanh2026Entities db = new Doanh2026Entities();

        // GET: Cart/Index - Xem giỏ hàng
        public ActionResult Index()
        {
            if (Session["UserId"] == null)
            {
                // Gợi mở modal login khi chưa đăng nhập
                TempData["RequireLogin"] = "true";
                return RedirectToAction("Index", "Home");
            }

            int userId = (int)Session["UserId"];
            var gioHang = db.GioHangs.Include(g => g.ChiTietGioHangs
                                                   .Select(c => c.SanPham)
                                                   .Select(s => s.HinhAnhSanPhams))
                                     .FirstOrDefault(g => g.MaNguoiDung == userId);

            if (gioHang == null)
            {
                // Tạo giỏ hàng mới nếu chưa có
                gioHang = new GioHang { MaNguoiDung = userId };
                db.GioHangs.Add(gioHang);
                db.SaveChanges();
            }

            return View(gioHang);
        }

        // POST: Cart/AddToCart - Thêm sản phẩm vào giỏ (AJAX)
        [HttpPost]
        public JsonResult AddToCart(int productId, int quantity = 1)
        {
            if (Session["UserId"] == null)
                return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập để thêm vào giỏ hàng!" });

            try
            {
                int userId = (int)Session["UserId"];
                var sanPham = db.SanPhams.Find(productId);

                if (sanPham == null)
                    return Json(new { success = false, message = "Sản phẩm không tồn tại!" });

                if (sanPham.SoLuongTon < quantity)
                    return Json(new { success = false, message = "Số lượng tồn kho không đủ!" });

                // Lấy hoặc tạo giỏ hàng
                var gioHang = db.GioHangs.FirstOrDefault(g => g.MaNguoiDung == userId);
                if (gioHang == null)
                {
                    gioHang = new GioHang { MaNguoiDung = userId };
                    db.GioHangs.Add(gioHang);
                    db.SaveChanges();
                }

                // Kiểm tra sản phẩm đã có trong giỏ chưa
                var existingItem = db.ChiTietGioHangs
                    .FirstOrDefault(c => c.MaGioHang == gioHang.MaGioHang && c.MaSanPham == productId);

                if (existingItem != null)
                {
                    existingItem.SoLuong = (existingItem.SoLuong) + quantity;
                    db.Entry(existingItem).State = EntityState.Modified;
                }
                else
                {
                    db.ChiTietGioHangs.Add(new ChiTietGioHang
                    {
                        MaGioHang = gioHang.MaGioHang,
                        MaSanPham = productId,
                        SoLuong = quantity
                    });
                }
                db.SaveChanges();

                // Đếm tổng số item trong giỏ
                int totalItems = db.ChiTietGioHangs
                    .Where(c => c.MaGioHang == gioHang.MaGioHang)
                    .Sum(c => c.SoLuong);

                return Json(new { 
                    success = true, 
                    message = $"Đã thêm \"{sanPham.TenSanPham}\" vào giỏ hàng!",
                    cartCount = totalItems
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi thêm vào giỏ: " + ex.Message });
            }
        }

        // POST: Cart/UpdateQuantity
        [HttpPost]
        public JsonResult UpdateQuantity(int chiTietId, int quantity)
        {
            if (Session["UserId"] == null)
                return Json(new { success = false, requireLogin = true });

            try
            {
                int userId = (int)Session["UserId"];
                var item = db.ChiTietGioHangs.Include(c => c.GioHang)
                             .FirstOrDefault(c => c.MaChiTietGio == chiTietId && c.GioHang.MaNguoiDung == userId);

                if (item == null)
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm trong giỏ!" });

                if (quantity <= 0)
                {
                    db.ChiTietGioHangs.Remove(item);
                }
                else
                {
                    item.SoLuong = quantity;
                    db.Entry(item).State = EntityState.Modified;
                }
                db.SaveChanges();

                // Tính lại tổng
                var gioHang = db.GioHangs.Include(g => g.ChiTietGioHangs.Select(c => c.SanPham))
                                         .FirstOrDefault(g => g.MaNguoiDung == userId);
                decimal total = 0;
                int cartCount = 0;
                if (gioHang != null)
                {
                    foreach(var c in gioHang.ChiTietGioHangs)
                    {
                        var gia = c.SanPham.GiaUuDai ?? c.SanPham.GiaGoc;
                        total += gia * (c.SoLuong);
                        cartCount += c.SoLuong;
                    }
                }

                return Json(new { success = true, total = total.ToString("N0"), cartCount = cartCount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Cart/RemoveItem
        [HttpPost]
        public JsonResult RemoveItem(int chiTietId)
        {
            if (Session["UserId"] == null)
                return Json(new { success = false, requireLogin = true });

            try
            {
                int userId = (int)Session["UserId"];
                var item = db.ChiTietGioHangs.Include(c => c.GioHang)
                             .FirstOrDefault(c => c.MaChiTietGio == chiTietId && c.GioHang.MaNguoiDung == userId);

                if (item != null)
                {
                    db.ChiTietGioHangs.Remove(item);
                    db.SaveChanges();
                }

                int cartCount = db.ChiTietGioHangs
                    .Where(c => c.GioHang.MaNguoiDung == userId)
                    .Sum(c => c.SoLuong);

                return Json(new { success = true, message = "Đã xóa sản phẩm khỏi giỏ hàng!", cartCount = cartCount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: Cart/GetCartCount - Lấy số lượng sản phẩm trong giỏ (AJAX)
        [HttpGet]
        public JsonResult GetCartCount()
        {
            if (Session["UserId"] == null)
                return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);

            int userId = (int)Session["UserId"];
            var gioHang = db.GioHangs.FirstOrDefault(g => g.MaNguoiDung == userId);
            int count = 0;
            if (gioHang != null)
                count = db.ChiTietGioHangs
                  .Where(c => c.MaGioHang == gioHang.MaGioHang)
                  .Sum(c => (int?)c.SoLuong) ?? 0;

            return Json(new { count = count }, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
