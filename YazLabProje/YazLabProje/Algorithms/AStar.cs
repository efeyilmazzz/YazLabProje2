using System;
using System.Collections.Generic;
using System.Linq;
using projedeneme.Models;

namespace projedeneme.Algorithms
{
    public static class AStar
    {
        
        public static List<int> FindPath(Graph graph, int startId, int goalId, Func<int, int, double> heuristic)
        {
            if (graph == null) return new List<int>();
            if (startId == goalId) return new List<int> { startId };

            // Node var mı kontrol
            var nodesById = graph.Nodes.ToDictionary(n => n.Id, n => n);
            if (!nodesById.ContainsKey(startId) || !nodesById.ContainsKey(goalId))
                return new List<int>();

            heuristic ??= ((_, __) => 0.0);

            // adjacency: id -> list of (neighborId, weight)
            // Senin UI/highlight mantığın undirected gibi olduğu için iki yönlü kuruyoruz.
            var adj = BuildAdjacency(graph);

            var cameFrom = new Dictionary<int, int>();
            var gScore = new Dictionary<int, double> { [startId] = 0.0 };

            var open = new MinOpenSet();
            open.Push(startId, heuristic(startId, goalId));

            var closed = new HashSet<int>();

            while (open.Count > 0)
            {
                int current = open.PopMin();

                if (current == goalId)
                    return Reconstruct(cameFrom, current);

                if (!closed.Add(current))
                    continue;

                if (!adj.TryGetValue(current, out var neighbors))
                    continue;

                foreach (var (nb, w) in neighbors)
                {
                    if (closed.Contains(nb)) continue;

                    // weighted edge cost
                    double tentativeG = gScore[current] + w;

                    if (!gScore.TryGetValue(nb, out var oldG) || tentativeG < oldG)
                    {
                        cameFrom[nb] = current;
                        gScore[nb] = tentativeG;

                        double f = tentativeG + heuristic(nb, goalId);
                        open.Push(nb, f);
                    }
                }
            }

            return new List<int>(); // no path
        }

        private static Dictionary<int, List<(int nb, double w)>> BuildAdjacency(Graph graph)
        {
            var adj = new Dictionary<int, List<(int nb, double w)>>();

            foreach (var e in graph.Edges)
            {
                int a = e.From.Id;
                int b = e.To.Id;
                double w = e.Weight;

                if (!adj.TryGetValue(a, out var la))
                    adj[a] = la = new List<(int nb, double w)>();
                la.Add((b, w));

                if (!adj.TryGetValue(b, out var lb))
                    adj[b] = lb = new List<(int nb, double w)>();
                lb.Add((a, w));
            }

            return adj;
        }

        private static List<int> Reconstruct(Dictionary<int, int> cameFrom, int current)
        {
            var path = new List<int> { current };
            while (cameFrom.TryGetValue(current, out var parent))
            {
                current = parent;
                path.Add(current);
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Simple min-priority open set (duplicates allowed).
        /// Closed set prevents re-processing.
        /// </summary>
        private sealed class MinOpenSet
        {
            private readonly SortedSet<Item> _set = new SortedSet<Item>(new ItemComparer());
            private long _seq = 0;

            public int Count => _set.Count;

            public void Push(int nodeId, double priority)
                => _set.Add(new Item(priority, _seq++, nodeId));

            public int PopMin()
            {
                var min = _set.Min;
                _set.Remove(min);
                return min.NodeId;
            }

            private readonly struct Item
            {
                public readonly double Priority;
                public readonly long Seq;
                public readonly int NodeId;

                public Item(double priority, long seq, int nodeId)
                {
                    Priority = priority;
                    Seq = seq;
                    NodeId = nodeId;
                }
            }

            private sealed class ItemComparer : IComparer<Item>
            {
                public int Compare(Item x, Item y)
                {
                    int c = x.Priority.CompareTo(y.Priority);
                    if (c != 0) return c;

                    c = x.Seq.CompareTo(y.Seq);
                    if (c != 0) return c;

                    return x.NodeId.CompareTo(y.NodeId);
                }
            }
        }
    }
}