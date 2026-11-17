using System;
using ObenseuerOS;
using UnityEngine.UIElements;

namespace Example.Hello
{
    internal static class HelloSite
    {
        internal static OsSitePage Build(VisualElement root, string path)
        {
            OsSitePage page = new OsSitePage();
            bool about = path.StartsWith("/about", StringComparison.OrdinalIgnoreCase);
            root.Add(new Label(about ? "About Hello OS" : "Hello from a mod site"));
            Button link = new Button(delegate { page.Navigate(about ? "/" : "/about"); });
            link.text = about ? "Home" : "About";
            root.Add(link);
            return page;
        }
    }
}
