using System;
using System.Collections.Generic;

namespace ReVersionVCS_API_Lambdas.Models
{
    public partial class Branch
    {
        public Branch()
        {
            EventLogs = new HashSet<EventLog>();
            VersionsBranch = new HashSet<Version>();
            VersionsParentBranchNavigation = new HashSet<Version>();
        }

        public int Id { get; set; }
        public int RepositoryId { get; set; }
        public string Name { get; set; }
        public bool Locked { get; set; }
        public int VersionNumber { get; set; }
        public string LatestFileHierarchy { get; set; }

        public virtual Repository Repository { get; set; }
        public virtual ICollection<EventLog> EventLogs { get; set; }
        public virtual ICollection<Version> VersionsBranch { get; set; }
        public virtual ICollection<Version> VersionsParentBranchNavigation { get; set; }
    }
}
