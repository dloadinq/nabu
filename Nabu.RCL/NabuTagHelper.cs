using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Nabu.RCL;

[HtmlTargetElement("nabu")]
public class NabuTagHelper(IHtmlHelper htmlHelper) : TagHelper
{
    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; } = null!;

    public bool ShowLanguageSelect { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ((IViewContextAware)htmlHelper).Contextualize(ViewContext);

        var content = await htmlHelper.RenderComponentAsync<NabuView>(
            RenderMode.Static,
            new { ShowLanguageSelect });

        output.TagName = null;
        output.Content.SetHtmlContent(content);
    }
}
