using System;
using System.Collections.Generic;
using System.Linq;
using projedeneme.Models;

namespace projedeneme.Algorithms
{
    public static class ConnectedComponents
    {
        // Undirected bağlı bileşenler: her bileşen node id listesi
        public static List<List<int>> Run(Graph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            var adj = BuildAdj(graph);

            var visited = new HashSet<int>();
            var components = new List<List<int>>();

            foreach (var id in graph.Nodes.Select(n => n.Id).OrderBy(x => x))
            {
                if (visited.Contains(id)) continue;

                var comp = new List<int>();
                var q = new Queue<int>();
                visited.Add(id);
                q.Enqueue(id);

                while (q.Count > 0)
                {
                    int u = q.Dequeue();
                    comp.Add(u);

                    if (!adj.TryGetValue(u, out var neigh)) continue;
                    foreach (var v in neigh)
                    {
                        if (visited.Add(v))
                            q.Enqueue(v);
                    }
                }

                comp.Sort();
                components.Add(comp);
            }

            return components;
        }

        private static Dictionary<int, List<int>> BuildAdj(Graph graph)
        {
            var adj = graph.Nodes.ToDictionary(n => n.Id, _ => new List<int>());

            foreach (var e in graph.Edges)
            {
                int a = e.From.Id;
                int b = e.To.Id;
                if (a == b) continue;

                if (!adj.ContainsKey(a)) adj[a] = new List<int>();
                if (!adj.ContainsKey(b)) adj[b] = new List<int>();

                adj[a].Add(b);
                adj[b].Add(a);
            }

            // deterministik gezi için
            foreach (var k in adj.Keys.ToList())
                adj[k] = adj[k].Distinct().OrderBy(x => x).ToList();

            return adj;
        }
    }
}
