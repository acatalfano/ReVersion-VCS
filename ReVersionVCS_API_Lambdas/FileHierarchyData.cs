using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ReVersionVCS_API_Lambdas
{
    public class HierarchyNode : IEquatable<HierarchyNode>
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsFile { get; set; }

        public override string ToString() => Path + Name + (!IsFile ? "/" : "");

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null)) return false;
            if (ReferenceEquals(obj, this)) return true;
            if (obj.GetType() != GetType()) return false;
            var fileNameAndPathPair = obj as HierarchyNode;
            if (ReferenceEquals(fileNameAndPathPair, null)) return false;
            return fileNameAndPathPair != null && Equals(fileNameAndPathPair);
        }

        public override int GetHashCode() => HashCode.Combine(Name, Path, IsFile);

        public bool Equals(HierarchyNode fileNameAndPathPair) =>
            Name.Equals(fileNameAndPathPair.Name)
                && Path.Equals(fileNameAndPathPair.Path)
                && IsFile.Equals(fileNameAndPathPair.IsFile);
    }


    public class FileHierarchyData
    {
        private readonly Node root;

        public FileHierarchyData(string hierarchyJson)
        {
            JObject hierarchy = JObject.Parse(hierarchyJson);
            string pathPrefix = string.Empty;
            root = hierarchy["Type"].ToObject<string>() == "Directory" ?
                    BuildHierarchy(pathPrefix, hierarchy) :
                    new Node(   "",
                                hierarchy["Name"].ToObject<string>(),
                                hierarchy["Type"].ToObject<string>().Equals("File"));
        }

        public FileHierarchyData(List<string> pathList) {

            for(int i = 0; i < pathList.Count; i++)
            {
                if (!string.IsNullOrEmpty(pathList[i]) && pathList[i][0].Equals('/'))
                    pathList[i] = pathList[i].Substring(1);
            }
            root = new Node("", "/", false);
            foreach (string path in pathList)
            {
                AddNode(path);
            }
        }

        public List<HierarchyNode> GetHierarchyList()
        {
            List<HierarchyNode> hierarchyList = new List<HierarchyNode>
            {
                new HierarchyNode { Name = "", Path = "", IsFile = root.IsFileNode }
            };

            hierarchyList.AddRange(GetChildPaths(root));

            return hierarchyList;            
        }
        
        public void AddNode(string path)
        {
            AddNodeRecursive(path, "/", root);
        }

        public void DeleteNode(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (path[0].Equals('/')) path = path.Substring(1);


            bool pathIsFile;

            if (path[path.Length - 1].Equals('/'))
            {
                pathIsFile = false;
                path = path.Substring(0, path.Length - 1);
            }
            else
            {
                pathIsFile = false;
            }

            int index = path.LastIndexOf('/');
            
            string targetNodeName;
            if (index == -1)
            {
                targetNodeName = path;
                path = string.Empty;
            }
            else
            {
                targetNodeName = path.Substring(index + 1);
                path = path.Substring(0, index);
            }

            DeleteNode(root, path, targetNodeName, pathIsFile);

        }

        private void DeleteNode(Node currentNode, string path, string targetNodeName, bool targetIsFile)
        {
            while (!string.IsNullOrEmpty(path))
            {
                int index = path.IndexOf('/');
                string nextNodeName;
                if (index == -1)
                {
                    nextNodeName = path;
                    path = string.Empty;
                }
                else
                {
                    nextNodeName = path.Substring(0, index);
                    path = path.Substring(index + 1);
                }

                currentNode = currentNode.Children.Find(
                    x => x.NodeName.Equals(nextNodeName) && x.IsFileNode.Equals(false));

                if (currentNode == null) return;
            }
            Node target = currentNode.Children.Find(
                    x => (x.NodeName.Equals(targetNodeName) && x.IsFileNode.Equals(targetIsFile)));
            if (currentNode != null) currentNode.Children.Remove(target);
        }

        private void AddNodeRecursive(string remainingPath, string prefix, Node node)
        {
            if (string.IsNullOrEmpty(remainingPath))
            {
                node.AddChild(prefix, remainingPath, prefix[prefix.Length - 1].Equals('/'));
                return;
            }

            int index = remainingPath.IndexOf('/');
            if (index == -1)
                node.AddChild(prefix, remainingPath, true);
            else if (index == remainingPath.Length - 1)
                node.AddChild(prefix, remainingPath.Substring(0, remainingPath.Length - 1), false);
            else
            {
                string nodeName = remainingPath.Substring(0, index);
                string nextPrefix = prefix + nodeName + '/';
                remainingPath = remainingPath.Substring(index + 1);
                Node child = node.AddChild(prefix, nodeName, false);
                AddNodeRecursive(remainingPath, nextPrefix, child);
            }
        }
        private List<HierarchyNode> GetChildPaths(Node node)
        {
            List<HierarchyNode> hierarchyList = new List<HierarchyNode>();
            foreach (Node child in node.Children)
            {
                hierarchyList.Add(new HierarchyNode
                {
                    Path = child.PathPrefix,
                    Name = child.NodeName,
                    IsFile = child.IsFileNode
                });
                hierarchyList.AddRange(GetChildPaths(child));
            }
            return hierarchyList;
        }

        private Node BuildHierarchy(string prefix, JToken node)
        {
            if (node["Type"].ToObject<string>().Equals("File"))
            {
                return new Node(prefix, node["Name"].ToObject<string>(),
                    node["Type"].ToObject<string>().Equals("File"));
            }

            IList<JToken> childJTokens = node["Children"].Children().ToList();

            prefix += node["Name"].ToObject<string>() + '/';
            List<Node> childNodes = new List<Node>();

            foreach (JToken token in childJTokens)
            {
                childNodes.Add(BuildHierarchy(prefix, token));
            }
            return new Node(prefix, node["Name"].ToObject<string>(),
                node["Type"].ToObject<string>().Equals("File"), childNodes);
        }

        private class NodePair
        {
            public Node Parent { get; set; }
            public Node Target { get; set; }
        }

        private class Node : IEquatable<Node>
        {
            public Node(string pathPrefix, string nodeName, bool isFileNode, List<Node> children = null)
            {
                PathPrefix = pathPrefix;
                NodeName = nodeName;
                Children = children ?? new List<Node>();
                IsFileNode = isFileNode;
            }
            public string NodeName { get; set; }
            public string PathPrefix { get; set; }
            public List<Node> Children { get; set; }
            public bool IsFileNode { get; set; }
            public bool IsLeafNode() => Children.Count == 0;
            public string FlatPath() => PathPrefix + '/' + NodeName;

            public Node AddChild(string pathPrefix, string nodeName, bool isFileNode)
            {
                Node child = new Node(pathPrefix, nodeName, isFileNode);
                if (!Children.Contains(child))
                {
                    Children.Add(child);
                    return child;
                }
                else
                {
                    return Children.Find(x => x.Equals(child));
                }
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(obj, null)) return false;
                if (ReferenceEquals(obj, this)) return true;
                if (obj.GetType() != GetType()) return false;
                var node = obj as Node;
                if (ReferenceEquals(node, null)) return false;
                return node != null && Equals(node);
            }

            public override int GetHashCode() => HashCode.Combine(PathPrefix, NodeName, IsFileNode);

            public bool Equals(Node node) =>
                PathPrefix.Equals(node.PathPrefix)
                    && NodeName.Equals(node.NodeName)
                    && IsFileNode.Equals(node.IsFileNode);
        }
    }
}
