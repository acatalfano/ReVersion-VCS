using System;
using System.Collections.Generic;
using System.Text;

namespace ReVersionVCS_API_Lambdas
{
    public class FileData
    {
        public string ObjectKey { get; set; }

        public string RepositoryName { get; set; }

        public string BranchName { get; set; }

        public string Content { get; set; }
    }
}
