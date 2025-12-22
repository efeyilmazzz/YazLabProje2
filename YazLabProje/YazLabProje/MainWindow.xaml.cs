using System;
using System.Collections.Generic;
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

        private readonly Dictionary<int, Ellipse> nodeEllipses = new Dictionary<int, Ellipse>();
        private readonly Dictionary<(int a, int b), Line> edgeLines = new Dictionary<(int a, int b), Line>();

        // ✅ A* heuristic için canvas pozisyonlarını saklayacağız
        private readonly Dictionary<int, (double x, double y)> nodePositions = new Dictionary<int, (double x, double y)>();
        private Dictionary<int, int> nodeColoring = null;
        private int? selectedStartId = null;
        private int? selectedEndId = null;
        

        private Graph _graph = null; // nullable kapalı uyum

        public MainWindow()
        {
            InitializeComponent();
            Log("UI hazır. CSV seçip 'Yükle'ye bas.");

            if (TxtStart != null) TxtStart.IsReadOnly = true;
            if (TxtEnd != null) TxtEnd.IsReadOnly = true;

            if (AlgoCombo != null)
                AlgoCombo.SelectionChanged += AlgoCombo_SelectionChanged;

            ApplyAlgoUiState();
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var csvFile = (CsvCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.IsNullOrWhiteSpace(csvFile))
            {
                Log("[HATA] CSV seçilmedi.");
                return;
            }

            try
            {
                var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", csvFile);
                Log($"[UI] CSV path: {path}");

                var rows = CsvNodeLoader.Load(path);
                _graph = GraphBuilder.BuildFromRows(rows);

                DrawGraphFromGraph(_graph);
                Log($"[OK] Yüklendi. Node={_graph.Nodes.Count}, Edge={_graph.Edges.Count}");
            }
            catch (Exception ex)
            {
                Log("[HATA] " + ex.Message);
            }
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (_graph == null)
            {
                Log("[HATA] Önce CSV yükle.");
                return;
            }

            var algo = (AlgoCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            Log($"[UI] Algo: {algo}, Start={TxtStart.Text}, End={TxtEnd.Text}");

            try
            {
                bool needsStart = AlgoNeedsStart(algo);
                bool needsEnd = AlgoNeedsEnd(algo);

                int startId = -1;
                int endId = -1;

                if (needsStart)
                {
                    if (!int.TryParse(TxtStart.Text, out startId))
                    {
                        Log("[HATA] Start seç (node'a tıkla).");
                        return;
                    }
                }

                if (needsEnd)
                {
                    if (!int.TryParse(TxtEnd.Text, out endId))
                    {
                        Log("[HATA] End seç (node'a tıkla).");
                        return;
                    }
                }

                // BFS
                if (algo == "BFS")
                {
                    var order = Bfs.Run(_graph, startId);
                    Log($"[OK] BFS order: {string.Join("->", order)}");
                    HighlightPath(order.ToArray());
                    return;
                }

                // DFS
                if (algo == "DFS")
                {
                    var order = Dfs.Run(_graph, startId);
                    Log($"[OK] DFS order: {string.Join("->", order)}");
                    HighlightPath(order.ToArray());
                    return;
                }

                // Dijkstra (sende zaten çalışıyor)
                if (algo == "Dijkstra")
                {
                    var (path, cost) = Dijkstra.Run(_graph, startId, endId);

                    Log($"[OK] Dijkstra path: {string.Join("->", path)}");
                    Log($"[OK] Toplam maliyet: {cost:0.####}");

                    HighlightPath(path.ToArray());
                    return;
                }

                // ✅ A* (FIX: nodePositions var + TryGetValue yazım düzeltildi)
                if (algo == "A*")
                {
                    double Heuristic(int a, int b)
                    {
                        if (nodePositions.TryGetValue(a, out var pa) &&
                            nodePositions.TryGetValue(b, out var pb))
                        {
                            double dx = pa.x - pb.x;
                            double dy = pa.y - pb.y;
                            return Math.Sqrt(dx * dx + dy * dy);
                        }
                        return 0.0; // pozisyon yoksa Dijkstra gibi
                    }

                    var path = AStar.FindPath(_graph, startId, endId, Heuristic);

                    if (path == null || path.Count == 0)
                    {
                        Log("[HATA] A*: Yol bulunamadı.");
                        HighlightPath(Array.Empty<int>());
                        return;
                    }

                    Log($"[OK] A* path: {string.Join("->", path)}");
                    HighlightPath(path.ToArray());
                    return;
                }

                if (algo == "Bağlı Bileşenler")
                {
                    HighlightPath(Array.Empty<int>());

                    var comps = ConnectedComponents.Run(_graph);
                    Log($"[OK] Bağlı Bileşen Sayısı: {comps.Count}");

                    for (int i = 0; i < comps.Count; i++)
                        Log($"  Bileşen {i + 1} ({comps[i].Count} node): {string.Join(", ", comps[i])}");

                    // bileşenleri renklendir
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
                    Log("[OK] Top 5 Degree Centrality:");
                    for (int i = 0; i < top.Count; i++)
                        Log($"  {i + 1}) Node {top[i].NodeId} -> Degree {top[i].Degree}");

                    return;
                }

                if (algo == "Welsh-Powell")
                {
                    HighlightPath(Array.Empty<int>());

                    var coloring = WelshPowell.Color(_graph);
                    nodeColoring = coloring;
                    UpdateNodeColors();

                    int colorCount = coloring.Values.DefaultIfEmpty(-1).Max() + 1;
                    Log($"[OK] Welsh-Powell: Kullanılan renk sayısı = {colorCount}");

                   
                    return;
                }


                Log("[HATA] Seçilen algoritma tanınmadı.");
            }
            catch (Exception ex)
            {
                Log("[HATA] " + ex.Message);
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            selectedStartId = null;
            selectedEndId = null;
            nextClickIsStart = true;

            if (TxtStart != null) TxtStart.Text = "";
            if (TxtEnd != null) TxtEnd.Text = "";

            foreach (var kvp in edgeLines)
            {
                kvp.Value.Stroke = Brushes.DimGray;
                kvp.Value.StrokeThickness = 2;
            }

            UpdateNodeColors();
            Log("[UI] Seçimler temizlendi (grafik/CSV korunuyor).");
        }

        // --- Algo UI state ---

        private void AlgoCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyAlgoUiState();
        }

        private void ApplyAlgoUiState()
        {
            var algo = (AlgoCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            bool startEnabled = AlgoNeedsStart(algo);
            bool endEnabled = AlgoNeedsEnd(algo);

            if (TxtStart != null) TxtStart.IsEnabled = startEnabled;
            if (TxtEnd != null) TxtEnd.IsEnabled = endEnabled;

            if (!startEnabled)
            {
                selectedStartId = null;
                if (TxtStart != null) TxtStart.Text = "";
            }

            if (!endEnabled)
            {
                selectedEndId = null;
                if (TxtEnd != null) TxtEnd.Text = "";
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
            if (algo == "BFS" || algo == "DFS") return false;

            if (algo == "Bağlı Bileşenler" || algo == "Degree Centrality" || algo == "Welsh-Powell") return false;
            return false;
        }

        // --- Canvas events ---

        private void GraphCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            if (_graph != null)
                DrawGraphFromGraph(_graph);
        }

        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_graph != null)
                DrawGraphFromGraph(_graph);
        }

        private void UiOptionChanged(object sender, RoutedEventArgs e)
        {
            if (_graph != null)
                DrawGraphFromGraph(_graph);
        }

        // --- Draw Graph ---

        private void DrawGraphFromGraph(Graph graph)
        {
            GraphCanvas.Children.Clear();

            nodeEllipses.Clear();
            edgeLines.Clear();

            // ✅ positions reset
            nodePositions.Clear();

            selectedStartId = null;
            selectedEndId = null;

            ApplyAlgoUiState();

            double W = GraphCanvas.ActualWidth;
            double H = GraphCanvas.ActualHeight;

            if (W < 50 || H < 50)
            {
                W = GraphCanvas.MinWidth > 0 ? GraphCanvas.MinWidth : 800;
                H = GraphCanvas.MinHeight > 0 ? GraphCanvas.MinHeight : 450;
            }

            double cx = W / 2.0;
            double cy = H / 2.0;
            double radius = Math.Min(W, H) * 0.35;

            var nodes = graph.Nodes.OrderBy(n => n.Id).ToList();
            int nCount = nodes.Count;
            if (nCount == 0) return;

            // ✅ nodePositions doldur
            for (int i = 0; i < nCount; i++)
            {
                double angle = 2 * Math.PI * i / nCount;
                nodePositions[nodes[i].Id] = (cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
            }

            // Edge çiz
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
                if (u > v) { int tmp = u; u = v; v = tmp; }
                edgeLines[(u, v)] = line;

                if (ChkShowWeights != null && ChkShowWeights.IsChecked == true)
                {
                    var t = new TextBlock
                    {
                        Text = e.Weight.ToString("0.##"),
                        Foreground = Brushes.White,
                        FontSize = 12,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(t, (p1.x + p2.x) / 2 + 4);
                    Canvas.SetTop(t, (p1.y + p2.y) / 2 + 2);
                    GraphCanvas.Children.Add(t);
                }
            }

            // Node çiz
            foreach (var n in nodes)
            {
                var p = nodePositions[n.Id];
                DrawNode(n.Id, p.x, p.y);
            }

            UpdateNodeColors();
        }

        private void DrawNode(int id, double x, double y)
        {
            double r = 16;

            var ellipse = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Fill = Brushes.Orange,
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            ellipse.Tag = id;
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
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(text, x - 4);
                Canvas.SetTop(text, y - 10);
                GraphCanvas.Children.Add(text);
            }
        }

        private void Node_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var el = sender as Ellipse;
            if (el == null) return;

            if (el.Tag == null || !int.TryParse(el.Tag.ToString(), out int id)) return;

            var algo = (AlgoCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            bool startEnabled = AlgoNeedsStart(algo);
            bool endEnabled = AlgoNeedsEnd(algo);

            if (!startEnabled && !endEnabled)
                return;

            if (startEnabled && !endEnabled)
            {
                selectedStartId = id;
                TxtStart.Text = id.ToString();
                Log($"[UI] Start seçildi: {id}");
                nextClickIsStart = true;
                UpdateNodeColors();
                return;
            }

            if (nextClickIsStart)
            {
                selectedStartId = id;
                TxtStart.Text = id.ToString();
                Log($"[UI] Start seçildi: {id}");
            }
            else
            {
                selectedEndId = id;
                TxtEnd.Text = id.ToString();
                Log($"[UI] End seçildi: {id}");
            }

            nextClickIsStart = !nextClickIsStart;
            UpdateNodeColors();
        }

        private void UpdateNodeColors()
        {
            foreach (var kvp in nodeEllipses)
            {
                int id = kvp.Key;
                Ellipse el = kvp.Value;

                // default renk
                Brush baseBrush = Brushes.Orange;

                // Welsh-Powell / Bileşen boyama varsa uygula
                if (nodeColoring != null && nodeColoring.TryGetValue(id, out int cidx))
                    baseBrush = GetBrushForColorIndex(cidx);

                // Start/End her zaman override etsin
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
                if (u > v) { int tmp = u; u = v; v = tmp; }

                if (edgeLines.TryGetValue((u, v), out Line line))
                {
                    line.Stroke = Brushes.DeepSkyBlue;
                    line.StrokeThickness = 5;
                }
            }

            Log($"[UI] Highlight: {string.Join("->", pathNodeIds)}");
        }

        private void Log(string msg)
        {
            TxtLog.AppendText(msg + "\n");
            TxtLog.ScrollToEnd();
        }
    }
}
