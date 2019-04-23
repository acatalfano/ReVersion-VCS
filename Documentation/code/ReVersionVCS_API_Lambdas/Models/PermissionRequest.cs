using System;
using System.Collections.Generic;

namespace ReVersionVCS_API_Lambdas.Models
{
    public partial class PermissionRequest
    {
        public int Id { get; set; }
        public int RepositoryId { get; set; }
        public int EventId { get; set; }

        public virtual EventLog Event { get; set; }
        public virtual Repository Repository { get; set; }
    }
}
