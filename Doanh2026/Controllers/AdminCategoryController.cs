using System;
using System.Linq;
using System.Web.Mvc;
using Doanh2026.Models;
using System.Data.Entity;

namespace Doanh2026.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminCategoryController : Controller
    {
        private Doanh2026Entities db = new Doanh2026Entities();

        // GET: AdminCategory
        public ActionResult Index(string searchString)
        {
            var categories = db.DanhMucs.AsQueryable();

            if (!String.IsNullOrEmpty(searchString))
            {
                categories = categories.Where(s => s.TenDanhMuc.Contains(searchString));
            }

            ViewBag.SearchString = searchString;
            return View(categories.OrderByDescending(c => c.MaDanhMuc).ToList());
        }

        // POST: AdminCategory/Create
        [HttpPost]
        public JsonResult Create(DanhMuc danhMuc)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    db.DanhMucs.Add(danhMuc);
                    db.SaveChanges();
                    return Json(new { success = true, message = "Thêm danh mục thành công!" });
                }
                return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // GET: AdminCategory/GetCategory/5
        [HttpGet]
        public JsonResult GetCategory(int id)
        {
            var category = db.DanhMucs.Find(id);
            if (category == null)
            {
                return Json(new { success = false, message = "Không tìm thấy danh mục" }, JsonRequestBehavior.AllowGet);
            }
            return Json(new { success = true, data = new { MaDanhMuc = category.MaDanhMuc, TenDanhMuc = category.TenDanhMuc, MoTa = category.MoTa } }, JsonRequestBehavior.AllowGet);
        }

        // POST: AdminCategory/Edit
        [HttpPost]
        public JsonResult Edit(DanhMuc danhMuc)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var existingCategory = db.DanhMucs.Find(danhMuc.MaDanhMuc);
                    if (existingCategory != null)
                    {
                        existingCategory.TenDanhMuc = danhMuc.TenDanhMuc;
                        existingCategory.MoTa = danhMuc.MoTa;
                        db.Entry(existingCategory).State = EntityState.Modified;
                        db.SaveChanges();
                        return Json(new { success = true, message = "Cập nhật danh mục thành công!" });
                    }
                    return Json(new { success = false, message = "Không tìm thấy danh mục!" });
                }
                return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // POST: AdminCategory/Delete/5
        [HttpPost]
        public JsonResult Delete(int id)
        {
            try
            {
                DanhMuc danhMuc = db.DanhMucs.Find(id);
                if (danhMuc != null)
                {
                    // Check if category has products
                    if (danhMuc.SanPhams != null && danhMuc.SanPhams.Any())
                    {
                        return Json(new { success = false, message = "Không thể xóa danh mục đang có sản phẩm!" });
                    }
                    
                    db.DanhMucs.Remove(danhMuc);
                    db.SaveChanges();
                    return Json(new { success = true, message = "Xóa danh mục thành công!" });
                }
                return Json(new { success = false, message = "Không tìm thấy danh mục!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
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
