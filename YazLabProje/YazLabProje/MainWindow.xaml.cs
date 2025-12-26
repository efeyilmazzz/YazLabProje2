using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using projedeneme.Models;
using projedeneme.Services;
using projedeneme.Algorithms;

namespace projedeneme
{
    public partial class MainWindow : Window
    {
        private bool nextClickIsStart = true;

        private readonly Dictionary<int, Ellipse> nodeEllipses = new();
        private readonly Dictionary<(int a, int b), Line> edgeLines = new();

        // A* heuristic için canvas pozisyonları
        private readonly Dictionary<int, (double x, double y)> nodePositions = new();

        // WelshPowell / Bileşen renklendirme
        private Dictionary<int, int> nodeColoring = null;

        private int? selectedStartId = null;
        private int? selectedEndId = null;

        private Graph _graph = null;

        private bool _uiReady = false;

        public MainWindow()
        {
            InitializeComponent();

            if (AlgoCombo != null)
                AlgoCombo.SelectionChanged += AlgoCombo_SelectionChanged;

            Log("Hazır. CSV seçip 'Yükle'ye bas.");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _uiReady = true;
            ApplyAlgoUiState();
            ClearNodeDetails();
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var csvFile = (CsvCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.IsNullOrWhiteSpace(csvFile))
            {
                Log("CSV seçilmedi.");
                return;
            }

            try
            {
                var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", csvFile);
                var rows = CsvNodeLoader.Load(path);
                _graph = GraphBuilder.BuildFromRows(rows);

                nodeColoring = null;
                DrawGraphFromGraph(_graph);

                Log($"Yüklendi. Node={_graph.Nodes.Count}, Edge={_graph.Edges.Count}");
                ClearNodeDetails();
            }
            catch
            {
                Log("CSV yüklenemedi.");
            }
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (_graph == null)
            {
                Log("Önce CSV yükle.");
                return;
            }

            var algo = (AlgoCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            try
            {
                // ✅ Welsh/Bileşen dışındaki tüm algoritmalarda renklendirmeyi sıfırla
                if (algo != "Welsh-Powell" && algo != "Bağlı Bileşenler")
                {
                    nodeColoring = null;
                    UpdateNodeColors();
                }

                bool needsStart = AlgoNeedsStart(algo);
                bool needsEnd = AlgoNeedsEnd(algo);

                int startId = -1, endId = -1;

                if (needsStart)
                {
                    if (!int.TryParse((TxtStart.Text ?? "").Trim(), out startId))
                    {
                        Log("Başlangıç node ID geçersiz.");
                        return;
                    }
                    if (!NodeExists(startId))
                    {
                        Log("Başlangıç node bulunamadı.");
                        return;
                    }
                }

                if (needsEnd)
                {
                    if (!int.TryParse((TxtEnd.Text ?? "").Trim(), out endId))
                    {
                        Log("Hedef node ID geçersiz.");
                        return;
                    }
                    if (!NodeExists(endId))
                    {
                        Log("Hedef node bulunamadı.");
                        return;
                    }
                }

                Log($"Algoritma: {algo}");

                if (algo == "BFS")
                {
                    var order = Bfs.Run(_graph, startId);
                    Log($"BFS: {string.Join("->", order)}");
                    HighlightPath(order.ToArray());
                    return;
                }

                if (algo == "DFS")
                {
                    var order = Dfs.Run(_graph, startId);
                    Log($"DFS: {string.Join("->", order)}");
                    HighlightPath(order.ToArray());
                    return;
                }

                if (algo == "Dijkstra")
                {
                    var (path, cost) = Dijkstra.Run(_graph, startId, endId);
                    Log($"Dijkstra: {string.Join("->", path)}");
                    Log($"Maliyet: {cost:0.####}");
                    HighlightPath(path.ToArray());
                    return;
                }

                if (algo == "A*")
                {
                    double Heuristic(int a, int b)
                    {
                        if (nodePositions.TryGetValue(a, out var pa) && nodePositions.TryGetValue(b, out var pb))
                        {
                            double dx = pa.x - pb.x;
                            double dy = pa.y - pb.y;
                            return Math.Sqrt(dx * dx + dy * dy);
                        }
                        return 0.0;
                    }

                    var path = AStar.FindPath(_graph, startId, endId, Heuristic);
                    if (path == null || path.Count == 0)
                    {
                        Log("A*: Yol bulunamadı.");
                        HighlightPath(Array.Empty<int>());
                        return;
                    }

                    Log($"A*: {string.Join("->", path)}");
                    HighlightPath(path.ToArray());
                    return;
                }

                if (algo == "Bağlı Bileşenler")
                {
                    HighlightPath(Array.Empty<int>());

                    var comps = ConnectedComponents.Run(_graph);
                    Log($"Bağlı Bileşen: {comps.Count}");

                    nodeColoring = new Dictionary<int, int>();
                    for (int i = 0; i < comps.Count; i++)
                        foreach (var id in comps[i])
                            nodeColoring[id] = i;

                    UpdateNodeColors();
                    return;
                }

                if (algo == "Degree Centrality")
                {
                    HighlightPath(Array.Empty<int>());
                    nodeColoring = null;
                    UpdateNodeColors();

                    var top = DegreeCentrality.TopK(_graph, 5);
                    Log("Top 5 Degree:");
                    for (int i = 0; i < top.Count; i++)
                        Log($"{i + 1}) Node {top[i].NodeId} -> Degree {top[i].Degree}");
                    return;
                }

                if (algo == "Welsh-Powell")
                {
                    HighlightPath(Array.Empty<int>());

                    var coloring = WelshPowell.Color(_graph);
                    nodeColoring = coloring;
                    UpdateNodeColors();

                    int colorCount = coloring.Values.DefaultIfEmpty(-1).Max() + 1;
                    Log($"Welsh-Powell renk sayısı: {colorCount}");
                    return;
                }

                Log("Algoritma tanınmadı.");
            }
            catch
            {
                Log("Algoritma çalıştırılamadı.");
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            selectedStartId = null;
            selectedEndId = null;
            nextClickIsStart = true;

            // tüm textboxlar
            TxtStart.Text = "";
            TxtEnd.Text = "";

            TxtEditNodeId.Text = "";
            TxtEdgeFrom.Text = "";
            TxtEdgeTo.Text = "";
            TxtEdgeWeight.Text = "1";

            TxtUpdateOldNodeId.Text = "";
            TxtUpdateNewNodeId.Text = "";
            TxtUpdateEdgeFrom.Text = "";
            TxtUpdateEdgeTo.Text = "";
            TxtUpdateEdgeWeight.Text = "1";

            ClearNodeDetails();

            foreach (var kvp in edgeLines)
            {
                kvp.Value.Stroke = Brushes.DimGray;
                kvp.Value.StrokeThickness = 2;
            }

            nodeColoring = null;
            UpdateNodeColors();

            Log("Temizlendi.");
        }

        private void AlgoCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;
            ApplyAlgoUiState();
        }

        private void ApplyAlgoUiState()
        {
            if (TxtStart == null || TxtEnd == null || AlgoCombo == null) return;

            var algo = (AlgoCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            bool startEnabled = AlgoNeedsStart(algo);
            bool endEnabled = AlgoNeedsEnd(algo);

            TxtStart.IsEnabled = startEnabled;
            TxtEnd.IsEnabled = endEnabled;

            if (!startEnabled)
            {
                selectedStartId = null;
                TxtStart.Text = "";
            }
            if (!endEnabled)
            {
                selectedEndId = null;
                TxtEnd.Text = "";
            }

            nextClickIsStart = startEnabled;

            UpdateNodeColors();
        }

        private bool AlgoNeedsStart(string algo)
        {
            if (algo == "BFS" || algo == "DFS") return true;
            if (algo == "Dijkstra" || algo == "A*") return true;
            if (algo == "Bağlı Bileşenler" || algo == "Degree Centrality" || algo == "Welsh-Powell") return false;
            return true;
        }

        private bool AlgoNeedsEnd(string algo)
        {
            if (algo == "Dijkstra" || algo == "A*") return true;
            return false;
        }

        private void GraphCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            if (_graph != null) DrawGraphFromGraph(_graph);
        }

        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_graph != null) DrawGraphFromGraph(_graph);
        }

