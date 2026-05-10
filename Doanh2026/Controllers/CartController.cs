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

        public ActionResult Index()
        {
            if (Session["UserId"] == null)
            {
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
                gioHang = new GioHang { MaNguoiDung = userId };
                db.GioHangs.Add(gioHang);
                db.SaveChanges();
            }

            return View(gioHang);
        }

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

                var tonKhoSanPham = sanPham.SoLuongTon ?? 0;
                if (tonKhoSanPham < quantity)
                    return Json(new { success = false, message = "Số lượng tồn kho không đủ!" });

                var gioHang = db.GioHangs.FirstOrDefault(g => g.MaNguoiDung == userId);
                if (gioHang == null)
                {
                    gioHang = new GioHang { MaNguoiDung = userId };
                    db.GioHangs.Add(gioHang);
                    db.SaveChanges();
                }

                var existingItem = db.ChiTietGioHangs
                    .FirstOrDefault(c => c.MaGioHang == gioHang.MaGioHang && c.MaSanPham == productId);

                if (existingItem != null)
                {
                    var nextQty = existingItem.SoLuong + quantity;
                    var tonKhoHienTai = sanPham.SoLuongTon ?? 0;
                    if (nextQty > tonKhoHienTai)
                        return Json(new { success = false, message = "Số lượng yêu cầu vượt quá tồn kho." });

                    existingItem.SoLuong = nextQty;
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

                int totalItems = db.ChiTietGioHangs
                    .Where(c => c.MaGioHang == gioHang.MaGioHang)
                    .Select(c => (int?)c.SoLuong)
                    .Sum() ?? 0;

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

        [HttpPost]
        public JsonResult UpdateQuantity(int chiTietId, int quantity)
        {
            if (Session["UserId"] == null)
                return Json(new { success = false, requireLogin = true, message = "Phiên đăng nhập đã hết hạn." });

            try
            {
                int userId = (int)Session["UserId"];
                var item = db.ChiTietGioHangs
                    .Include(c => c.GioHang)
                    .Include(c => c.SanPham)
                    .FirstOrDefault(c => c.MaChiTietGio == chiTietId && c.GioHang.MaNguoiDung == userId);

                if (item == null)
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm trong giỏ." });

                if (quantity <= 0)
                {
                    db.ChiTietGioHangs.Remove(item);
                }
                else
                {
                    var tonKho = item.SanPham != null ? (item.SanPham.SoLuongTon ?? 0) : 0;
                    if (quantity > tonKho)
                        return Json(new { success = false, message = "Số lượng yêu cầu vượt quá tồn kho." });

                    item.SoLuong = quantity;
                    db.Entry(item).State = EntityState.Modified;
                }

                db.SaveChanges();

                var gioHang = db.GioHangs.FirstOrDefault(g => g.MaNguoiDung == userId);
                if (gioHang == null)
                    return Json(new { success = true, total = "0", cartCount = 0 });

                var cartItems = db.ChiTietGioHangs
                    .Include(c => c.SanPham)
                    .Where(c => c.MaGioHang == gioHang.MaGioHang)
                    .ToList();

                decimal total = cartItems.Sum(c => (c.SanPham.GiaUuDai ?? c.SanPham.GiaGoc) * c.SoLuong);
                int cartCount = cartItems.Sum(c => c.SoLuong);

                return Json(new { success = true, total = total.ToString("N0"), cartCount = cartCount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Không thể cập nhật giỏ hàng: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult RemoveItem(int chiTietId)
        {
            if (Session["UserId"] == null)
                return Json(new { success = false, requireLogin = true, message = "Phiên đăng nhập đã hết hạn." });

            try
            {
                int userId = (int)Session["UserId"];
                var gioHang = db.GioHangs.FirstOrDefault(g => g.MaNguoiDung == userId);
                if (gioHang == null)
                    return Json(new { success = true, message = "Giỏ hàng trống.", cartCount = 0 });

                var item = db.ChiTietGioHangs
                    .FirstOrDefault(c => c.MaChiTietGio == chiTietId && c.MaGioHang == gioHang.MaGioHang);

                if (item == null)
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm để xóa." });

                db.ChiTietGioHangs.Remove(item);
                db.SaveChanges();

                int cartCount = db.ChiTietGioHangs
                    .Where(c => c.MaGioHang == gioHang.MaGioHang)
                    .Select(c => (int?)c.SoLuong)
                    .Sum() ?? 0;

                return Json(new { success = true, message = "Đã xóa sản phẩm khỏi giỏ hàng!", cartCount = cartCount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Không thể xóa sản phẩm: " + ex.Message });
            }
        }

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
