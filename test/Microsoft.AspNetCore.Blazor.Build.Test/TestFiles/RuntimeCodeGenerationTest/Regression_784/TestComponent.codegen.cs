// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line hidden
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Blazor;
    using Microsoft.AspNetCore.Blazor.Components;
    public class TestComponent : Microsoft.AspNetCore.Blazor.Components.BlazorComponent
    {
        #pragma warning disable 1998
        protected override void BuildRenderTree(Microsoft.AspNetCore.Blazor.RenderTree.RenderTreeBuilder builder)
        {
            base.BuildRenderTree(builder);
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "onmouseover", Microsoft.AspNetCore.Blazor.Components.BindMethods.GetEventHandlerValue<Microsoft.AspNetCore.Blazor.UIMouseEventArgs>(OnComponentHover));
            builder.AddAttribute(2, "style", "background:" + " " + (ParentBgColor) + ";");
            builder.CloseElement();
        }
        #pragma warning restore 1998
#line 2 "x:\dir\subdir\Test\TestComponent.cshtml"
            
    public string ParentBgColor { get; set; } = "#FFFFFF";

    public void OnComponentHover(UIMouseEventArgs e)
    {
    }

#line default
#line hidden
    }
}
#pragma warning restore 1591
