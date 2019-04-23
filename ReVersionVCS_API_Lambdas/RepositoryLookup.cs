using System;
using System.Collections.Generic;
using System.Text;

namespace ReVersionVCS_API_Lambdas
{
    public class RepositoryLookup : IEquatable<RepositoryLookup>
    {
        public string Name { get; set; }
        public string Owner { get; set; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null)) return false;
            if (ReferenceEquals(obj, this)) return true;
            if (obj.GetType() != GetType()) return false;
            var repositoryLookup = obj as RepositoryLookup;
            if (ReferenceEquals(repositoryLookup, null)) return false;
            return repositoryLookup != null && Equals(repositoryLookup);
        }

        public override int GetHashCode() => HashCode.Combine(Name, Owner);

        public bool Equals(RepositoryLookup repositoryLookup) =>
            Name.Equals(repositoryLookup.Name)
            && Owner.Equals(repositoryLookup.Owner);
    }
}
