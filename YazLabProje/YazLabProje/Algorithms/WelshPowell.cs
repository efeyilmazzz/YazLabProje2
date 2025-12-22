using System;
using System.Collections.Generic;
using System.Linq;
using projedeneme.Models;

namespace projedeneme.Algorithms
{
    public static class WelshPowell
    {
        // Sonuç: nodeId -> colorIndex (0,1,2,...)
        public static Dictionary<int, int> Color(Graph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            var adj = BuildAdj(graph);
            var degree = adj.ToDictionary(kv => kv.Key, kv => kv.Value.Count);

            // Welsh-Powell: dereceye göre azalan sırala
            var order = degree
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Key)
                .Select(x => x.Key)
                .ToList();

            var colorOf = new Dictionary<int, int>();

            foreach (var u in order)
            {
                // komşu renklerini topla
                var used = new HashSet<int>();
                foreach (var v in adj[u])
                {
                    if (colorOf.TryGetValue(v, out int c))
                        used.Add(c);
                }

                // en küçük kullanılmayan rengi ver
                int color = 0;
                while (used.Contains(color)) color++;
                colorOf[u] = color;
            }

            return colorOf;
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

            foreach (var k in adj.Keys.ToList())
                adj[k] = adj[k].Distinct().OrderBy(x => x).ToList();

            return adj;
        }
    }
}
