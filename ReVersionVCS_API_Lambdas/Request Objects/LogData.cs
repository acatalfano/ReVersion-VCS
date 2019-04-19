using Newtonsoft.Json;

namespace ReVersionVCS_API_Lambdas.Request_Objects
{
    public class LogData
    {
        [JsonProperty(Required = Required.Always)]
        public string UserName { get; set; }
        [JsonProperty(Required = Required.Always)]
        public string Message { get; set; }
    }
}
