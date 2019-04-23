using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using ReVersionVCS_API_Lambdas;

namespace ReVersionVCS_API_Lambdas.Response_Objects
{
    public class ResourceTree : ResourceItem
    {
        [JsonProperty(Required = Required.Always)]
        public List<ResourceTree> Children { get; set; }

        [JsonProperty(Required = Required.Always)]
        public bool IsFile { get; set; }

        public ResourceTree(HierarchyNode data, string hrefRoot)
        {
            DisplayData = "root";
            Href = hrefRoot;
            IsFile = data.IsFile;
            Children = GetChildren(data, hrefRoot);
        }

        public ResourceTree() {}

        private List<ResourceTree> GetChildren(HierarchyNode data, string hrefPrefix)
        {
            var children = new List<ResourceTree>();
            if (data.Children.Count == 0)
                return children;

            hrefPrefix += $"/{data.Name}";

            foreach (var child in data.Children)
            {
                children.Add(new ResourceTree
                {
                    DisplayData = child.Name,
                    Href = hrefPrefix,
                    IsFile = child.IsFile,
                    Children = GetChildren(child, hrefPrefix)
                });
            }

            return children;
        }
    }
}
