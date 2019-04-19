using System;
using System.Collections.Generic;

namespace ReVersionVCS_API_Lambdas.Models
{
    public partial class RepositoryPermission
    {
        public int Id { get; set; }
        public int PermittedUser { get; set; }
        public int RepositoryId { get; set; }

        public virtual User PermittedUserNavigation { get; set; }
        public virtual Repository Repository { get; set; }
    }
}
