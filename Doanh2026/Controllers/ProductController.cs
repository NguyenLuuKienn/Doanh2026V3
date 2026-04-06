using System;
using System.Linq;
using System.Web.Mvc;
using Doanh2026.Models;

namespace Doanh2026.Controllers
{
    public class ProductController : Controller
    {
        private Doanh2026Entities db = new Doanh2026Entities();

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
                        TenSanPham = "PC AMD GAMING RYZEN 7 7700 - RTX 5060 8GB (Cấu hình gốc)",
                        GiaGoc = 26990000,
                        GiaUuDai = 26360000,
                        MoTa = "Mô tả ngắn gọn",
                        ThongSoKyThuat = "- CPU AMD Ryzen 7 7700 (3.8 GHz Upto 5.3GHz / 40MB / 8 Cores, 16 Threads, AM5) TRAY\n- Mainboard ASUS A620M-K PRIME DDR5\n- RAM APACER NOX 16GB BUSS 5200MHz DDR5\n- Ổ cứng SSD HIKSEMI WAVE PRO 512GB M.2 2280 PCIe 3.0x4 (Đọc 3500MB/s, Ghi 1800MB/s)\n- Nguồn FSP HY PRO 650W (80 Plus Bronze/DÂY LIỀN/EU/ĐEN)\n- Card màn hình ZOTAC GAMING GeForce RTX 5060 Twin Edge OC\n- Vỏ Case AIGO C218M BLACK - KÈM 4 FAN ARGB\n- Tản nhiệt khí IDCOOLING SE-214 XT BLACK"
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
                    TenSanPham = "PC AMD GAMING RYZEN 7 7700 - RTX 5060 8GB (Cấu hình gốc)",
                    GiaGoc = 26990000,
                    GiaUuDai = 26360000,
                    MoTa = "Mô tả ngắn gọn",
                    ThongSoKyThuat = "- CPU AMD Ryzen 7 7700 (3.8 GHz Upto 5.3GHz / 40MB / 8 Cores, 16 Threads, AM5) TRAY\n- Mainboard ASUS A620M-K PRIME DDR5\n- RAM APACER NOX 16GB BUSS 5200MHz DDR5\n- Ổ cứng SSD HIKSEMI WAVE PRO 512GB M.2 2280 PCIe 3.0x4 (Đọc 3500MB/s, Ghi 1800MB/s)\n- Nguồn FSP HY PRO 650W (80 Plus Bronze/DÂY LIỀN/EU/ĐEN)\n- Card màn hình ZOTAC GAMING GeForce RTX 5060 Twin Edge OC\n- Vỏ Case AIGO C218M BLACK - KÈM 4 FAN ARGB\n- Tản nhiệt khí IDCOOLING SE-214 XT BLACK"
                };
                mockSp.DanhMuc = new DanhMuc { TenDanhMuc = "PC AMD GAMING" };
                return View(mockSp);
            }
        }
    }
}
