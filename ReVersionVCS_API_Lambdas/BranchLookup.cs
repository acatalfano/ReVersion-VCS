using System;

namespace ReVersionVCS_API_Lambdas
{
    public class BranchLookup : IEquatable<BranchLookup>
    {
        public bool Locked { get; set; }
        public string BranchName { get; set; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null)) return false;
            if (ReferenceEquals(obj, this)) return true;
            if (obj.GetType() != GetType()) return false;
            var branchLookup = obj as BranchLookup;
            if (ReferenceEquals(branchLookup, null)) return false;
            return branchLookup != null && Equals(branchLookup);
        }

        public override int GetHashCode() => HashCode.Combine(BranchName);

        public bool Equals(BranchLookup branchLookup) =>
            Locked.Equals(branchLookup.Locked)
                && BranchName.Equals(branchLookup.BranchName);
    }
}