        private void UiOptionChanged(object sender, RoutedEventArgs e)
        {
            if (_graph != null) DrawGraphFromGraph(_graph);
        }

        private void DrawGraphFromGraph(Graph graph)
        {
            GraphCanvas.Children.Clear();

            nodeEllipses.Clear();
            edgeLines.Clear();
            nodePositions.Clear();

            selectedStartId = null;
            selectedEndId = null;
            TxtStart.Text = "";
            TxtEnd.Text = "";

            double W = GraphCanvas.ActualWidth;
            double H = GraphCanvas.ActualHeight;

            if (W < 50 || H < 50)
            {
                W = GraphCanvas.MinWidth > 0 ? GraphCanvas.MinWidth : 900;
                H = GraphCanvas.MinHeight > 0 ? GraphCanvas.MinHeight : 600;
            }

            double cx = W / 2.0;
            double cy = H / 2.0;

            var nodes = graph.Nodes.OrderBy(n => n.Id).ToList();
            int nCount = nodes.Count;
            if (nCount == 0) return;

            double baseRadius = Math.Min(W, H) * 0.38;
            double radius = baseRadius;
            if (nCount >= 40) radius = baseRadius * 0.85;
            if (nCount >= 70) radius = baseRadius * 0.75;

            for (int i = 0; i < nCount; i++)
            {
                double angle = 2 * Math.PI * i / nCount;
                nodePositions[nodes[i].Id] = (cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
            }

            foreach (var e in graph.Edges)
            {
                if (!nodePositions.TryGetValue(e.From.Id, out var p1)) continue;
                if (!nodePositions.TryGetValue(e.To.Id, out var p2)) continue;

                var line = new Line
                {
                    X1 = p1.x,
                    Y1 = p1.y,
                    X2 = p2.x,
                    Y2 = p2.y,
                    StrokeThickness = 2,
                    Stroke = Brushes.DimGray,
                    IsHitTestVisible = false
                };
                GraphCanvas.Children.Add(line);

                int u = e.From.Id, v = e.To.Id;
                if (u > v) (u, v) = (v, u);
                edgeLines[(u, v)] = line;

                if (ChkShowWeights != null && ChkShowWeights.IsChecked == true)
                {
                    var t = new TextBlock
                    {
                        Text = e.Weight.ToString("0.##", CultureInfo.InvariantCulture),
                        Foreground = Brushes.White,
                        FontSize = 12,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(t, (p1.x + p2.x) / 2 + 4);
                    Canvas.SetTop(t, (p1.y + p2.y) / 2 + 2);
                    GraphCanvas.Children.Add(t);
                }
            }

            foreach (var n in nodes)
            {
                var p = nodePositions[n.Id];
                DrawNode(n.Id, p.x, p.y, nCount);
            }

            UpdateNodeColors();
            ClearNodeDetails();
        }

        private void DrawNode(int id, double x, double y, int nodeCount)
        {
            double r = 16;
            if (nodeCount >= 40) r = 12;
            if (nodeCount >= 70) r = 10;

            var ellipse = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Fill = Brushes.Orange,
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = id
            };
            ellipse.MouseLeftButtonDown += Node_MouseLeftButtonDown;

            Canvas.SetLeft(ellipse, x - r);
            Canvas.SetTop(ellipse, y - r);
            GraphCanvas.Children.Add(ellipse);

            nodeEllipses[id] = ellipse;

            if (ChkShowIds != null && ChkShowIds.IsChecked == true)
            {
                var text = new TextBlock
                {
                    Text = id.ToString(),
                    Foreground = Brushes.Black,
                    FontWeight = FontWeights.Bold,
                    IsHitTestVisible = false,
                    FontSize = nodeCount >= 70 ? 10 : 12
                };
                Canvas.SetLeft(text, x - 4);
                Canvas.SetTop(text, y - 10);
                GraphCanvas.Children.Add(text);
            }
        }

        private void Node_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not Ellipse el) return;
            if (el.Tag == null || !int.TryParse(el.Tag.ToString(), out int id)) return;

