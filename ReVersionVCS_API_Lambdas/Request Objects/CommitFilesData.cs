using System.Collections.Generic;
using Newtonsoft.Json;

namespace ReVersionVCS_API_Lambdas.Request_Objects
{
    public class CommitFilesData : LogData
    {
        // These data are base64 encoded
        [JsonProperty(Required = Required.Always)]
        public List<string> Filename { get; set; }
    }
}
