using Newtonsoft.Json;

namespace ReVersionVCS_API_Lambdas.Request_Objects
{
    public class NewBranchData : LogData
    {
        [JsonProperty(Required = Required.Always)]
        public string BranchId { get; set; }
    }
}
