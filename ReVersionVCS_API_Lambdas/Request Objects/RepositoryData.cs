using Newtonsoft.Json;

namespace ReVersionVCS_API_Lambdas.Request_Objects
{
    public class RepositoryData : LogData
    {
        [JsonProperty(Required = Required.Always)]
        public string RepositoryId { get; set; }
    }
}
