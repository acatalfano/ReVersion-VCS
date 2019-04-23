using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace ReVersionVCS_API_Lambdas.Response_Objects
{
    public class ResourceItem
    {
        [JsonProperty(Required = Required.Always)]
        public string Href { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string DisplayData { get; set; }

        public string Owner { get; set; }
    }
}
