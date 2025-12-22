using System;
using System.Collections.Generic;
using System.Linq;
using projedeneme.Models;

namespace projedeneme.Algorithms
{
    public static class DegreeCentrality
    {
        // Degree (kenar sayısı) bazlı TopK
        public static List<(int NodeId, int Degree)> TopK(Graph graph, int k)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (k <= 0) return new List<(int, int)>();

            var degree = graph.Nodes.ToDictionary(n => n.Id, _ => 0);

            foreach (var e in graph.Edges)
            {
                int a = e.From.Id;
                int b = e.To.Id;
                if (a == b) continue;

                if (!degree.ContainsKey(a)) degree[a] = 0;
                if (!degree.ContainsKey(b)) degree[b] = 0;

                degree[a]++;
                degree[b]++;
            }

            return degree
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Key)
                .Take(k)
                .Select(x => (x.Key, x.Value))
                .ToList();
        }
    }
}