            var algo = (AlgoCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            bool startEnabled = AlgoNeedsStart(algo);
            bool endEnabled = AlgoNeedsEnd(algo);

            ShowNodeDetails(id);

            if (!startEnabled && !endEnabled) return;

            if (startEnabled && !endEnabled)
            {
                selectedStartId = id;
                TxtStart.Text = id.ToString();
                nextClickIsStart = true;
                UpdateNodeColors();
                return;
            }

            if (nextClickIsStart)
            {
                selectedStartId = id;
                TxtStart.Text = id.ToString();
            }
            else
            {
                selectedEndId = id;
                TxtEnd.Text = id.ToString();
            }

            nextClickIsStart = !nextClickIsStart;
            UpdateNodeColors();
        }

        // -------------------------
        // Start/End doğrulama (LostFocus)
        // -------------------------
        private void TxtStart_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;
            if (!TxtStart.IsEnabled) return;

            var s = (TxtStart.Text ?? "").Trim();
            if (string.IsNullOrEmpty(s))
            {
                selectedStartId = null;
                UpdateNodeColors();
                return;
            }

            if (!int.TryParse(s, out int id))
            {
                selectedStartId = null;
                UpdateNodeColors();
                Log("Başlangıç ID sayı olmalı.");
                return;
            }

            if (_graph == null)
            {
                selectedStartId = null;
                UpdateNodeColors();
                Log("Önce CSV yükle.");
                return;
            }

