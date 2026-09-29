using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ISMLTS_WebApp_.TagHelpers
{
    // <a asp-controller="Students" highlight-active> gets class="active" and aria-current="page"
    // while any page of that controller is showing.
    [HtmlTargetElement("a", Attributes = HighlightAttribute)]
    public class ActiveLinkTagHelper : TagHelper
    {
        private const string HighlightAttribute = "highlight-active";

        [HtmlAttributeName("asp-controller")]
        public string? Controller { get; set; }

        [ViewContext]
        [HtmlAttributeNotBound]
        public ViewContext ViewContext { get; set; } = null!;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.Attributes.RemoveAll(HighlightAttribute);

            var current = ViewContext.RouteData.Values["controller"]?.ToString();
            if (!string.Equals(current, Controller, StringComparison.OrdinalIgnoreCase)) return;

            var classes = output.Attributes.TryGetAttribute("class", out var existing) ? $"{existing.Value} active" : "active";
            output.Attributes.SetAttribute("class", classes);
            output.Attributes.SetAttribute("aria-current", "page");
        }
    }
}
