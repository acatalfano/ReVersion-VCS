using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using ReVersionVCS_API_Lambdas.Request_Objects;

namespace ReVersionVCS_API_Lambdas
{
    public class LockData : LogData
    {
        [JsonProperty(Required = Required.Always)]
        public DateTime Timestamp { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string LockedBranchId { get; set; }
    }
}
