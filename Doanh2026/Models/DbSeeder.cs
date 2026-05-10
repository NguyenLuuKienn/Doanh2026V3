using System;
using System.Collections.Generic;
using System.Linq;
using Doanh2026.Models;

namespace Doanh2026.Models
{
    public static class DbSeeder
    {
        public static void Seed()
        {
            using (var db = new Doanh2026Entities())
            {
                // 1. Seed VaiTro (Roles)
                if (!db.VaiTroes.Any())
                {
                    db.VaiTroes.AddRange(new List<VaiTro>
                    {
                        new VaiTro { TenVaiTro = "SuperAdmin" },
                        new VaiTro { TenVaiTro = "Admin" },
                        new VaiTro { TenVaiTro = "Khách hàng" },
                        new VaiTro { TenVaiTro = "Nhân viên" }
                    });
                    db.SaveChanges();
                }
                else if (!db.VaiTroes.Any(r => r.TenVaiTro == "SuperAdmin"))
                {
                    db.VaiTroes.Add(new VaiTro { TenVaiTro = "SuperAdmin" });
                    db.SaveChanges();
                }

                // 2. Seed TrangThaiDonHang (Order Statuses)
                if (!db.TrangThaiDonHangs.Any())
                {
                    db.TrangThaiDonHangs.AddRange(new List<TrangThaiDonHang>
                    {
                        new TrangThaiDonHang { MaTrangThai = 1, TenTrangThai = "Chờ xác nhận" },
                        new TrangThaiDonHang { MaTrangThai = 2, TenTrangThai = "Đang xử lý" },
                        new TrangThaiDonHang { MaTrangThai = 3, TenTrangThai = "Đang giao" },
                        new TrangThaiDonHang { MaTrangThai = 4, TenTrangThai = "Đã giao" },
                        new TrangThaiDonHang { MaTrangThai = 5, TenTrangThai = "Đã hủy" }
                    });
                    db.SaveChanges();
                }

                // 3. Seed PhuongThucThanhToan (Payment Methods)
                if (!db.PhuongThucThanhToans.Any())
                {
                    db.PhuongThucThanhToans.AddRange(new List<PhuongThucThanhToan>
                    {
                        new PhuongThucThanhToan { MaPTTT = 1, TenPTTT = "Thanh toán khi nhận hàng (COD)" },
                        new PhuongThucThanhToan { MaPTTT = 2, TenPTTT = "Thanh toán qua VNPAY" }
                    });
                    db.SaveChanges();
                }

                // 4. Tạo tài khoản Admin mặc định nếu chưa có
                if (!db.NguoiDungs.Any(u => u.Email == "admin@gmail.com"))
                {
                    var adminRole = db.VaiTroes.FirstOrDefault(r => r.TenVaiTro == "Admin");
                    if (adminRole != null)
                    {
                        db.NguoiDungs.Add(new NguoiDung
                        {
                            HoTen = "Administrator",
                            Email = "admin@gmail.com",
                            MatKhau = "123456", // Nên đổi sau
                            SoDienThoai = "0123456789",
                            DiaChi = "Hệ thống",
                            MaVaiTro = adminRole.MaVaiTro,
                            TrangThaiTaiKhoan = "HoatDong",
                            NgayDangKy = DateTime.Now
                        });
                        db.SaveChanges();
                    }
                }

                // 5. Tạo tài khoản SuperAdmin mặc định nếu chưa có
                if (!db.NguoiDungs.Any(u => u.Email == "superadmin@gmail.com"))
                {
                    var superAdminRole = db.VaiTroes.FirstOrDefault(r => r.TenVaiTro == "SuperAdmin");
                    if (superAdminRole != null)
                    {
                        db.NguoiDungs.Add(new NguoiDung
                        {
                            HoTen = "Super Administrator",
                            Email = "superadmin@gmail.com",
                            MatKhau = "Super@123",
                            SoDienThoai = "0999999999",
                            DiaChi = "Hệ thống",
                            MaVaiTro = superAdminRole.MaVaiTro,
                            TrangThaiTaiKhoan = "HoatDong",
                            NgayDangKy = DateTime.Now
                        });
                        db.SaveChanges();
                    }
                }
            }
        }
    }
}
