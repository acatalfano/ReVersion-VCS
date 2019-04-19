using System;
using System.Collections.Generic;

namespace ReVersionVCS_API_Lambdas.Models
{
    public partial class EventLog
    {
        public EventLog()
        {
            PermissionRequests = new HashSet<PermissionRequest>();
            Versions = new HashSet<Version>();
        }

        public int Id { get; set; }
        public string Type { get; set; }
        public int BranchId { get; set; }
        public int UserId { get; set; }
        public string Message { get; set; }
        public DateTime LoggedAt { get; set; }

        public virtual Branch Branch { get; set; }
        public virtual User User { get; set; }
        public virtual ICollection<PermissionRequest> PermissionRequests { get; set; }
        public virtual ICollection<Version> Versions { get; set; }
    }
}
