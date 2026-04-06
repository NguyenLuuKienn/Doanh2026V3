using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Security.Principal;
using System.Web.Security;

namespace Doanh2026
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Tự động seed dữ liệu nếu DB trống
            Models.DbSeeder.Seed();
        }

        protected void Application_AcquireRequestState(Object sender, EventArgs e)
        {
            if (HttpContext.Current.Session != null && Request.IsAuthenticated)
            {
                // Lấy Role từ Session (đã được lưu khi Login)
                var userRole = Session["UserRole"]?.ToString();
                string[] roles = new string[] { };

                if (userRole == "1") roles = new string[] { "Admin" };
                else if (userRole == "2") roles = new string[] { "Customer" };
                else if (userRole == "3") roles = new string[] { "Staff" };

                // Tạo Principal mới với Roles
                IPrincipal principal = new GenericPrincipal(Context.User.Identity, roles);
                Context.User = principal;
            }
        }
    }
}
