using System;
using System.Web.Mvc;

namespace Doanh2026.Filters
{
    /// <summary>
    /// Custom Authorization Filter dựa trên Session cho các trang Admin.
    /// Dùng thay cho [Authorize] mặc định vì project dùng Session-based Auth.
    /// </summary>
    public class AdminAuthFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var session = filterContext.HttpContext.Session;
            
            // Kiểm tra Session đăng nhập
            if (session["UserId"] == null)
            {
                // Chưa đăng nhập -> về trang chủ  
                filterContext.Result = new RedirectResult("/Home/Index?requireLogin=true");
                return;
            }

            // Kiểm tra quyền Admin (MaVaiTro == 1)
            var role = session["UserRole"];
            if (role == null || (int)role != 1)
            {
                filterContext.Result = new RedirectResult("/Home/Index?accessDenied=true");
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }

    /// <summary>
    /// Custom Authorization Filter cho các Action yêu cầu đăng nhập (Customer + Admin).
    /// </summary>
    public class UserAuthFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var session = filterContext.HttpContext.Session;
            
            if (session["UserId"] == null)
            {
                filterContext.Result = new RedirectResult("/Home/Index?requireLogin=true");
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
