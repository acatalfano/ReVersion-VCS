using System;
using System.Collections.Generic;

namespace ReVersionVCS_API_Lambdas.Models
{
    public partial class User
    {
        public User()
        {
            EventLogs = new HashSet<EventLog>();
            Repositories = new HashSet<Repository>();
            RepositoryPermissions = new HashSet<RepositoryPermission>();
        }

        public int Id { get; set; }
        public string UserName { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual ICollection<EventLog> EventLogs { get; set; }
        public virtual ICollection<Repository> Repositories { get; set; }
        public virtual ICollection<RepositoryPermission> RepositoryPermissions { get; set; }
    }
}
