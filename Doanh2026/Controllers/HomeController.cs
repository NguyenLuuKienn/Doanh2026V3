using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Doanh2026.Models;

namespace Doanh2026.Controllers
{
    public class HomeController : Controller
    {
        private Doanh2026Entities db = new Doanh2026Entities();

        public ActionResult Index()
        {
            try {
                // Lấy 5 sản phẩm mới nhất làm Hot Sale
                var hotSales = db.SanPhams.OrderByDescending(p => p.NgayTao).Take(8).ToList();
                
                // Lấy sản phẩm thuộc danh mục PC Gaming
                var pcGaming = db.SanPhams.Where(p => p.DanhMuc.TenDanhMuc.Contains("PC GAMING")).OrderByDescending(p => p.NgayTao).Take(8).ToList();
                
                // Demo fallback nếu chưa có dữ liệu chuẩn
                if (!pcGaming.Any()) {
                    pcGaming = hotSales;
                }

                // Lấy danh mục thật từ DB cho nav
                var danhMucs = db.DanhMucs.OrderBy(d => d.TenDanhMuc).ToList();
                ViewBag.DanhMucs = danhMucs;

                ViewBag.HotSales = hotSales;
                ViewBag.PCGaming = pcGaming;
            } catch (Exception) {
                // Fallback nếu chưa kết nối db hoặc ko có data
                ViewBag.HotSales = new List<SanPham>();
                ViewBag.PCGaming = new List<SanPham>();
                ViewBag.DanhMucs = new List<DanhMuc>();
            }

            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}