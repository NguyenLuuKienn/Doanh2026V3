using System;
using System.Linq;
using System.Web.Mvc;
using Doanh2026.Models;
using System.Data.Entity;

namespace Doanh2026.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminProductController : Controller
    {
        private Doanh2026Entities db = new Doanh2026Entities();

        // GET: AdminProduct
        public ActionResult Index(string searchString)
        {
            var products = db.SanPhams.Include(p => p.DanhMuc).AsQueryable();

            if (!String.IsNullOrEmpty(searchString))
            {
                products = products.Where(s => s.TenSanPham.Contains(searchString));
            }

            ViewBag.SearchString = searchString;
            
            // Lấy danh sách danh mục để đổ vào Modal Select
            ViewBag.MaDanhMuc = new SelectList(db.DanhMucs, "MaDanhMuc", "TenDanhMuc");
            
            return View(products.OrderByDescending(p => p.MaSanPham).ToList());
        }

        // POST: AdminProduct/Create
        [HttpPost, ValidateInput(false)]
        public JsonResult Create(SanPham sanPham)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    sanPham.NgayTao = DateTime.Now;
                    db.SanPhams.Add(sanPham);
                    db.SaveChanges(); // Lưu để có MaSanPham
                    
                    // --- XỬ LÝ UPLOAD HÌNH ẢNH DYNAMIC ---
                    if (Request.Files.Count > 0)
                    {
                        string uploadPath = Server.MapPath("~/Content/Images/Products/");
                        if (!System.IO.Directory.Exists(uploadPath))
                        {
                            System.IO.Directory.CreateDirectory(uploadPath);
                        }

                        for (int i = 0; i < Request.Files.Count; i++)
                        {
                            var file = Request.Files[i];
                            if (file != null && file.ContentLength > 0)
                            {
                                string fileName = DateTime.Now.ToString("yyyyMMddHHmmssfff") + "_" + System.IO.Path.GetFileName(file.FileName);
                                string filePath = System.IO.Path.Combine(uploadPath, fileName);
                                file.SaveAs(filePath);

                                db.HinhAnhSanPhams.Add(new HinhAnhSanPham
                                {
                                    MaSanPham = sanPham.MaSanPham,
                                    DuongDanAnh = "/Content/Images/Products/" + fileName,
                                    LaAnhDaiDien = (i == 0) // Ảnh đầu tiên làm đại diện
                                });
                            }
                        }
                        db.SaveChanges(); // Lưu ảnh
                    }

                    return Json(new { success = true, message = "Thêm sản phẩm thành công!" });
                }
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, message = "Dữ liệu không hợp lệ: " + string.Join(", ", errors) });
            }
            catch (Exception ex)
            {
                // Inspect inner exception for FK conflicts or constraints
                var msg = ex.Message;
                if(ex.InnerException != null) msg += " - " + ex.InnerException.Message;
                return Json(new { success = false, message = "Lỗi khi thêm: " + msg });
            }
        }

        // GET: AdminProduct/GetProduct/5
        [HttpGet]
        public JsonResult GetProduct(int id)
        {
            var p = db.SanPhams.Find(id);
            if (p == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm" }, JsonRequestBehavior.AllowGet);
            }
            return Json(new { 
                success = true, 
                data = new { 
                    MaSanPham = p.MaSanPham, 
                    TenSanPham = p.TenSanPham, 
                    MaDanhMuc = p.MaDanhMuc,
                    GiaGoc = p.GiaGoc,
                    GiaUuDai = p.GiaUuDai,
                    SoLuongTon = p.SoLuongTon,
                    MoTa = p.MoTa,
                    ThongSoKyThuat = p.ThongSoKyThuat // JS Object Array JSON String
                } 
            }, JsonRequestBehavior.AllowGet);
        }

        // POST: AdminProduct/Edit
        [HttpPost, ValidateInput(false)]
        public JsonResult Edit(SanPham sanPham)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var existing = db.SanPhams.Find(sanPham.MaSanPham);
                    if (existing != null)
                    {
                        existing.TenSanPham = sanPham.TenSanPham;
                        existing.MaDanhMuc = sanPham.MaDanhMuc;
                        existing.GiaGoc = sanPham.GiaGoc;
                        existing.GiaUuDai = sanPham.GiaUuDai;
                        existing.SoLuongTon = sanPham.SoLuongTon;
                        existing.MoTa = sanPham.MoTa;
                        existing.ThongSoKyThuat = sanPham.ThongSoKyThuat;
                        
                        // --- XỬ LÝ UPLOAD HÌNH ẢNH SỬA ---
                        if (Request.Files.Count > 0)
                        {
                            string uploadPath = Server.MapPath("~/Content/Images/Products/");
                            if (!System.IO.Directory.Exists(uploadPath))
                            {
                                System.IO.Directory.CreateDirectory(uploadPath);
                            }
                            
                            // Xóa ảnh cũ (Tùy chọn ghi đè toàn bộ ảnh cũ)
                            var oldImages = db.HinhAnhSanPhams.Where(h => h.MaSanPham == sanPham.MaSanPham).ToList();
                            foreach(var img in oldImages) {
                                string oldFilePath = Server.MapPath(img.DuongDanAnh);
                                if (System.IO.File.Exists(oldFilePath)) System.IO.File.Delete(oldFilePath);
                            }
                            db.HinhAnhSanPhams.RemoveRange(oldImages);

                            for (int i = 0; i < Request.Files.Count; i++)
                            {
                                var file = Request.Files[i];
                                if (file != null && file.ContentLength > 0)
                                {
                                    string fileName = DateTime.Now.ToString("yyyyMMddHHmmssfff") + "_" + System.IO.Path.GetFileName(file.FileName);
                                    string filePath = System.IO.Path.Combine(uploadPath, fileName);
                                    file.SaveAs(filePath);

                                    db.HinhAnhSanPhams.Add(new HinhAnhSanPham
                                    {
                                        MaSanPham = sanPham.MaSanPham,
                                        DuongDanAnh = "/Content/Images/Products/" + fileName,
                                        LaAnhDaiDien = (i == 0)
                                    });
                                }
                            }
                        }
                        
                        db.Entry(existing).State = EntityState.Modified;
                        db.SaveChanges();
                        return Json(new { success = true, message = "Cập nhật sản phẩm thành công!" });
                    }
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm!" });
                }
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, message = "Dữ liệu không hợp lệ: " + string.Join(", ", errors) });
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if(ex.InnerException != null) msg += " - " + ex.InnerException.Message;
                return Json(new { success = false, message = "Lỗi cập nhật: " + msg });
            }
        }

        // POST: AdminProduct/Delete/5
        [HttpPost]
        public JsonResult Delete(int id)
        {
            try
            {
                SanPham sanPham = db.SanPhams.Find(id);
                if (sanPham != null) {
                    // Xóa file ảnh vật lý trên máy chủ
                    var images = db.HinhAnhSanPhams.Where(h => h.MaSanPham == id).ToList();
                    foreach (var img in images)
                    {
                        string filePath = Server.MapPath(img.DuongDanAnh);
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }
                    // Xóa ảnh liên quan trước DB
                    db.HinhAnhSanPhams.RemoveRange(images);
                    
                    db.SanPhams.Remove(sanPham);
                    db.SaveChanges();
                    return Json(new { success = true, message = "Xóa sản phẩm thành công!" });
                }
                return Json(new { success = false, message = "Không tìm thấy sản phẩm để xóa!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xóa: " + ex.Message });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
