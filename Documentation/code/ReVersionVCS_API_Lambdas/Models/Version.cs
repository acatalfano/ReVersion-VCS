using System;
using System.Collections.Generic;

namespace ReVersionVCS_API_Lambdas.Models
{
    public partial class Version
    {
        public int Id { get; set; }
        public int VersionNumber { get; set; }
        public int BranchId { get; set; }
        public int ParentBranch { get; set; }
        public string RollbackDelta { get; set; }
        public string FileHierarchy { get; set; }
        public int UpdateEventId { get; set; }

        public virtual Branch Branch { get; set; }
        public virtual Branch ParentBranchNavigation { get; set; }
        public virtual EventLog UpdateEvent { get; set; }
    }
}
