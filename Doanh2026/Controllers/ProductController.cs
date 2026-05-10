using System;
using System.Linq;
using System.Web.Mvc;
using Doanh2026.Models;
using System.Data.Entity;

namespace Doanh2026.Controllers
{
    public class ProductController : Controller
    {
        private Doanh2026Entities db = new Doanh2026Entities();

        public ActionResult Category(int id)
        {
            var category = db.DanhMucs.FirstOrDefault(c => c.MaDanhMuc == id);
            if (category == null)
            {
                return HttpNotFound();
            }

            var products = db.SanPhams
                .Include(p => p.HinhAnhSanPhams)
                .Include(p => p.DanhMuc)
                .Where(p => p.MaDanhMuc == id)
                .OrderByDescending(p => p.MaSanPham)
                .ToList();

            ViewBag.Category = category;
            return View(products);
        }

        public ActionResult Details(int? id)
        {
            try
            {
                SanPham sanPham = null;
                if (id == null)
                {
                    sanPham = db.SanPhams.FirstOrDefault();
                }
                else
                {
                    sanPham = db.SanPhams.Find(id);
                }

                if (sanPham == null)
                {
                    // Fallback mock up for UI testing if DB is empty
                    sanPham = new SanPham {
                        MaSanPham = 1,
                        TenSanPham = "PC AMD GAMING RYZEN 7 7700 - RTX 5060 8GB (C?u h�nh g?c)",
                        GiaGoc = 26990000,
                        GiaUuDai = 26360000,
                        MoTa = "M� t? ng?n g?n",
                        ThongSoKyThuat = "- CPU AMD Ryzen 7 7700 (3.8 GHz Upto 5.3GHz / 40MB / 8 Cores, 16 Threads, AM5) TRAY\n- Mainboard ASUS A620M-K PRIME DDR5\n- RAM APACER NOX 16GB BUSS 5200MHz DDR5\n- ? c?ng SSD HIKSEMI WAVE PRO 512GB M.2 2280 PCIe 3.0x4 (�?c 3500MB/s, Ghi 1800MB/s)\n- Ngu?n FSP HY PRO 650W (80 Plus Bronze/D�Y LI?N/EU/�EN)\n- Card m�n h�nh ZOTAC GAMING GeForce RTX 5060 Twin Edge OC\n- V? Case AIGO C218M BLACK - K�M 4 FAN ARGB\n- T?n nhi?t kh� IDCOOLING SE-214 XT BLACK"
                    };
                    sanPham.DanhMuc = new DanhMuc { TenDanhMuc = "PC AMD GAMING" };
                }

                return View(sanPham);
            }
            catch (Exception)
            {
                // Fallback mock up
                var mockSp = new SanPham {
                    MaSanPham = 1,
                    TenSanPham = "PC AMD GAMING RYZEN 7 7700 - RTX 5060 8GB (C?u h�nh g?c)",
                    GiaGoc = 26990000,
                    GiaUuDai = 26360000,
                    MoTa = "M� t? ng?n g?n",
                    ThongSoKyThuat = "- CPU AMD Ryzen 7 7700 (3.8 GHz Upto 5.3GHz / 40MB / 8 Cores, 16 Threads, AM5) TRAY\n- Mainboard ASUS A620M-K PRIME DDR5\n- RAM APACER NOX 16GB BUSS 5200MHz DDR5\n- ? c?ng SSD HIKSEMI WAVE PRO 512GB M.2 2280 PCIe 3.0x4 (�?c 3500MB/s, Ghi 1800MB/s)\n- Ngu?n FSP HY PRO 650W (80 Plus Bronze/D�Y LI?N/EU/�EN)\n- Card m�n h�nh ZOTAC GAMING GeForce RTX 5060 Twin Edge OC\n- V? Case AIGO C218M BLACK - K�M 4 FAN ARGB\n- T?n nhi?t kh� IDCOOLING SE-214 XT BLACK"
                };
                mockSp.DanhMuc = new DanhMuc { TenDanhMuc = "PC AMD GAMING" };
                return View(mockSp);
            }
        }
    }
}