            if (!NodeExists(id))
            {
                selectedStartId = null;
                UpdateNodeColors();
                Log("Başlangıç node bulunamadı.");
                return;
            }

            selectedStartId = id;
            ShowNodeDetails(id);
            UpdateNodeColors();
        }

        private void TxtEnd_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;
            if (!TxtEnd.IsEnabled) return;

            var s = (TxtEnd.Text ?? "").Trim();
            if (string.IsNullOrEmpty(s))
            {
                selectedEndId = null;
                UpdateNodeColors();
                return;
            }

            if (!int.TryParse(s, out int id))
            {
                selectedEndId = null;
                UpdateNodeColors();
                Log("Hedef ID sayı olmalı.");
                return;
            }

            if (_graph == null)
            {
                selectedEndId = null;
                UpdateNodeColors();
                Log("Önce CSV yükle.");
                return;
            }

            if (!NodeExists(id))
            {
                selectedEndId = null;
                UpdateNodeColors();
                Log("Hedef node bulunamadı.");
                return;
            }

            selectedEndId = id;
            ShowNodeDetails(id);
            UpdateNodeColors();
        }

        private void UpdateNodeColors()
        {
            foreach (var kvp in nodeEllipses)
            {
                int id = kvp.Key;
                var el = kvp.Value;

                Brush baseBrush = Brushes.Orange;
                if (nodeColoring != null && nodeColoring.TryGetValue(id, out int cidx))
                    baseBrush = GetBrushForColorIndex(cidx);

                if (selectedStartId.HasValue && id == selectedStartId.Value)
                    el.Fill = Brushes.LimeGreen;
                else if (selectedEndId.HasValue && id == selectedEndId.Value)
                    el.Fill = Brushes.Red;
                else
                    el.Fill = baseBrush;
            }
        }

        private Brush GetBrushForColorIndex(int c)
        {
            Brush[] palette =
            {
                Brushes.Orange,
                Brushes.DeepSkyBlue,
                Brushes.Violet,
                Brushes.YellowGreen,
                Brushes.Gold,
                Brushes.Tomato,
                Brushes.MediumSpringGreen,
                Brushes.Cyan
            };
            if (c < 0) c = 0;
            return palette[c % palette.Length];
        }

        private void HighlightPath(int[] pathNodeIds)
        {
            foreach (var kvp in edgeLines)
            {
                kvp.Value.Stroke = Brushes.DimGray;
                kvp.Value.StrokeThickness = 2;
            }

            for (int i = 0; i < pathNodeIds.Length - 1; i++)
            {
                int a = pathNodeIds[i];
                int b = pathNodeIds[i + 1];
                int u = a, v = b;
                if (u > v) (u, v) = (v, u);

                if (edgeLines.TryGetValue((u, v), out var line))
                {
                    line.Stroke = Brushes.DeepSkyBlue;
                    line.StrokeThickness = 5;
                }
            }
        }

        // -------------------------
        // Node Özellik Paneli
        // -------------------------
        private void ClearNodeDetails()
        {
            TxtNodeInfoId.Text = "-";
            TxtNodeInfoDegree.Text = "-";
            TxtNodeInfoNeighbors.Text = "-";
        }

        private void ShowNodeDetails(int nodeId)
        {
            if (_graph == null)
            {
                ClearNodeDetails();
                return;
            }

            var node = _graph.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
            {
                ClearNodeDetails();
                return;
            }

            int degree = _graph.Edges.Count(ed => ed.From.Id == nodeId || ed.To.Id == nodeId);

            var neighbors = _graph.Edges
                .Where(ed => ed.From.Id == nodeId || ed.To.Id == nodeId)
                .Select(ed => ed.From.Id == nodeId ? ed.To.Id : ed.From.Id)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            TxtNodeInfoId.Text = nodeId.ToString();
            TxtNodeInfoDegree.Text = degree.ToString();
            TxtNodeInfoNeighbors.Text = neighbors.Count == 0 ? "-" : string.Join(", ", neighbors);
        }

        // -------------------------
        // Helpers
        // -------------------------
        private bool NodeExists(int id) => _graph != null && _graph.Nodes.Any(n => n.Id == id);

        private bool TryGetNode(int id, out Node node)
        {
            node = null;
            if (_graph == null) return false;
            node = _graph.Nodes.FirstOrDefault(n => n.Id == id);
            return node != null;
        }

        private bool EdgeExistsUndirected(int a, int b)
        {
            return FindEdgeUndirected(a, b) != null;
        }

        private Edge FindEdgeUndirected(int a, int b)
        {
            if (_graph == null) return null;
            int u = Math.Min(a, b);
            int v = Math.Max(a, b);

            return _graph.Edges.FirstOrDefault(ed =>
            {
                int x = ed.From.Id;
                int y = ed.To.Id;
                int ex = Math.Min(x, y);
                int ey = Math.Max(x, y);
                return ex == u && ey == v;
            });
        }

        private double ParseWeightOrFail(string raw, out bool ok)
        {
            ok = true;
            raw = (raw ?? "").Trim();
            if (string.IsNullOrWhiteSpace(raw)) return 1.0;

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var w)) return w;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.GetCultureInfo("tr-TR"), out w)) return w;

            ok = false;
            return 1.0;
        }

        private void RefreshGraphViewAfterEdit()
        {
            selectedStartId = null;
            selectedEndId = null;
            nextClickIsStart = true;

            TxtStart.Text = "";
            TxtEnd.Text = "";

            HighlightPath(Array.Empty<int>());
            DrawGraphFromGraph(_graph);
        }

        // -------------------------
        // Node Yönetimi (Ekle / Sil)
        // -------------------------
        private void BtnAddNode_Click(object sender, RoutedEventArgs e)
        {
            if (_graph == null) { Log("Önce CSV yükle."); return; }

            if (!int.TryParse((TxtEditNodeId.Text ?? "").Trim(), out int id))
            {
                Log("Node ID geçersiz.");
                return;
            }

            if (NodeExists(id))
            {
                Log("Bu Node ID zaten var.");
                return;
            }

            _graph.Nodes.Add(new Node { Id = id });

            RefreshGraphViewAfterEdit();
            Log($"Node eklendi: {id}");

            TxtEditNodeId.Text = "";
        }

        private void BtnRemoveNode_Click(object sender, RoutedEventArgs e)
        {
            if (_graph == null) { Log("Önce CSV yükle."); return; }

            if (!int.TryParse((TxtEditNodeId.Text ?? "").Trim(), out int id))
            {
                Log("Node ID geçersiz.");
                return;
            }

            var node = _graph.Nodes.FirstOrDefault(n => n.Id == id);
            if (node == null)
            {
                Log("Node bulunamadı.");
                return;
            }

            _graph.Edges.RemoveAll(ed => ed.From.Id == id || ed.To.Id == id);
            _graph.Nodes.Remove(node);

            RefreshGraphViewAfterEdit();
            ClearNodeDetails();
            Log($"Node silindi: {id}");

            TxtEditNodeId.Text = "";
        }

        // -------------------------
        // Edge Yönetimi (Ekle / Sil)
        // -------------------------
        private void BtnAddEdge_Click(object sender, RoutedEventArgs e)
        {
            if (_graph == null) { Log("Önce CSV yükle."); return; }

            if (!int.TryParse((TxtEdgeFrom.Text ?? "").Trim(), out int fromId) ||
                !int.TryParse((TxtEdgeTo.Text ?? "").Trim(), out int toId))
            {
                Log("From/To ID geçersiz.");
                return;
            }

            if (fromId == toId)
            {
                Log("Self-loop yasak (From=To).");
                return;
            }

            if (!TryGetNode(fromId, out var fromNode) || !TryGetNode(toId, out var toNode))
            {
                Log("From/To node bulunamadı.");
                return;
            }

            if (EdgeExistsUndirected(fromId, toId))
            {
                Log("Bu edge zaten var.");
                return;
            }

            var w = ParseWeightOrFail(TxtEdgeWeight.Text, out bool ok);
            if (!ok)
            {
                Log("Weight geçersiz. Örn: 1 veya 1.5");
                return;
            }

            _graph.Edges.Add(new Edge { From = fromNode, To = toNode, Weight = w });

            RefreshGraphViewAfterEdit();
            Log($"Edge eklendi: {fromId} - {toId} (w={w:0.##})");

            TxtEdgeFrom.Text = "";
            TxtEdgeTo.Text = "";
            TxtEdgeWeight.Text = "1";
        }

        private void BtnRemoveEdge_Click(object sender, RoutedEventArgs e)
        {
            if (_graph == null) { Log("Önce CSV yükle."); return; }

            if (!int.TryParse((TxtEdgeFrom.Text ?? "").Trim(), out int fromId) ||
                !int.TryParse((TxtEdgeTo.Text ?? "").Trim(), out int toId))
            {
                Log("From/To ID geçersiz.");
                return;
            }

            if (fromId == toId)
            {
                Log("Self-loop yok (From=To).");
                return;
            }

            var edge = FindEdgeUndirected(fromId, toId);
            if (edge == null)
            {
                Log("Edge bulunamadı.");
                return;
            }

            _graph.Edges.Remove(edge);

            RefreshGraphViewAfterEdit();
            Log($"Edge silindi: {fromId} - {toId}");

            TxtEdgeFrom.Text = "";
            TxtEdgeTo.Text = "";
            TxtEdgeWeight.Text = "1";
        }

        // -------------------------
        // ✅ Güncelleme: Node Rename
        // -------------------------
        private void BtnUpdateNode_Click(object sender, RoutedEventArgs e)
        {
            if (_graph == null) { Log("Önce CSV yükle."); return; }

            if (!int.TryParse((TxtUpdateOldNodeId.Text ?? "").Trim(), out int oldId) ||
                !int.TryParse((TxtUpdateNewNodeId.Text ?? "").Trim(), out int newId))
            {
                Log("Eski/Yeni Node ID geçersiz.");
                return;
            }

            if (oldId == newId)
            {
                Log("Yeni ID eski ID ile aynı olamaz.");
                return;
            }

            var node = _graph.Nodes.FirstOrDefault(n => n.Id == oldId);
            if (node == null)
            {
                Log("Güncellenecek node bulunamadı.");
                return;
            }

            if (NodeExists(newId))
            {
                Log("Yeni Node ID zaten var.");
                return;
            }

            // Node ID değiştir (edge'ler Node referansı tuttuğu için otomatik güncellenir)
            node.Id = newId;

            // Start/End seçiliyse temizle (çünkü id değişti)
            selectedStartId = null;
            selectedEndId = null;
            TxtStart.Text = "";
            TxtEnd.Text = "";

            // Renklendirme varsa eski mapping bozulur, temizle
            nodeColoring = null;

            RefreshGraphViewAfterEdit();
            Log($"Node güncellendi: {oldId} -> {newId}");

            TxtUpdateOldNodeId.Text = "";
            TxtUpdateNewNodeId.Text = "";
        }

        // -------------------------
        // ✅ Güncelleme: Edge Weight Update
        // -------------------------
        private void BtnUpdateEdge_Click(object sender, RoutedEventArgs e)
        {
            if (_graph == null) { Log("Önce CSV yükle."); return; }

            if (!int.TryParse((TxtUpdateEdgeFrom.Text ?? "").Trim(), out int fromId) ||
                !int.TryParse((TxtUpdateEdgeTo.Text ?? "").Trim(), out int toId))
            {
                Log("From/To ID geçersiz.");
                return;
            }

            if (fromId == toId)
            {
                Log("Self-loop yok (From=To).");
                return;
            }

            var edge = FindEdgeUndirected(fromId, toId);
            if (edge == null)
            {
                Log("Güncellenecek edge bulunamadı.");
                return;
            }

            var w = ParseWeightOrFail(TxtUpdateEdgeWeight.Text, out bool ok);
            if (!ok)
            {
                Log("Yeni weight geçersiz. Örn: 1 veya 1.5");
                return;
            }

            edge.Weight = w;

            // Renklendirme yoksa zaten normal, varsa da bozmaya gerek yok
            RefreshGraphViewAfterEdit();
            Log($"Edge güncellendi: {fromId} - {toId} (w={w:0.##})");

            TxtUpdateEdgeFrom.Text = "";
            TxtUpdateEdgeTo.Text = "";
            TxtUpdateEdgeWeight.Text = "1";
        }

        private void Log(string msg)
        {
            TxtLog.AppendText(msg + "\n");
            TxtLog.ScrollToEnd();
        }
    }
}
