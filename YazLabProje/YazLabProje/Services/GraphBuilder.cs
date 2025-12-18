using projedeneme.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using projedeneme.Models;

namespace projedeneme.Services;

public static class GraphBuilder
{
    public static Graph BuildFromRows(List<NodeRow> rows)
    {
        var graph = new Graph();

        var nodes = rows.Select(r => new Node
        {
            Id = r.DugumId,
            Aktiflik = r.Aktiflik,
            Etkilesim = r.Etkilesim,
            BaglantiSayisi = r.BaglantiSayisi,
            KomsuIdler = ParseNeighbors(r.Komsular)
        }).ToList();

        graph.Nodes.AddRange(nodes);

        var byId = graph.Nodes.ToDictionary(n => n.Id, n => n);

        var added = new HashSet<(int, int)>();

        foreach (var n in graph.Nodes)
        {
            foreach (var nbId in n.KomsuIdler)
            {
                if (!byId.TryGetValue(nbId, out var nb)) continue;

                int u = n.Id, v = nb.Id;
                if (u > v) (u, v) = (v, u);
                if (!added.Add((u, v))) continue;

                graph.Edges.Add(new Edge
                {
                    From = byId[u],
                    To = byId[v],
                    Weight = WeightCalculator.Calculate(byId[u], byId[v])
                });

            }
        }

        return graph;
    }

    private static List<int> ParseNeighbors(string komsular)
    {
        if (string.IsNullOrWhiteSpace(komsular)) return new();

        var parts = komsular
            .Trim().Trim('"')
            .Replace("[", "").Replace("]", "")
            .Replace(";", ",")
            .Replace(" ", ",")
            .Split(',', StringSplitOptions.RemoveEmptyEntries);

        var list = new List<int>();
        foreach (var p in parts)
            if (int.TryParse(p.Trim(), out int id))
                list.Add(id);

        return list;
    }
}