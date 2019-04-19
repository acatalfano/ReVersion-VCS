using System;
using System.Collections.Generic;

namespace ReVersionVCS_API_Lambdas.Models
{
    public partial class Repository
    {
        public Repository()
        {
            Branches = new HashSet<Branch>();
            PermissionRequests = new HashSet<PermissionRequest>();
            RepositoryPermissions = new HashSet<RepositoryPermission>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public int Owner { get; set; }

        public virtual User OwnerNavigation { get; set; }
        public virtual ICollection<Branch> Branches { get; set; }
        public virtual ICollection<PermissionRequest> PermissionRequests { get; set; }
        public virtual ICollection<RepositoryPermission> RepositoryPermissions { get; set; }
    }
}
