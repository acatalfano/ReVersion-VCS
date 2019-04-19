using System;
using System.Collections.Generic;
using System.Text;

namespace ReVersionVCS_API_Lambdas
{
    public class PermissionLookup : IEquatable<PermissionLookup>
    {
        public string Message { get; set; }
        public string RequestingUser { get; set; }
        public string RepositoryName { get; set; }
        public int RequestId { get; set; }
        public DateTime LogTimestamp { get; set; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null)) return false;
            if (ReferenceEquals(obj, this)) return true;
            if (obj.GetType() != GetType()) return false;
            var permissionLookup = obj as PermissionLookup;
            if (ReferenceEquals(permissionLookup, null)) return false;
            return permissionLookup != null && Equals(permissionLookup);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Message, RequestingUser, RepositoryName, RequestId);
        }

        public bool Equals(PermissionLookup other) =>
            Message.Equals(other.Message)
                && RequestingUser.Equals(other.RequestingUser)
                && RepositoryName.Equals(other.RepositoryName)
                && RequestId.Equals(other.RequestId);
    }
}
