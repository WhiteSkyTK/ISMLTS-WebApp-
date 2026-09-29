using Microsoft.AspNetCore.Mvc;

namespace ISMLTS_WebApp_.Extensions
{
    public static class ToastTypes
    {
        public const string Success = "success";
        public const string Danger = "danger";
        public const string Info = "info";
    }

    public static class ControllerExtensions
    {
        // The layout shows this once as a Bootstrap toast after the redirect
        public static void Toast(this Controller controller, string message, string type = ToastTypes.Success)
        {
            controller.TempData["Toast"] = message;
            controller.TempData["ToastType"] = type is ToastTypes.Danger or ToastTypes.Info ? type : ToastTypes.Success;
        }
    }
}
