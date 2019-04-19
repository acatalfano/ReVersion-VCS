using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ReVersionVCS_API_Lambdas
{
    public class VersionData : IEquatable<VersionData>
    {
        public string RepositoryName { get; set; }
        public string BranchName { get; set; }
        public string ParentBranchName { get; set; }
        public bool NewBranch { get; set; } = false;
        public string DeltaContent { get; set; }
        public List<HierarchyNode> FileHierarchy { get; set; }
        public int EventId { get; set; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null)) return false;
            if (ReferenceEquals(obj, this)) return true;
            if (obj.GetType() != GetType()) return false;
            var versionData = obj as VersionData;
            if (ReferenceEquals(versionData, null)) return false;
            return versionData != null && Equals(versionData);
        }

        public bool Equals(VersionData versionData) =>
            RepositoryName.Equals(versionData.RepositoryName)
                && BranchName.Equals(versionData.BranchName)
                && ParentBranchName.Equals(versionData.BranchName)
                && NewBranch.Equals(versionData.NewBranch)
                && DeltaContent.Equals(versionData.DeltaContent)
                && FileHierarchy.All(versionData.FileHierarchy.Contains)
                && FileHierarchy.Count.Equals(versionData.FileHierarchy.Count)
                && EventId.Equals(versionData.EventId);

        public string FileHierarchyString() => JsonConvert.SerializeObject(FileHierarchy);

        public override int GetHashCode()
        {
            return HashCode.Combine(RepositoryName, BranchName, ParentBranchName, NewBranch, DeltaContent, FileHierarchy, EventId);
        }
        // TODO: make the FileHierarchyData into an actual object
        //      and turn into a string via json.net serializer (jsonConvert.SerializeObject(this.FileHierarchyData))
    }
}
